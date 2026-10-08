import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { eventDraftFrom, eventDraftHasSecondaryValues, eventDraftProblem, newEventDraft } from './event-draft.ts'
import type { ManualMeal, ManualSport } from '../../core/api/manual-planner.models.ts'
import { formSnapshot, hasChanges } from '../../shared/dialog/dialog-guard.ts'

const sport: ManualSport = {
  id: 'sport-1', dayPlanId: 'day-2', sportTemplateId: 'tpl', name: 'Vélo', sport: 'bike', intensity: 'high',
  durationMinutes: 60, distanceKm: 20, calories: 500, startMinute: 1080, endMinute: 1140,
}

describe('event creation dialog draft', () => {
  it('prefills the clicked day and slot with a 30 minute meal', () => {
    const draft = newEventDraft('day-1', 12 * 60)
    assert.equal(draft.dayId, 'day-1')
    assert.equal(draft.start, '12:00')
    assert.equal(draft.end, '12:30')
    assert.equal(draft.kind, 'meal')
    assert.equal(draft.id, '')
    assert.equal(eventDraftHasSecondaryValues(draft), false)
  })

  it('opens an existing sport with its saved values and secondary options expanded', () => {
    const draft = eventDraftFrom({ key: sport.id, kind: 'sport', value: sport, lane: 0, lanes: 1, top: 0, height: 0 })
    assert.equal(draft.id, 'sport-1')
    assert.equal(draft.start, '18:00')
    assert.equal(draft.distance, 20)
    assert.equal(draft.calories, 500)
    assert.equal(eventDraftHasSecondaryValues(draft), true)
  })

  it('keeps meal snapshot lines and marks legacy composed meals', () => {
    const meal = {
      id: 'meal-1', dayPlanId: 'day-1', name: 'Bol', startMinute: null, endMinute: null, personalPortion: 1.5,
      childrenCount: 2, recipeId: null, composedMealId: 'old', hasSnapshot: false,
      lines: [{ id: 'line-1', foodItemId: 'food-1', quantity: 80, unit: 'g' }],
    } as unknown as ManualMeal
    const draft = eventDraftFrom({ key: meal.id, kind: 'meal', value: meal, lane: 0, lanes: 1, top: 0, height: 0 })
    assert.equal(draft.source, 'legacy')
    assert.equal(draft.start, '')
    assert.deepEqual(draft.lines, [{ foodItemId: 'food-1', quantity: 80, unit: 'g', snapshotLineId: 'line-1' }])
    assert.equal(eventDraftHasSecondaryValues(draft), true)
  })

  it('explains invalid times, portions and missing content without closing the dialog', () => {
    const draft = newEventDraft('day-1', 600)
    draft.end = '09:00'
    assert.match(eventDraftProblem(draft, true), /fin après début/)
    draft.end = '11:00'
    draft.personalPortion = 0
    assert.match(eventDraftProblem(draft, true), /portion/)
    draft.personalPortion = 1
    assert.match(eventDraftProblem(draft, true), /recette/)
    draft.recipeId = 'recipe-1'
    assert.equal(eventDraftProblem(draft, true), '')
  })

  it('requires a sport name even when the sport field is collapsed', () => {
    const draft = newEventDraft('day-1', 600, 'sport')
    draft.name = 'Footing'
    draft.sport = ' '
    assert.match(eventDraftProblem(draft, true), /sport/)
    draft.sport = 'run'
    assert.equal(eventDraftProblem(draft, true), '')
  })

  it('accepts midnight as the end of day and ignores content when it is kept', () => {
    const draft = newEventDraft('day-1', 1380)
    draft.end = '24:00'
    assert.equal(eventDraftProblem(draft, false), '')
  })

  it('is dirty only after the user changes something', () => {
    const draft = newEventDraft('day-1')
    const initial = formSnapshot(draft)
    assert.equal(hasChanges(initial, draft), false)
    draft.recipeId = 'recipe-1'
    assert.equal(hasChanges(initial, draft), true)
  })
})
