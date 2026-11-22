import type { TodoDto, TodoPriority, TodoRequest } from './tasks.models.ts'

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']
const DAYS = ['dim.', 'lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.']
const LONG_DAYS = ['Dimanche', 'Lundi', 'Mardi', 'Mercredi', 'Jeudi', 'Vendredi', 'Samedi']
const LONG_MONTHS = ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre']

export const PRIORITIES: { value: TodoPriority, label: string }[] = [
  { value: 'high', label: 'Haute' },
  { value: 'normal', label: 'Normale' },
  { value: 'low', label: 'Basse' },
]

/** ISO days (yyyy-MM-dd) are read as calendar days, never shifted by a time zone. */
function parts(iso: string): Date { return new Date(`${iso}T12:00:00Z`) }

export function addDays(iso: string, days: number): string {
  const d = parts(iso)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

export function daysBetween(from: string, to: string): number {
  return Math.round((parts(to).getTime() - parts(from).getTime()) / 86_400_000)
}

/** "Aujourd’hui", "Demain", "Hier", else "lun. 23 nov.". */
export function dayLabel(iso: string, today: string): string {
  const gap = daysBetween(today, iso)
  if (gap === 0) return 'Aujourd’hui'
  if (gap === 1) return 'Demain'
  if (gap === -1) return 'Hier'
  const d = parts(iso)
  return `${DAYS[d.getUTCDay()]} ${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}`
}

export function longDay(iso: string): string {
  const d = parts(iso)
  return `${LONG_DAYS[d.getUTCDay()]} ${d.getUTCDate()} ${LONG_MONTHS[d.getUTCMonth()]}`
}

export function durationLabel(minutes: number | null): string {
  if (!minutes) return ''
  if (minutes < 60) return `${minutes} min`
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return rest ? `${hours} h ${String(rest).padStart(2, '0')}` : `${hours} h`
}

export function priorityLabel(priority: TodoPriority): string {
  return PRIORITIES.find(p => p.value === priority)?.label ?? priority
}

/** The card shows the title's first letter, upper case, when the todo has no image (ADR 0017). */
export function initial(title: string): string {
  return (title.trim().charAt(0) || '?').toLocaleUpperCase('fr')
}

/** Second line of a card: due day (when asked), duration and sub-tasks. */
export function todoMeta(todo: TodoDto, today: string, showDue: boolean): string {
  const bits: string[] = []
  if (showDue && todo.state === 'open' && todo.dueOn) bits.push(todo.isLate ? `En retard · ${dayLabel(todo.dueOn, today)}` : dayLabel(todo.dueOn, today))
  if (todo.state === 'done' && todo.doneOn) bits.push(`Fait ${dayLabel(todo.doneOn, today).toLocaleLowerCase('fr')}`)
  if (todo.state === 'abandoned') bits.push('Abandonné')
  const duration = durationLabel(todo.durationMinutes)
  if (duration) bits.push(duration)
  if (todo.checklist.length) bits.push(`${todo.checklist.filter(i => i.done).length}/${todo.checklist.length} sous-tâches`)
  return bits.join(' · ')
}

function priorityRank(todo: TodoDto): number { return todo.priority === 'high' ? 0 : todo.priority === 'normal' ? 1 : 2 }

function byPriority(a: TodoDto, b: TodoDto): number {
  return priorityRank(a) - priorityRank(b) || a.title.localeCompare(b.title, 'fr')
}

export interface TodaySection { id: string, title: string, late: boolean, todos: TodoDto[] }

/**
 * The "Aujourd’hui" page (ADR 0017): late, today (with what was done today), then each of the next
 * seven days that has something, and the undated open todos apart. High priority first in each.
 */
export function todaySections(todos: TodoDto[], today: string, tag = ''): { sections: TodaySection[], undated: TodoDto[] } {
  const shown = todos.filter(t => !tag || t.tags.some(x => x.localeCompare(tag, 'fr', { sensitivity: 'base' }) === 0))
  const late = shown.filter(t => t.state === 'open' && t.dueOn && t.dueOn < today)
    .sort((a, b) => a.dueOn!.localeCompare(b.dueOn!) || byPriority(a, b))
  const now = shown.filter(t => (t.state === 'open' && t.dueOn === today) || (t.state === 'done' && t.doneOn === today))
    .sort((a, b) => (a.state === 'open' ? 0 : 1) - (b.state === 'open' ? 0 : 1) || byPriority(a, b))
  const sections: TodaySection[] = []
  if (late.length) sections.push({ id: 'late', title: 'En retard', late: true, todos: late })
  sections.push({ id: today, title: 'Aujourd’hui', late: false, todos: now })
  for (let gap = 1; gap <= 7; gap++) {
    const day = addDays(today, gap)
    const due = shown.filter(t => t.state === 'open' && t.dueOn === day).sort(byPriority)
    if (due.length) sections.push({ id: day, title: dayLabel(day, today), late: false, todos: due })
  }
  const undated = shown.filter(t => t.state === 'open' && !t.dueOn).sort(byPriority)
  return { sections, undated }
}

/** The tags of a list of todos, without duplicates, for the filter chips. */
export function tagsOf(todos: TodoDto[]): string[] {
  const seen = new Map<string, string>()
  for (const tag of todos.flatMap(t => t.tags)) if (!seen.has(tag.toLocaleLowerCase('fr'))) seen.set(tag.toLocaleLowerCase('fr'), tag)
  return [...seen.values()].sort((a, b) => a.localeCompare(b, 'fr'))
}

/** Quick choices of the "Reporter" window. */
export function postponeChoices(today: string): { label: string, date: string }[] {
  const weekday = parts(today).getUTCDay()
  const toMonday = ((8 - weekday) % 7) || 7
  return [
    { label: 'Demain', date: addDays(today, 1) },
    { label: 'Lundi prochain', date: addDays(today, toMonday) },
    { label: 'Dans une semaine', date: addDays(today, 7) },
  ]
}

export interface TodoDraft {
  title: string
  notes: string
  dueOn: string
  durationMinutes: number | string | null
  priority: TodoPriority
  tags: string[]
}

export function draftOf(todo: TodoDto | null, dueOn = ''): TodoDraft {
  return todo
    ? { title: todo.title, notes: todo.notes ?? '', dueOn: todo.dueOn ?? '', durationMinutes: todo.durationMinutes, priority: todo.priority, tags: [...todo.tags] }
    : { title: '', notes: '', dueOn, durationMinutes: null, priority: 'normal', tags: [] }
}

/** The request of a draft, or the message to show. */
export function todoRequest(draft: TodoDraft): TodoRequest | string {
  const title = draft.title.trim()
  if (!title) return 'Le titre est obligatoire.'
  const raw = draft.durationMinutes === null || draft.durationMinutes === '' ? null : Number(draft.durationMinutes)
  if (raw !== null && (!Number.isInteger(raw) || raw < 1 || raw > 1440)) return 'Durée estimée : un nombre entier de minutes, de 1 à 1440.'
  return {
    title,
    notes: draft.notes.trim() || null,
    dueOn: draft.dueOn || null,
    durationMinutes: raw,
    priority: draft.priority,
    tags: draft.tags,
  }
}

/** Adds a typed tag unless the todo already has it (case ignored). */
export function withTag(tags: string[], tag: string): string[] {
  const clean = tag.trim()
  if (!clean || tags.some(t => t.localeCompare(clean, 'fr', { sensitivity: 'base' }) === 0)) return tags
  return [...tags, clean]
}

/** The request that keeps a todo as it is, with another due day (postpone). */
export function postponed(todo: TodoDto, dueOn: string | null): TodoRequest {
  return { title: todo.title, notes: todo.notes, dueOn, durationMinutes: todo.durationMinutes, priority: todo.priority, tags: todo.tags }
}
