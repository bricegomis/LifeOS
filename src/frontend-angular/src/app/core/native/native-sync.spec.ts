import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { splitInstructionsIntoSteps } from './recipe-steps.ts'
import { civilTimeToIso, projectEventsFromWeeks } from './event-projection.ts'
import { hashNotificationId } from './notification-id.ts'
import type { ManualWeek, ManualDay, ManualMeal, ManualSport } from '../api/manual-planner.models.ts'

describe('recipe-steps instruction splitting', () => {
  it('returns empty array on empty or null instructions', () => {
    assert.deepEqual(splitInstructionsIntoSteps(''), [])
    assert.deepEqual(splitInstructionsIntoSteps('   '), [])
    assert.deepEqual(splitInstructionsIntoSteps(null), [])
    assert.deepEqual(splitInstructionsIntoSteps(undefined), [])
  })

  it('splits newline-separated steps and strips bullet numbers', () => {
    const raw = `1. Préchauffer le four à 180°C.
2. Émincer les oignons.
- Faire revenir dans l'huile d'olive.
* Laisser mijoter 20 minutes.`

    const steps = splitInstructionsIntoSteps(raw)
    assert.equal(steps.length, 4)
    assert.equal(steps[0], 'Préchauffer le four à 180°C.')
    assert.equal(steps[1], 'Émincer les oignons.')
    assert.equal(steps[2], "Faire revenir dans l'huile d'olive.")
    assert.equal(steps[3], 'Laisser mijoter 20 minutes.')
  })

  it('splits single-line instructions with inline numbering deterministically without inventing steps', () => {
    const raw = '1. Laver les légumes. 2. Couper en dés. 3. Servir frais.'
    const steps = splitInstructionsIntoSteps(raw)
    assert.equal(steps.length, 3)
    assert.equal(steps[0], 'Laver les légumes.')
    assert.equal(steps[1], 'Couper en dés.')
    assert.equal(steps[2], 'Servir frais.')
  })

  it('preserves single un-numbered paragraph as a single step', () => {
    const raw = 'Mélanger tous les ingrédients dans un grand saladier.'
    const steps = splitInstructionsIntoSteps(raw)
    assert.deepEqual(steps, ['Mélanger tous les ingrédients dans un grand saladier.'])
  })
})

describe('event-projection and civil time', () => {
  it('converts civil date and minute to ISO string respecting timeZone', () => {
    // 2026-10-15 at 12:30 (minute = 750) in Europe/Paris (UTC+2 in October)
    const iso = civilTimeToIso('2026-10-15', 750, 'Europe/Paris')
    assert.match(iso, /^2026-10-15T10:30:00/)

    // 2026-12-15 at 12:30 (minute = 750) in Europe/Paris (UTC+1 in winter)
    const winterIso = civilTimeToIso('2026-12-15', 750, 'Europe/Paris')
    assert.match(winterIso, /^2026-12-15T11:30:00/)
  })

  it('projects manual weeks into sorted event items with null startMinute at the end of the day', () => {
    const mealWithTime: ManualMeal = {
      id: 'm1', dayPlanId: 'd1', name: 'Déjeuner', startMinute: 720, endMinute: 780,
      recipeLines: [{ id: 'r1', recipeId: 'rec1', name: 'Pâtes', instructions: 'Cuire', yieldServings: 2, plannedServings: 2, isAdHoc: false, portions: [], ingredients: [], nutrition: { calories: 400, protein: 12, carbs: 70, fat: 5, isComplete: true, warnings: [] } }],
      nutrition: { calories: 400, protein: 12, carbs: 70, fat: 5, isComplete: true, warnings: [] },
    }
    const mealWithoutTime: ManualMeal = {
      id: 'm2', dayPlanId: 'd1', name: 'Collation', startMinute: null, endMinute: null,
      recipeLines: [],
      nutrition: { calories: 150, protein: 3, carbs: 20, fat: 2, isComplete: true, warnings: [] },
    }
    const sport: ManualSport = {
      id: 's1', dayPlanId: 'd1', sportTemplateId: null, name: 'Course', sport: 'running',
      intensity: 'high', durationMinutes: 45, distanceKm: 8, calories: 500, startMinute: 480, endMinute: 525,
    }

    const day: ManualDay = {
      id: 'd1', date: '2026-10-15', meals: [mealWithoutTime, mealWithTime], sports: [sport],
      nutrition: { calories: 550, protein: 15, carbs: 90, fat: 7, isComplete: true, warnings: [] },
    }

    const week: ManualWeek = {
      id: 'w1', startsOn: '2026-10-12', timeZoneId: 'Europe/Paris', isManual: true, days: [day],
    }

    const events = projectEventsFromWeeks([week], new Date('2026-10-15T06:00:00Z'), 'Europe/Paris')

    assert.equal(events.length, 3)
    // First should be sport (startMinute 480 = 08:00)
    assert.equal(events[0]?.id, 'sport-s1')
    assert.equal(events[0]?.kind, 'sport')
    assert.equal(events[0]?.startMinute, 480)

    // Second should be mealWithTime (startMinute 720 = 12:00)
    assert.equal(events[1]?.id, 'meal-m1')
    assert.equal(events[1]?.kind, 'meal')
    assert.equal(events[1]?.startMinute, 720)

    // Third should be mealWithoutTime (startMinute null)
    assert.equal(events[2]?.id, 'meal-m2')
    assert.equal(events[2]?.startMinute, null)
    assert.equal(events[2]?.startIso, null)
  })
})

