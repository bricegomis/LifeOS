import type { ManualMeal, ManualSport, MealLinePayload } from '../../core/api/manual-planner.models.ts'
import { parseTime, timeLabel } from './calendar.ts'
import type { CalendarEvent } from './calendar.ts'

export interface EventDraft {
  kind: 'meal' | 'sport'
  id: string
  dayId: string
  start: string
  end: string
  personalPortion: number
  children: number
  source: 'recipe' | 'foods' | 'legacy'
  recipeId: string
  lines: MealLinePayload[]
  templateId: string
  name: string
  sport: string
  intensity: string
  distance: number | null
  calories: number
  replaceContent: boolean
  hasSnapshot: boolean
}

export function newEventDraft(dayId: string, minute = 480, kind: 'meal' | 'sport' = 'meal'): EventDraft {
  return {
    kind, id: '', dayId, start: timeLabel(minute), end: timeLabel(minute + 30),
    personalPortion: 1, children: 0, source: 'recipe', recipeId: '', lines: [],
    templateId: '', name: '', sport: 'run', intensity: 'moderate', distance: null, calories: 0,
    replaceContent: false, hasSnapshot: true,
  }
}

export function eventDraftFrom(event: CalendarEvent): EventDraft {
  const value = event.value
  const draft: EventDraft = {
    kind: event.kind, id: value.id, dayId: value.dayPlanId,
    start: value.startMinute === null ? '' : timeLabel(value.startMinute),
    end: value.endMinute === null ? '' : timeLabel(value.endMinute),
    personalPortion: 1, children: 0, source: 'foods', recipeId: '', lines: [],
    templateId: '', name: value.name, sport: 'run', intensity: 'moderate', distance: null, calories: 0,
    replaceContent: false, hasSnapshot: true,
  }
  if (event.kind === 'meal') {
    const meal = value as ManualMeal
    Object.assign(draft, {
      personalPortion: meal.personalPortion, children: meal.childrenCount,
      source: meal.composedMealId ? 'legacy' : meal.recipeId ? 'recipe' : 'foods',
      recipeId: meal.recipeId ?? '', hasSnapshot: meal.hasSnapshot,
      lines: meal.lines.map(l => ({ foodItemId: l.foodItemId, quantity: l.quantity, unit: l.unit, snapshotLineId: l.id })),
    })
  } else {
    const sport = value as ManualSport
    Object.assign(draft, { templateId: sport.sportTemplateId ?? '', sport: sport.sport, intensity: sport.intensity, distance: sport.distanceKm, calories: sport.calories })
  }
  return draft
}

/** Secondary options start expanded only when they already hold a non-default value. */
export function eventDraftHasSecondaryValues(draft: EventDraft): boolean {
  return draft.kind === 'meal'
    ? draft.children !== 0
    : draft.intensity !== 'moderate' || draft.distance !== null
}

/** Returns a user-facing problem, or an empty string when the draft can be sent. */
export function eventDraftProblem(draft: EventDraft, contentEditable: boolean): string {
  const start = parseTime(draft.start)
  const end = draft.end === '24:00' ? 1440 : parseTime(draft.end)
  if (start === null || end === null || end <= start)
    return 'Choisissez un début et une fin valides dans la même journée, fin après début.'
  if (draft.kind === 'meal' && (!Number.isFinite(draft.personalPortion) || draft.personalPortion <= 0
    || !Number.isInteger(draft.children) || draft.children < 0))
    return 'La portion doit être positive et le nombre d’enfants un entier non négatif.'
  if (draft.kind === 'meal' && contentEditable
    && (draft.source === 'recipe' ? !draft.recipeId : !draft.lines.length || draft.lines.some(l => !l.foodItemId || l.quantity <= 0 || !l.unit)))
    return 'Sélectionnez une recette, ou au moins un produit avec quantité et unité.'
  if (draft.kind === 'sport' && (!draft.name.trim() || !draft.sport.trim()))
    return 'Indiquez le nom de la séance et le sport (dans « Plus d’options »).'
  return ''
}
