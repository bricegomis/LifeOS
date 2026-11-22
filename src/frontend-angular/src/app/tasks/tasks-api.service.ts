import { inject, Injectable } from '@angular/core'
import { from, switchMap, type Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LIBRARY_IMAGE, reduceImage } from '@/app/shared/image-reduction'
import type {
  RoutineStateFilter, TaskProjectDetailDto, TaskProjectDto, TaskProjectRequest, TaskRoutineDetailDto, TaskRoutineDto, TaskRoutineRequest, TaskTodayDto, TodoDto, TodoRequest, TodoStateFilter,
} from './tasks.models'

const id = (value: string) => encodeURIComponent(value)

/** Every HTTP call of the Tasks screens. Without a profile, the API uses the household's main profile. */
@Injectable({ providedIn: 'root' })
export class TasksApiService {
  private readonly api = inject(LifeosApiService)

  today(): Observable<TaskTodayDto> { return this.api.get<TaskTodayDto>('/tasks/today') }
  tags(): Observable<string[]> { return this.api.get<string[]>('/tasks/tags') }
  /** Searched in title, notes, tags and sub-tasks, ignoring case and accents. */
  todos(state: TodoStateFilter, q = '', tag = ''): Observable<TodoDto[]> {
    return this.api.get<TodoDto[]>('/tasks/todos', { state, ...(q.trim() ? { q: q.trim() } : {}), ...(tag ? { tag } : {}) })
  }
  todo(todoId: string): Observable<TodoDto> { return this.api.get<TodoDto>(`/tasks/todos/${id(todoId)}`) }
  createTodo(request: TodoRequest): Observable<TodoDto> { return this.api.post<TodoDto>('/tasks/todos', request) }
  /** Also postpones: an update with a new due day. */
  updateTodo(todoId: string, request: TodoRequest): Observable<TodoDto> { return this.api.put<TodoDto>(`/tasks/todos/${id(todoId)}`, request) }
  complete(todoId: string, doneOn: string | null = null): Observable<TodoDto> {
    return this.api.post<TodoDto>(`/tasks/todos/${id(todoId)}/complete`, { doneOn })
  }
  reopen(todoId: string): Observable<TodoDto> { return this.api.post<TodoDto>(`/tasks/todos/${id(todoId)}/reopen`, {}) }
  abandon(todoId: string): Observable<TodoDto> { return this.api.post<TodoDto>(`/tasks/todos/${id(todoId)}/abandon`, {}) }

  addChecklistItem(todoId: string, label: string): Observable<TodoDto> {
    return this.api.post<TodoDto>(`/tasks/todos/${id(todoId)}/checklist`, { label, done: false })
  }
  updateChecklistItem(todoId: string, itemId: string, label: string, done: boolean): Observable<TodoDto> {
    return this.api.put<TodoDto>(`/tasks/todos/${id(todoId)}/checklist/${id(itemId)}`, { label, done })
  }
  removeChecklistItem(todoId: string, itemId: string): Observable<TodoDto> {
    return this.api.delete<TodoDto>(`/tasks/todos/${id(todoId)}/checklist/${id(itemId)}`)
  }

  /** Reduces the picture, then uploads it; returns the updated todo. */
  uploadImage(todoId: string, file: File): Observable<TodoDto> {
    return from(reduceImage(file, LIBRARY_IMAGE)).pipe(switchMap(image => {
      const form = new FormData()
      form.append('image', image)
      return this.api.put<TodoDto>(`/tasks/todos/${id(todoId)}/image`, form)
    }))
  }
  removeImage(todoId: string): Observable<void> { return this.api.delete(`/tasks/todos/${id(todoId)}/image`) }

  /** Active and paused by default; searched like todos. */
  routines(state: RoutineStateFilter = 'active', q = '', tag = ''): Observable<TaskRoutineDto[]> {
    return this.api.get<TaskRoutineDto[]>('/tasks/routines', { state, ...(q.trim() ? { q: q.trim() } : {}), ...(tag ? { tag } : {}) })
  }
  routine(routineId: string): Observable<TaskRoutineDetailDto> { return this.api.get<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}`) }
  createRoutine(request: TaskRoutineRequest): Observable<TaskRoutineDetailDto> { return this.api.post<TaskRoutineDetailDto>('/tasks/routines', request) }
  updateRoutine(routineId: string, request: TaskRoutineRequest): Observable<TaskRoutineDetailDto> {
    return this.api.put<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}`, request)
  }
  /** Closes the open due day; `day` is today when null. */
  routineDone(routineId: string, day: string | null = null, note: string | null = null): Observable<TaskRoutineDetailDto> {
    return this.api.post<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}/done`, { day, note })
  }
  skipRoutine(routineId: string): Observable<TaskRoutineDetailDto> { return this.api.post<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}/skip`, {}) }
  removePass(routineId: string, passId: string): Observable<TaskRoutineDetailDto> {
    return this.api.delete<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}/passes/${id(passId)}`)
  }
  pauseRoutine(routineId: string): Observable<TaskRoutineDetailDto> { return this.api.post<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}/pause`, {}) }
  resumeRoutine(routineId: string): Observable<TaskRoutineDetailDto> { return this.api.post<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}/resume`, {}) }
  stopRoutine(routineId: string): Observable<TaskRoutineDetailDto> { return this.api.post<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}/stop`, {}) }
  uploadRoutineImage(routineId: string, file: File): Observable<TaskRoutineDetailDto> {
    return from(reduceImage(file, LIBRARY_IMAGE)).pipe(switchMap(image => {
      const form = new FormData()
      form.append('image', image)
      return this.api.put<TaskRoutineDetailDto>(`/tasks/routines/${id(routineId)}/image`, form)
    }))
  }
  removeRoutineImage(routineId: string): Observable<void> { return this.api.delete(`/tasks/routines/${id(routineId)}/image`) }

  /** In progress by default, `closed` (done or abandoned) or `all`. */
  projects(state: 'active' | 'closed' | 'all' = 'active'): Observable<TaskProjectDto[]> { return this.api.get<TaskProjectDto[]>('/tasks/projects', { state }) }
  project(projectId: string): Observable<TaskProjectDetailDto> { return this.api.get<TaskProjectDetailDto>(`/tasks/projects/${id(projectId)}`) }
  createProject(request: TaskProjectRequest): Observable<TaskProjectDetailDto> { return this.api.post<TaskProjectDetailDto>('/tasks/projects', request) }
  updateProject(projectId: string, request: TaskProjectRequest): Observable<TaskProjectDetailDto> {
    return this.api.put<TaskProjectDetailDto>(`/tasks/projects/${id(projectId)}`, request)
  }
  /** Finished or abandoned; its open todos stay open. */
  closeProject(projectId: string, how: 'finish' | 'abandon'): Observable<TaskProjectDetailDto> {
    return this.api.post<TaskProjectDetailDto>(`/tasks/projects/${id(projectId)}/${how}`, {})
  }
  reopenProject(projectId: string): Observable<TaskProjectDetailDto> { return this.api.post<TaskProjectDetailDto>(`/tasks/projects/${id(projectId)}/reopen`, {}) }
  uploadProjectImage(projectId: string, file: File): Observable<TaskProjectDetailDto> {
    return from(reduceImage(file, LIBRARY_IMAGE)).pipe(switchMap(image => {
      const form = new FormData()
      form.append('image', image)
      return this.api.put<TaskProjectDetailDto>(`/tasks/projects/${id(projectId)}/image`, form)
    }))
  }
  removeProjectImage(projectId: string): Observable<void> { return this.api.delete(`/tasks/projects/${id(projectId)}/image`) }
}