describe('local reminders hashNotificationId', () => {
  it('generates deterministic stable 32-bit positive integer IDs', () => {
    const id1 = hashNotificationId('meal-m1')
    const id2 = hashNotificationId('meal-m1')
    const id3 = hashNotificationId('sport-s1')

    assert.equal(id1, id2)
    assert.notEqual(id1, id3)
    assert.ok(Number.isInteger(id1))
    assert.ok(id1 >= 0)
    assert.ok(id1 < 2147483647)
  })
})

describe('untimed events and notifications exclusion', () => {
  it('preserves null startMinute without inventing fake hours or ISO timestamps', () => {
    const mealWithoutTime: ManualMeal = {
      id: 'm-untimed', dayPlanId: 'd1', name: 'Déjeuner informel', startMinute: null, endMinute: null,
      recipeLines: [], nutrition: { calories: 0, protein: 0, carbs: 0, fat: 0, isComplete: false, warnings: [] },
    }
    const day: ManualDay = {
      id: 'd1', date: '2026-10-15', meals: [mealWithoutTime], sports: [],
      nutrition: { calories: 0, protein: 0, carbs: 0, fat: 0, isComplete: false, warnings: [] },
    }
    const week: ManualWeek = {
      id: 'w1', startsOn: '2026-10-12', timeZoneId: 'Europe/Paris', isManual: true, days: [day],
    }

    const events = projectEventsFromWeeks([week], new Date('2026-10-15T00:00:00Z'), 'Europe/Paris')
    assert.equal(events.length, 1)
    const event = events[0]!
    assert.equal(event.startMinute, null)
    assert.equal(event.startIso, null)
    assert.equal(event.endMinute, null)
    assert.equal(event.endIso, null)
  })
})

describe('native URL and deep link resolution', () => {
  it('extracts path and safe redirect from native callback URLs', () => {
    const authUrl = new URL('lifeos://auth/callback?code=oauth123&redirect=%2Fplanning')
    assert.equal(authUrl.searchParams.get('code'), 'oauth123')
    assert.equal(authUrl.searchParams.get('redirect'), '/planning')

    const planningDeepLink = 'lifeos:///planning?date=2026-10-15'
    const matchPlanning = planningDeepLink.match(/^lifeos:\/\/?\/?(.*)$/)
    assert.ok(matchPlanning)
    assert.equal('/' + matchPlanning[1], '/planning?date=2026-10-15')

    const recipesDeepLink = 'lifeos:///recipes'
    const matchRecipes = recipesDeepLink.match(/^lifeos:\/\/?\/?(.*)$/)
    assert.ok(matchRecipes)
    assert.equal('/' + matchRecipes[1], '/recipes')
  })
})
