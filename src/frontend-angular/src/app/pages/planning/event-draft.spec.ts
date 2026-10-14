import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  emptyPreparation, eventDraftFrom, eventDraftHasSecondaryValues, eventDraftProblem,
  newEventDraft, recipeLineRequest,
} from './event-draft.ts'
import type { ManualMeal, ManualSport } from '../../core/api/manual-planner.models.ts'
import { formSnapshot, hasChanges } from '../../shared/dialog/dialog-guard.ts'

const sport: ManualSport = {
  id: 'sport-1', dayPlanId: 'day-2', sportTemplateId: 'tpl', name: 'Vélo', sport: 'bike', intensity: 'high',
  durationMinutes: 60, distanceKm: 20, calories: 500, startMinute: 1080, endMinute: 1140,
}

const meal: ManualMeal = {
  id: 'meal-1',
  dayPlanId: 'day-1',
  name: 'Préparation · Dhal',
  startMinute: 720,
  endMinute: 780,
  recipeLines: [{
    id: 'line-1', recipeId: null, name: 'Préparation', instructions: null, yieldServings: 2,
    plannedServings: 1, isAdHoc: true, portions: [], ingredients: [{
      id: 'ingredient-1', foodItemId: 'food-1', name: 'Carotte', quantity: 2, unit: 'piece',
      requiredQuantity: 1, referenceUnit: 'piece', calories: null, protein: null, carbs: null, fat: null,
    }],
    nutrition: { calories: null, protein: null, carbs: null, fat: null, isComplete: false, warnings: [] },
  }],
  nutrition: { calories: null, protein: null, carbs: null, fat: null, isComplete: false, warnings: [] },
}

describe('event creation dialog draft', () => {
  it('prefills the clicked day and slot with a 30 minute event', () => {
    const draft = newEventDraft('day-1', 12 * 60)
    assert.equal(draft.dayId, 'day-1')
    assert.equal(draft.start, '12:00')
    assert.equal(draft.end, '12:30')
    assert.equal(draft.kind, 'meal')
    assert.equal(draft.id, '')
    assert.equal(eventDraftHasSecondaryValues(draft), false)
  })

  it('opens a saved sport with its values and remembers its initial template', () => {
    const draft = eventDraftFrom({ key: sport.id, kind: 'sport', value: sport, lane: 0, lanes: 1, top: 0, height: 0 })
    assert.equal(draft.id, 'sport-1')
    assert.equal(draft.start, '18:00')
    assert.equal(draft.distance, 20)
    assert.equal(draft.calories, 500)
    assert.equal(draft.initialTemplateId, 'tpl')
    assert.equal(eventDraftHasSecondaryValues(draft), true)
  })

  it('loads snapshots without requesting replacement when only rescheduling', () => {
    const draft = eventDraftFrom({ key: meal.id, kind: 'meal', value: meal, lane: 0, lanes: 1, top: 0, height: 0 })
    assert.equal(draft.hasSnapshot, true)
    assert.equal(draft.replaceContent, false)
    assert.equal(draft.recipeLines[0]?.ingredients[0]?.foodItemId, 'food-1')
    assert.equal(eventDraftProblem(draft, false), '')
  })

  it('expresses an ad-hoc line in total portions or a per-profile split, never both', () => {
    const line = emptyPreparation()
    line.name = 'Lait de coco et chia'
    line.yieldServings = 2
    line.plannedServings = 4
    line.ingredients = [
      { foodItemId: 'coco', quantity: 125, unit: 'ml' },
      { foodItemId: 'chia', quantity: 25, unit: 'g' },
    ]
    assert.deepEqual(recipeLineRequest(line), {
      recipeId: null, name: 'Lait de coco et chia', instructions: null, yieldServings: 2,
      plannedServings: 4, ingredients: line.ingredients, portions: null,
    })
    line.portions = [{ memberProfileId: 'profile-1', servings: 1.5 }]
    assert.equal(recipeLineRequest(line).plannedServings, null)
    assert.deepEqual(recipeLineRequest(line).portions, line.portions)
  })

  it('requires a recipe or a complete ad-hoc preparation and valid servings', () => {
    const draft = newEventDraft('day-1', 600)
    assert.match(eventDraftProblem(draft, true), /au moins une recette/)
    const line = emptyPreparation()
    line.yieldServings = 0
    draft.recipeLines.push(line)
    assert.match(eventDraftProblem(draft, true), /rendement/)
    line.ingredients[0] = { foodItemId: 'food-1', quantity: 1, unit: 'piece' }
    line.yieldServings = 2
    assert.equal(eventDraftProblem(draft, true), '')
    line.yieldServings = 0
    assert.match(eventDraftProblem(draft, true), /rendement/)
  })

  it('rejects duplicate profiles, invalid portions, and invalid time ranges', () => {
    const draft = newEventDraft('day-1', 600)
    draft.recipeLines.push({
      ...emptyPreparation(), recipeId: 'recipe-1', plannedServings: null,
      portions: [{ memberProfileId: 'profile-1', servings: 1 }, { memberProfileId: 'profile-1', servings: 0.5 }],
    })
    assert.match(eventDraftProblem(draft, true), /apparaître qu’une fois/)
    draft.recipeLines[0]!.portions[1]!.memberProfileId = 'profile-2'
    assert.equal(eventDraftProblem(draft, true), '')
    draft.end = '09:00'
    assert.match(eventDraftProblem(draft, true), /fin après début/)
  })

  it('requires a valid sport template and detail fields', () => {
    const draft = newEventDraft('day-1', 600, 'sport')
    draft.name = 'Footing'
    draft.sport = 'run'
    assert.match(eventDraftProblem(draft, true), /modèle sportif/)
    draft.templateId = 'template-1'
    assert.equal(eventDraftProblem(draft, true), '')
  })

  it('accepts midnight as the end of day and ignores content when it is kept', () => {
    const draft = newEventDraft('day-1', 1380)
    draft.end = '24:00'
    assert.equal(eventDraftProblem(draft, false), '')
  })

  it('loads an existing event ending at 1440 as an explicit end-of-day value', () => {
    const event = { ...sport, endMinute: 1440 }
    const draft = eventDraftFrom({ key: event.id, kind: 'sport', value: event, lane: 0, lanes: 1, top: 0, height: 0 })
    assert.equal(draft.end, '24:00')
    assert.equal(eventDraftProblem(draft, false), '')
  })

  it('is dirty only after the user changes something', () => {
    const draft = newEventDraft('day-1')
    const initial = formSnapshot(draft)
    assert.equal(hasChanges(initial, draft), false)
    draft.recipeLines.push(emptyPreparation())
    assert.equal(hasChanges(initial, draft), true)
  })
})
