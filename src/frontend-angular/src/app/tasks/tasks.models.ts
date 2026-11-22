/** API models of the Tasks module (ADR 0017). Dates are ISO days (yyyy-MM-dd). */
export type TodoPriority = 'high' | 'normal' | 'low'
export type TodoState = 'open' | 'done' | 'abandoned'

export interface ChecklistItemDto { id: string, label: string, done: boolean }

export interface TodoDto {
  id: string
  memberProfileId: string
  title: string
  notes: string | null
  dueOn: string | null
  durationMinutes: number | null
  priority: TodoPriority
  tags: string[]
  state: TodoState
  doneOn: string | null
  isLate: boolean
  hasImage: boolean
  checklist: ChecklistItemDto[]
  updatedAt: string
}

export interface TodoRequest {
  memberProfileId?: string | null
  title: string
  notes: string | null
  dueOn: string | null
  durationMinutes: number | null
  priority: TodoPriority
  tags: string[]
}

export type RecurrenceKind = 'fixed' | 'delay'
export type RecurrenceUnit = 'day' | 'week' | 'month'
export type RoutineState = 'active' | 'paused' | 'stopped'

/** `days`: 1 (Monday) to 7 (Sunday), for a weekly recurrence. */
export interface RecurrenceDto { kind: RecurrenceKind, unit: RecurrenceUnit, every: number, days: number[], monthDay: number, delayDays: number }

export interface TaskRoutineDto {
  id: string
  memberProfileId: string
  title: string
  notes: string | null
  durationMinutes: number | null
  priority: TodoPriority
  tags: string[]
  recurrence: RecurrenceDto
  startOn: string
  state: RoutineState
  /** The open due day; none while paused or stopped. */
  nextDueOn: string | null
  isLate: boolean
  streak: number
  record: number
  lastDoneOn: string | null
  doneToday: boolean
  hasImage: boolean
  updatedAt: string
}

export interface RoutinePassDto { id: string, kind: 'done' | 'skipped', day: string, dueOn: string, onTime: boolean, note: string | null }
export interface TaskRoutineDetailDto { routine: TaskRoutineDto, passes: RoutinePassDto[] }

export interface RecurrenceRequest {
  kind: RecurrenceKind
  unit: RecurrenceUnit | null
  every: number | null
  days: number[] | null
  monthDay: number | null
  delayDays: number | null
}

export interface TaskRoutineRequest {
  memberProfileId?: string | null
  title: string
  notes: string | null
  durationMinutes: number | null
  priority: TodoPriority
  tags: string[]
  recurrence: RecurrenceRequest
  startOn: string | null
}

/** Open todos due within a week (late ones included), undated open todos and those done today; routines due within a week or done today. */
export interface TaskTodayDto { today: string, todos: TodoDto[], routines: TaskRoutineDto[] }

export type RoutineStateFilter = 'active' | 'stopped' | 'all'


export type TodoStateFilter = TodoState | 'all'
