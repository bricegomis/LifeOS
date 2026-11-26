/**
 * What is left to do today, read from the published Tasks, Sport and Health endpoints
 * (roadmap « Accueil Aujourd'hui », ADR 0021). The shapes below keep only the fields this page
 * reads; the modules own the full contracts.
 */

export interface TodayTodo {
  id: string
  title: string
  dueOn: string | null
  durationMinutes: number | null
  state: 'open' | 'done' | 'abandoned'
  doneOn: string | null
  isLate: boolean
  checklist: { done: boolean }[]
  projectTitle: string | null
}

export interface TodayTaskRoutine {
  id: string
  title: string
  state: string
  nextDueOn: string | null
  isLate: boolean
  streak: number
  doneToday: boolean
}

export interface TodayTasks { today: string, todos: TodayTodo[], routines: TodayTaskRoutine[] }

export interface TodaySportRoutine {
  routine: { id: string, name: string, lines: unknown[], isActive: boolean }
  plannedToday: boolean
  completedToday: boolean
  streak: number
  todaySessions: { finished: boolean, exercisesDone: number, exercisesTotal: number }[]
}

export interface TodayChallenge {
  challenge: { id: string, name: string, unit: 'reps' | 'minutes', isActive: boolean }
  todayTotal: number
  todayTarget: number
  reachedToday: boolean
  streak: number
  todayEntries: { id: string, quantity: number, loggedAt: string }[]
}

export type TodaySupplementMoment = 'matin' | 'midi' | 'soir' | 'choix'

export interface TodaySupplement {
  id: string
  name: string
  dose: number
  unit: string
  moment: TodaySupplementMoment
  isStopped: boolean
  status: { active: boolean }
  takenToday: boolean
}

export type TodayItemKind = 'todo' | 'task-routine' | 'sport-routine' | 'challenge' | 'supplement'
export type TodaySectionKey = 'late' | 'tasks' | 'sport' | 'supplements'

export interface TodayItem {
  key: string
  kind: TodayItemKind
  id: string
  title: string
  sub: string
  section: TodaySectionKey
  done: boolean
  /** Page of the element, opened by the sport routines' button. */
  link: string
  moment?: TodaySupplementMoment
  /** Challenges: today's progress (0 to 100) and the proposed amounts for one set. */
  percent?: number
  steps?: number[]
  unit?: 'reps' | 'minutes'
  /** Sport routines: a pass is started and not finished. */
  inProgress?: boolean
}

export interface TodayGroup { label: string | null, items: TodayItem[] }
export interface TodaySection { key: TodaySectionKey, label: string, icon: string, groups: TodayGroup[], count: number }

const DAY_MS = 86_400_000
const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']

function daysBetween(from: string, to: string): number {
  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / DAY_MS)
}

function shortDay(iso: string): string {
  const date = new Date(`${iso}T00:00:00Z`)
  return `${date.getUTCDate()} ${MONTHS[date.getUTCMonth()]}`
}

function lateLabel(dueOn: string, today: string): string {
  const days = daysBetween(dueOn, today)
  return days === 1 ? 'prévu hier' : `en retard de ${days} jours`
}

