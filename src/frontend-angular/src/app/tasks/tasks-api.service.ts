import { inject, Injectable } from '@angular/core'
import { from, switchMap, type Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LIBRARY_IMAGE, reduceImage } from '@/app/shared/image-reduction'
import type { TaskTodayDto, TodoDto, TodoRequest, TodoStateFilter } from './tasks.models'

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
}
