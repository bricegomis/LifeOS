import type { TaskProjectDto, TaskRoutineDto, TodoDto, TodoPriority, TodoRequest } from './tasks.models.ts'

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

/** Second line of a card: due day (when asked), duration, project (unless on its page) and sub-tasks. */
export function todoMeta(todo: TodoDto, today: string, showDue: boolean, showProject = true): string {
  const bits: string[] = []
  if (showDue && todo.state === 'open' && todo.dueOn) bits.push(todo.isLate ? `En retard · ${dayLabel(todo.dueOn, today)}` : dayLabel(todo.dueOn, today))
  if (todo.state === 'done' && todo.doneOn) bits.push(`Fait ${dayLabel(todo.doneOn, today).toLocaleLowerCase('fr')}`)
  if (todo.state === 'abandoned') bits.push('Abandonné')
  const duration = durationLabel(todo.durationMinutes)
  if (duration) bits.push(duration)
  if (todo.projectTitle && showProject) bits.push(todo.projectTitle)
  if (todo.checklist.length) bits.push(`${todo.checklist.filter(i => i.done).length}/${todo.checklist.length} sous-tâches`)
  return bits.join(' · ')
}

/** A todo or a routine, as one card of a list. */
export type TaskItem = { kind: 'todo', key: string, todo: TodoDto } | { kind: 'routine', key: string, routine: TaskRoutineDto }

export function todoItem(todo: TodoDto): TaskItem { return { kind: 'todo', key: `t-${todo.id}`, todo } }
export function routineItem(routine: TaskRoutineDto): TaskItem { return { kind: 'routine', key: `r-${routine.id}`, routine } }

function itemOf(item: TaskItem): { title: string, priority: TodoPriority, tags: string[] } {
  return item.kind === 'todo' ? item.todo : item.routine
}

function byPriority(a: TaskItem, b: TaskItem): number {
  const rank = (item: TaskItem) => { const p = itemOf(item).priority; return p === 'high' ? 0 : p === 'normal' ? 1 : 2 }
  return rank(a) - rank(b) || itemOf(a).title.localeCompare(itemOf(b).title, 'fr')
}

function hasTag(item: TaskItem, tag: string): boolean {
  return !tag || itemOf(item).tags.some(x => x.localeCompare(tag, 'fr', { sensitivity: 'base' }) === 0)
}

/** The day a card belongs to on the today page; null when undated. */
function dayOf(item: TaskItem, today: string): string | null {
  if (item.kind === 'todo') return item.todo.state === 'done' ? today : item.todo.dueOn
  return item.routine.nextDueOn
}

export interface TodaySection { id: string, title: string, late: boolean, items: TaskItem[] }

/**
 * The "Aujourd’hui" page (ADR 0017): late, today (with what was done today), then each of the next
 * seven days that has something, and the undated open todos apart; todos and routines mixed, high
 * priority first in each. A routine done today shows struck through today and again on its next due day.
 */
export function todaySections(todos: TodoDto[], routines: TaskRoutineDto[], today: string, tag = ''): { sections: TodaySection[], undated: TaskItem[] } {
  const items = [
    ...todos.filter(t => t.state === 'open' || (t.state === 'done' && t.doneOn === today)).map(todoItem),
    ...routines.filter(r => r.state === 'active' && r.nextDueOn).map(routineItem),
  ].filter(item => hasTag(item, tag))
  const doneRoutines = routines.filter(r => r.doneToday && hasTag(routineItem(r), tag)).map(r => ({ ...routineItem(r), key: `d-${r.id}` }))
  const late = items.filter(i => { const d = dayOf(i, today); return d !== null && d < today })
    .sort((a, b) => dayOf(a, today)!.localeCompare(dayOf(b, today)!) || byPriority(a, b))
  const closed = (i: TaskItem) => (i.kind === 'todo' ? i.todo.state === 'done' : i.key.startsWith('d-')) ? 1 : 0
  const now = [...items.filter(i => dayOf(i, today) === today), ...doneRoutines].sort((a, b) => closed(a) - closed(b) || byPriority(a, b))
  const sections: TodaySection[] = []
  if (late.length) sections.push({ id: 'late', title: 'En retard', late: true, items: late })
  sections.push({ id: today, title: 'Aujourd’hui', late: false, items: now })
  for (let gap = 1; gap <= 7; gap++) {
    const day = addDays(today, gap)
    const due = items.filter(i => dayOf(i, today) === day).sort(byPriority)
    if (due.length) sections.push({ id: day, title: dayLabel(day, today), late: false, items: due })
  }
  const undated = items.filter(i => dayOf(i, today) === null).sort(byPriority)
  return { sections, undated }
}

/** The tags of todos and routines, without duplicates, for the filter chips. */
export function tagsOf(items: { tags: string[] }[]): string[] {
  const seen = new Map<string, string>()
  for (const tag of items.flatMap(t => t.tags)) if (!seen.has(tag.toLocaleLowerCase('fr'))) seen.set(tag.toLocaleLowerCase('fr'), tag)
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
  projectId: string
}

export function draftOf(todo: TodoDto | null, dueOn = '', projectId = ''): TodoDraft {
  return todo
    ? { title: todo.title, notes: todo.notes ?? '', dueOn: todo.dueOn ?? '', durationMinutes: todo.durationMinutes, priority: todo.priority, tags: [...todo.tags], projectId: todo.projectId ?? '' }
    : { title: '', notes: '', dueOn, durationMinutes: null, priority: 'normal', tags: [], projectId }
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
    projectId: draft.projectId || null,
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
  return { title: todo.title, notes: todo.notes, dueOn, durationMinutes: todo.durationMinutes, priority: todo.priority, tags: todo.tags, projectId: todo.projectId }
}

/** "3 / 7 faits" and the bar width of a project. */
export function projectProgress(done: number, total: number): { label: string, percent: number } {
  return { label: `${done} / ${total} fait${done > 1 ? 's' : ''}`, percent: total ? Math.round(100 * done / total) : 0 }
}

export interface ProjectOption { id: string, label: string, depth: number }

/**
 * Projects in tree order (each followed by its sub-projects, by name), labelled with their path
 * ("Terrain › Chalet"). A project whose parent is not in the list starts its own branch.
 */
export function projectTree(projects: Pick<TaskProjectDto, 'id' | 'title' | 'parentId'>[]): ProjectOption[] {
  const ids = new Set(projects.map(p => p.id))
  const byName = (a: { title: string }, b: { title: string }) => a.title.localeCompare(b.title, 'fr', { sensitivity: 'base' })
  const children = (parent: string | null) => projects
    .filter(p => parent === null ? !p.parentId || !ids.has(p.parentId) : p.parentId === parent).sort(byName)
  const out: ProjectOption[] = []
  const visit = (parent: string | null, path: string, depth: number) => {
    for (const p of children(parent)) {
      if (out.some(o => o.id === p.id)) continue
      const label = path ? `${path} › ${p.title}` : p.title
      out.push({ id: p.id, label, depth })
      visit(p.id, label, depth + 1)
    }
  }
  visit(null, '', 0)
  return out
}

/** The ids of a project and of all its sub-projects, from a flat list. */
export function subTreeIds(projects: Pick<TaskProjectDto, 'id' | 'parentId'>[], id: string): Set<string> {
  const ids = new Set([id])
  let grew = true
  while (grew) {
    grew = false
    for (const p of projects) if (p.parentId && ids.has(p.parentId) && !ids.has(p.id)) { ids.add(p.id); grew = true }
  }
  return ids
}
