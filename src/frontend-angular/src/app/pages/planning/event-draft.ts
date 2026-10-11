import type { ManualMeal, ManualSport, PlannedRecipeLineRequest } from '../../core/api/manual-planner.models.ts'
import { parseTime, timeLabel } from './calendar.ts'
import type { CalendarEvent } from './calendar.ts'

export interface MealLineDraft {
  recipeId: string
  name: string
  instructions: string
  yieldServings: number
  plannedServings: number | null
  portions: Array<{ memberProfileId: string; servings: number }>
  ingredients: Array<{ foodItemId: string; quantity: number; unit: string }>
}

export interface EventDraft {
  kind: 'meal' | 'sport'
  id: string
  dayId: string
  start: string
  end: string
  recipeLines: MealLineDraft[]
  hasSnapshot: boolean
  replaceContent: boolean
  templateId: string
  initialTemplateId: string
  name: string
  sport: string
  intensity: string
  distance: number | null
  calories: number
}

export function emptyPreparation(): MealLineDraft {
  return {
    recipeId: '', name: '', instructions: '', yieldServings: 2, plannedServings: 1,
    portions: [], ingredients: [{ foodItemId: '', quantity: 1, unit: '' }],
  }
}

export function recipeLineRequest(line: MealLineDraft): PlannedRecipeLineRequest {
  return line.recipeId
    ? {
      recipeId: line.recipeId, name: null, instructions: null, yieldServings: null,
      plannedServings: line.portions.length ? null : line.plannedServings,
      ingredients: null, portions: line.portions.length ? line.portions : null,
    }
    : {
      recipeId: null, name: line.name.trim() || null, instructions: line.instructions.trim() || null,
      yieldServings: line.yieldServings, plannedServings: line.portions.length ? null : line.plannedServings,
      ingredients: line.ingredients.map(i => ({ ...i })), portions: line.portions.length ? line.portions : null,
    }
}

export function newEventDraft(dayId: string, minute = 480, kind: 'meal' | 'sport' = 'meal'): EventDraft {
  return {
    kind, id: '', dayId, start: timeLabel(minute), end: timeLabel(minute + 30),
    recipeLines: [], hasSnapshot: false, replaceContent: true, templateId: '', initialTemplateId: '',
    name: '', sport: 'run', intensity: 'moderate', distance: null, calories: 0,
  }
}

export function eventDraftFrom(event: CalendarEvent): EventDraft {
  const value = event.value
  const draft: EventDraft = {
    kind: event.kind, id: value.id, dayId: value.dayPlanId,
    start: value.startMinute === null ? '' : timeLabel(value.startMinute),
    end: value.endMinute === null ? '' : timeLabel(value.endMinute),
    recipeLines: [], hasSnapshot: event.kind === 'meal', replaceContent: false,
    templateId: '', initialTemplateId: '', name: value.name, sport: 'run', intensity: 'moderate', distance: null, calories: 0,
  }
  if (event.kind === 'meal') {
    const meal = value as ManualMeal
    draft.recipeLines = meal.recipeLines.map(line => ({
      recipeId: line.recipeId ?? '', name: line.isAdHoc ? line.name ?? '' : '',
      instructions: line.instructions ?? '', yieldServings: line.yieldServings,
      plannedServings: line.portions.length ? null : line.plannedServings,
      portions: line.portions.map(p => ({ memberProfileId: p.memberProfileId, servings: p.servings })),
      ingredients: line.ingredients.map(i => ({ foodItemId: i.foodItemId, quantity: i.quantity, unit: i.unit })),
    }))
    draft.hasSnapshot = meal.recipeLines.length > 0
  } else {
    const sport = value as ManualSport
    Object.assign(draft, {
      templateId: sport.sportTemplateId ?? '', sport: sport.sport, intensity: sport.intensity,
      distance: sport.distanceKm, calories: sport.calories,
    })
    draft.initialTemplateId = sport.sportTemplateId ?? ''
  }
  return draft
}

export function eventDraftHasSecondaryValues(draft: EventDraft): boolean {
  return draft.kind === 'meal'
    ? draft.recipeLines.some(line => line.portions.length > 0 || line.instructions !== '')
    : draft.intensity !== 'moderate' || draft.distance !== null
}

export function eventDraftProblem(draft: EventDraft, contentEditable: boolean): string {
  const start = parseTime(draft.start)
  const end = draft.end === '24:00' ? 1440 : parseTime(draft.end)
  if (start === null || end === null || end <= start)
    return 'Choisissez un début et une fin valides dans la même journée, fin après début.'
  if (draft.kind === 'meal' && contentEditable) {
    if (draft.recipeLines.length === 0) return 'Ajoutez au moins une recette ou une préparation à ce repas.'
    for (const line of draft.recipeLines) {
      if (line.recipeId) {
        if (line.plannedServings === null && line.portions.length === 0)
          return 'Indiquez les portions de chaque recette ou répartissez-les par personne.'
      } else {
        if (line.name.trim().length > 200 || line.instructions.length > 10000)
          return 'Le nom de la préparation est limité à 200 caractères et les instructions à 10 000.'
        if (!Number.isFinite(line.yieldServings) || line.yieldServings <= 0)
          return 'Indiquez le rendement de chaque préparation ponctuelle.'
        if (line.plannedServings === null && line.portions.length === 0)
          return 'Indiquez les portions de chaque préparation ou répartissez-les par personne.'
        if (line.ingredients.length === 0 || line.ingredients.some(i => !i.foodItemId || !Number.isFinite(i.quantity) || i.quantity <= 0 || !i.unit.trim()))
          return 'Chaque préparation ponctuelle exige au moins un aliment avec une quantité et une unité.'
      }
      if (line.portions.some(p => !p.memberProfileId || !Number.isFinite(p.servings) || p.servings <= 0))
        return 'Les portions par personne doivent être positives.'
      if (new Set(line.portions.map(p => p.memberProfileId)).size !== line.portions.length)
        return 'Chaque personne ne peut apparaître qu’une fois dans la répartition.'
    }
  }
  if (draft.kind === 'sport' && contentEditable && (!draft.templateId || !draft.name.trim() || !draft.sport.trim()))
    return 'Choisissez un modèle sportif actif et vérifiez le nom et le sport.'
  return ''
}