function durationLabel(minutes: number | null): string {
  if (!minutes) return ''
  return minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h${minutes % 60 ? ` ${String(minutes % 60).padStart(2, '0')}` : ''}`
}

function joined(parts: string[]): string { return parts.filter(Boolean).join(' · ') }

export function taskItems(tasks: TodayTasks): TodayItem[] {
  const today = tasks.today
  const todos = tasks.todos.flatMap((todo): TodayItem[] => {
    const done = todo.state === 'done' && todo.doneOn === today
    const due = todo.state === 'open' && todo.dueOn !== null && todo.dueOn <= today
    if (!done && !due) return []
    const late = !done && todo.dueOn !== null && todo.dueOn < today
    const checked = todo.checklist.filter(item => item.done).length
    const sub = joined([
      'Tâche',
      late ? lateLabel(todo.dueOn!, today) : '',
      durationLabel(todo.durationMinutes),
      todo.checklist.length ? `checklist ${checked}/${todo.checklist.length}` : '',
      todo.projectTitle ?? '',
    ])
    return [{ key: `todo-${todo.id}`, kind: 'todo', id: todo.id, title: todo.title, sub, section: late ? 'late' : 'tasks', done, link: `/tasks/todo/${todo.id}` }]
  })
  const routines = tasks.routines.flatMap((routine): TodayItem[] => {
    const due = routine.state === 'active' && !routine.doneToday && routine.nextDueOn !== null && routine.nextDueOn <= today
    if (!routine.doneToday && !due) return []
    const late = due && routine.nextDueOn! < today
    const sub = joined([
      'Routine du foyer',
      late ? (daysBetween(routine.nextDueOn!, today) === 1 ? 'prévue hier' : `prévue le ${shortDay(routine.nextDueOn!)}`) : '',
      routine.streak > 0 ? `série ${routine.streak}` : '',
    ])
    return [{ key: `task-routine-${routine.id}`, kind: 'task-routine', id: routine.id, title: routine.title, sub, section: late ? 'late' : 'tasks', done: routine.doneToday, link: `/tasks/routine/${routine.id}` }]
  })
  return [...todos, ...routines]
}

export function sportRoutineItems(reports: TodaySportRoutine[]): TodayItem[] {
  return reports.flatMap((report): TodayItem[] => {
    if (!report.routine.isActive || (!report.plannedToday && !report.completedToday)) return []
    const open = report.todaySessions.find(session => !session.finished)
    const exercises = report.routine.lines.length
    const sub = report.completedToday
      ? joined(['Routine sportive', report.streak > 0 ? `série ${report.streak} j` : ''])
      : open
        ? `Passage en cours · ${open.exercisesDone}/${open.exercisesTotal} exercices`
        : joined([`${exercises} exercice${exercises > 1 ? 's' : ''}`, report.streak > 0 ? `série ${report.streak} j` : ''])
    return [{
      key: `sport-routine-${report.routine.id}`, kind: 'sport-routine', id: report.routine.id, title: report.routine.name, sub,
      section: 'sport', done: report.completedToday, link: `/sports/routine/${report.routine.id}`, inProgress: Boolean(open),
    }]
  })
}

export function challengeAmount(unit: 'reps' | 'minutes', value: number): string {
  return unit === 'reps' ? `${value} rép.` : `${value} min`
}

/** Amounts proposed for one set; the same steps as the Sport page. */
export function challengeSteps(unit: 'reps' | 'minutes', target: number): number[] {
  if (unit === 'minutes') return [5, 10, 20]
  if (target >= 50) return [10, 20, 25]
  if (target >= 20) return [5, 10]
  return [1, 5]
}

export function challengeItems(reports: TodayChallenge[]): TodayItem[] {
  return reports.filter(report => report.challenge.isActive).map((report): TodayItem => {
    const { unit } = report.challenge
    const total = unit === 'reps' ? `${report.todayTotal} / ${report.todayTarget} rép.` : `${report.todayTotal} / ${report.todayTarget} min`
    return {
      key: `challenge-${report.challenge.id}`, kind: 'challenge', id: report.challenge.id, title: report.challenge.name,
      sub: joined([`${total} aujourd’hui`, report.streak > 0 ? `série ${report.streak} j` : '']),
      section: 'sport', done: report.reachedToday, link: '/sports',
      percent: Math.min(100, Math.round(report.todayTotal * 100 / Math.max(1, report.todayTarget))),
      steps: challengeSteps(unit, report.todayTarget), unit,
    }
  })
}

const UNITS: Record<string, [string, string]> = {
  gelule: ['gélule', 'gélules'], comprime: ['comprimé', 'comprimés'], goutte: ['goutte', 'gouttes'], dose: ['dose', 'doses'], g: ['g', 'g'], ml: ['ml', 'ml'],
}

function doseText(dose: number, unit: string): string {
  const [one, many] = UNITS[unit] ?? [unit, unit]
  return `${String(dose).replace('.', ',')} ${dose > 1 ? many : one}`
}

export const MOMENT_LABELS: Record<TodaySupplementMoment, string> = { matin: 'Matin', midi: 'Midi', soir: 'Soir', choix: 'Au choix' }

export function supplementItems(supplements: TodaySupplement[]): TodayItem[] {
  return supplements.filter(s => !s.isStopped && s.status.active).map((s): TodayItem => ({
    key: `supplement-${s.id}`, kind: 'supplement', id: s.id, title: s.name, sub: doseText(s.dose, s.unit),
    section: 'supplements', done: s.takenToday, link: `/health/supplement/${s.id}`, moment: s.moment,
  }))
}

const SECTIONS: { key: TodaySectionKey, label: string, icon: string }[] = [
  { key: 'late', label: 'En retard', icon: 'schedule' },
  { key: 'tasks', label: 'Tâches du jour', icon: 'task_alt' },
  { key: 'sport', label: 'Sport', icon: 'fitness_center' },
  { key: 'supplements', label: 'Compléments', icon: 'medication' },
]

/** Open items by section, in the page's order; supplements grouped by moment. Empty sections are left out. */
export function openSections(items: TodayItem[]): TodaySection[] {
  const open = items.filter(item => !item.done)
  return SECTIONS.flatMap((section): TodaySection[] => {
    const list = open.filter(item => item.section === section.key)
    if (!list.length) return []
    const groups = section.key === 'supplements'
      ? (Object.keys(MOMENT_LABELS) as TodaySupplementMoment[])
        .map(moment => ({ label: MOMENT_LABELS[moment], items: list.filter(item => item.moment === moment) }))
        .filter(group => group.items.length)
      : [{ label: null, items: list }]
    return [{ ...section, groups, count: list.length }]
  })
}

export function doneItems(items: TodayItem[]): TodayItem[] { return items.filter(item => item.done) }

export function daySummary(items: TodayItem[]): { open: number, done: number, percent: number, text: string } {
  const done = items.filter(item => item.done).length
  const open = items.length - done
  const text = !items.length ? 'rien de prévu' : !open ? 'tout est fait' : `${open} à faire, ${done} fait${done > 1 ? 's' : ''}`
  return { open, done, percent: items.length ? Math.round(done * 100 / items.length) : 0, text }
}
