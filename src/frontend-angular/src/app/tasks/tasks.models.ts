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

/** Open todos due within a week (late ones included), undated open todos and those done today. */
export interface TaskTodayDto { today: string, todos: TodoDto[] }

export type TodoStateFilter = TodoState | 'all'
