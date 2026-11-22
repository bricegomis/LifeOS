import { durationLabel, dayLabel } from './task-format.ts'
import type { RecurrenceDto, RecurrenceKind, RecurrenceUnit, TaskRoutineDto, TaskRoutineRequest, TodoPriority } from './tasks.models.ts'

const WEEKDAYS = ['lundi', 'mardi', 'mercredi', 'jeudi', 'vendredi', 'samedi', 'dimanche']
export const WEEKDAY_CHIPS = ['Lun.', 'Mar.', 'Mer.', 'Jeu.', 'Ven.', 'Sam.', 'Dim.']

function list(words: string[]): string {
  return words.length > 1 ? `${words.slice(0, -1).join(', ')} et ${words[words.length - 1]}` : (words[0] ?? '')
}

/** "Chaque lundi et jeudi", "Le 25 de chaque mois", "7 jours après la dernière fois"… */
export function recurrenceLabel(r: RecurrenceDto): string {
  if (r.kind === 'delay') return `${r.delayDays} jour${r.delayDays > 1 ? 's' : ''} après la dernière fois`
  if (r.unit === 'day') return r.every === 1 ? 'Tous les jours' : `Tous les ${r.every} jours`
  if (r.unit === 'week') {
    const days = list(r.days.map(d => WEEKDAYS[d - 1]!))
    return r.every === 1 ? `Chaque ${days}` : `Toutes les ${r.every} semaines, le ${days}`
  }
  const day = r.monthDay === 1 ? '1er' : String(r.monthDay)
  return r.every === 1 ? `Le ${day} de chaque mois` : `Le ${day}, tous les ${r.every} mois`
}

/** Second line of a routine card: due day (when asked), recurrence and duration. */
export function routineMeta(routine: TaskRoutineDto, today: string, showDue: boolean): string {
  const bits: string[] = []
  if (routine.state === 'paused') bits.push('En pause')
  if (routine.state === 'stopped') bits.push('Arrêtée')
  if (showDue && routine.nextDueOn) {
    bits.push(routine.isLate ? `En retard · ${dayLabel(routine.nextDueOn, today)}` : `Prochaine : ${dayLabel(routine.nextDueOn, today).toLocaleLowerCase('fr')}`)
  }
  bits.push(recurrenceLabel(routine.recurrence))
  const duration = durationLabel(routine.durationMinutes)
  if (duration) bits.push(duration)
  return bits.join(' · ')
}

export interface RoutineDraft {
  title: string
  notes: string
  durationMinutes: number | string | null
  priority: TodoPriority
  tags: string[]
  kind: RecurrenceKind
  unit: RecurrenceUnit
  every: number | string
  days: number[]
  monthDay: number | string
  delayDays: number | string
  startOn: string
}

export function routineDraftOf(routine: TaskRoutineDto | null, today: string): RoutineDraft {
  if (!routine) {
    return { title: '', notes: '', durationMinutes: null, priority: 'normal', tags: [], kind: 'fixed', unit: 'week', every: 1, days: [1], monthDay: 1, delayDays: 7, startOn: today }
  }
  const r = routine.recurrence
  return {
    title: routine.title, notes: routine.notes ?? '', durationMinutes: routine.durationMinutes, priority: routine.priority, tags: [...routine.tags],
    kind: r.kind, unit: r.unit, every: r.every, days: r.kind === 'fixed' && r.unit === 'week' ? [...r.days] : [1],
    monthDay: r.monthDay, delayDays: r.delayDays, startOn: routine.startOn,
  }
}

function whole(value: number | string | null, min: number, max: number): number | null {
  if (value === null || value === '') return null
  const n = Number(value)
  return Number.isInteger(n) && n >= min && n <= max ? n : NaN
}

/** The request of a draft, or the message to show. */
export function routineRequest(draft: RoutineDraft): TaskRoutineRequest | string {
  const title = draft.title.trim()
  if (!title) return 'Le titre est obligatoire.'
  const duration = whole(draft.durationMinutes, 1, 1440)
  if (Number.isNaN(duration)) return 'Durée estimée : un nombre entier de minutes, de 1 à 1440.'
  if (!draft.startOn) return 'Choisissez la date de départ.'
  let recurrence: TaskRoutineRequest['recurrence']
  if (draft.kind === 'delay') {
    const delay = whole(draft.delayDays, 1, 365)
    if (delay === null || Number.isNaN(delay)) return 'Délai : de 1 à 365 jours.'
    recurrence = { kind: 'delay', unit: null, every: null, days: null, monthDay: null, delayDays: delay }
  } else {
    const every = whole(draft.every, 1, 365)
    if (every === null || Number.isNaN(every)) return 'Tous les … : de 1 à 365.'
    if (draft.unit === 'week' && !draft.days.length) return 'Choisissez au moins un jour de la semaine.'
    const monthDay = whole(draft.monthDay, 1, 31)
    if (draft.unit === 'month' && (monthDay === null || Number.isNaN(monthDay))) return 'Jour du mois : de 1 à 31.'
    recurrence = {
      kind: 'fixed', unit: draft.unit, every,
      days: draft.unit === 'week' ? [...draft.days].sort((a, b) => a - b) : null,
      monthDay: draft.unit === 'month' ? monthDay : null, delayDays: null,
    }
  }
  return {
    title, notes: draft.notes.trim() || null, durationMinutes: duration, priority: draft.priority, tags: draft.tags,
    recurrence, startOn: draft.startOn,
  }
}
