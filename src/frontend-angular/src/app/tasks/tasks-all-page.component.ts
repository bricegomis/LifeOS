import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent } from '@/app/shared/ui/list-page.component'
import { TasksApiService } from './tasks-api.service'
import { RoutineCardComponent } from './routine-card.component'
import { routineItem, todoItem, type TaskItem } from './task-format'
import type { RoutineStateFilter, TaskRoutineDto, TodoDto, TodoStateFilter } from './tasks.models'
import { TodoActionsService } from './todo-actions.service'
import { TodoCardComponent } from './todo-card.component'
import { TodoDialogComponent, type TodoDialogData, type TodoDialogResult } from './todo-dialog.component'

const STATES: { value: TodoStateFilter, label: string }[] = [
  { value: 'open', label: 'Ouvertes' },
  { value: 'done', label: 'Faites' },
  { value: 'abandoned', label: 'Abandonnées ou arrêtées' },
  { value: 'all', label: 'Toutes' },
]

type KindFilter = 'all' | 'todo' | 'routine'
const KINDS: { value: KindFilter, label: string }[] = [
  { value: 'all', label: 'Todos et routines' },
  { value: 'todo', label: 'Todos' },
  { value: 'routine', label: 'Routines' },
]

/** Routines matching a todo state filter: open → active and paused, abandoned → stopped, done → none. */
const ROUTINE_STATE: Record<TodoStateFilter, RoutineStateFilter | null> = { open: 'active', done: null, abandoned: 'stopped', all: 'all' }

/**
 * Every todo and routine in one list (ADR 0017): text search in title, notes, tags and sub-tasks (case
 * and accents ignored), filters by type, state and tag; by due day, undated next, closed last.
 */
@Component({
  selector: 'app-tasks-all-page',
  standalone: true,
  imports: [ListPageComponent, MatIconModule, RoutineCardComponent, TodoCardComponent],
  template: `
    <app-list-page heading="Toutes les tâches" addLabel="Nouveau todo" [(query)]="query" (add)="add()">
      <div class="lo-chips" role="group" aria-label="Type">
        @for (k of kinds; track k.value) {
          <button type="button" class="lo-filter-chip" [class.is-on]="kind() === k.value" [attr.aria-pressed]="kind() === k.value" (click)="kind.set(k.value)">
            @if (kind() === k.value) { <mat-icon svgIcon="check" /> }{{ k.label }}
          </button>
        }
      </div>
      <div class="lo-chips" role="group" aria-label="État">
        @for (s of states; track s.value) {
          <button type="button" class="lo-filter-chip" [class.is-on]="state() === s.value" [attr.aria-pressed]="state() === s.value" (click)="state.set(s.value)">
            @if (state() === s.value) { <mat-icon svgIcon="check" /> }{{ s.label }}
          </button>
        }
      </div>
      @if (tags().length) {
        <div class="lo-chips" role="group" aria-label="Étiquette">
          @for (tag of ['', ...tags()]; track tag) {
            <button type="button" class="lo-filter-chip" [class.is-on]="tag === tag$()" [attr.aria-pressed]="tag === tag$()" (click)="tag$.set(tag)">
              @if (tag === tag$()) { <mat-icon svgIcon="check" /> }{{ tag || 'Toutes les étiquettes' }}
            </button>
          }
        </div>
      }
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        <p class="lo-muted" role="status">{{ items().length }} résultat{{ items().length > 1 ? 's' : '' }}</p>
        @if (items().length) {
          <div class="cards">
            @for (item of items(); track item.key) {
              @if (item.kind === 'todo') {
                <app-todo-card [todo]="item.todo" [today]="today()" [showDue]="true" [busy]="pending() === item.todo.id" (tick)="toggle($event)" />
              } @else {
                <app-routine-card [routine]="item.routine" [today]="today()" [showDue]="true" [busy]="pending() === item.routine.id"
                  (done)="close($event, 'done')" (skip)="close($event, 'skip')" />
              }
            }
          </div>
        } @else {
          <p class="lo-empty">{{ query().trim() ? 'Aucune tâche ne correspond à « ' + query().trim() + ' ».' : 'Aucune tâche avec ces filtres.' }}</p>
        }
      }
    </app-list-page>
  `,
  styles: [`
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class TasksAllPageComponent {
  private readonly api = inject(TasksApiService)
  private readonly actions = inject(TodoActionsService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly states = STATES
  readonly kinds = KINDS
  readonly kind = signal<KindFilter>('all')
  readonly query = signal('')
  readonly state = signal<TodoStateFilter>('open')
  readonly tag$ = signal('')
  readonly tags = signal<string[]>([])
  readonly items = signal<TaskItem[]>([])
  readonly today = signal(new Date().toISOString().slice(0, 10))
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly pending = signal('')
  private timer: ReturnType<typeof setTimeout> | undefined
  private loadId = 0

  constructor() {
    effect(() => {
      this.query(); this.state(); this.tag$(); this.kind()
      untracked(() => {
        clearTimeout(this.timer)
        this.timer = setTimeout(() => void this.load(), 250)
      })
    })
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer))
    void this.loadMeta()
  }

  async toggle(todo: TodoDto): Promise<void> {
    this.pending.set(todo.id)
    await this.actions.toggle(todo, () => void this.load())
    this.pending.set('')
    await this.load()
  }

  async close(routine: TaskRoutineDto, how: 'done' | 'skip'): Promise<void> {
    this.pending.set(routine.id)
    await this.actions.closeRoutine(routine, how, () => void this.load())
    this.pending.set('')
    await this.load()
  }

  async add(): Promise<void> {
    const data: TodoDialogData = { todo: null, tags: this.tags() }
    const result = await firstValueFrom(openEditor<TodoDialogComponent, TodoDialogData, TodoDialogResult>(this.dialog, TodoDialogComponent, data).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await Promise.all([this.load(), this.loadMeta()])
  }

  private async loadMeta(): Promise<void> {
    try {
      const [tags, page] = await Promise.all([firstValueFrom(this.api.tags()), firstValueFrom(this.api.today())])
      this.tags.set(tags)
      this.today.set(page.today)
    } catch { /* the list shows its own error */ }
  }

  private async load(): Promise<void> {
    const current = ++this.loadId
    try {
      const routineState = ROUTINE_STATE[this.state()]
      const [todos, routines] = await Promise.all([
        this.kind() === 'routine' ? Promise.resolve([] as TodoDto[]) : firstValueFrom(this.api.todos(this.state(), this.query(), this.tag$())),
        this.kind() === 'todo' || !routineState ? Promise.resolve([] as TaskRoutineDto[]) : firstValueFrom(this.api.routines(routineState, this.query(), this.tag$())),
      ])
      if (current !== this.loadId) return
      this.items.set(merged(todos, routines)); this.error.set('')
    } catch (error) {
      if (current === this.loadId) this.error.set(apiErrorMessage(error))
    } finally {
      if (current === this.loadId) this.loaded.set(true)
    }
  }
}

/** Open first by due day (undated after), then paused, then closed; high priority first. */
function merged(todos: TodoDto[], routines: TaskRoutineDto[]): TaskItem[] {
  const rank = (item: TaskItem): [number, string, number, string] => {
    const closed = item.kind === 'todo' ? item.todo.state !== 'open' : item.routine.state === 'stopped'
    const paused = item.kind === 'routine' && item.routine.state === 'paused'
    const due = item.kind === 'todo' ? item.todo.dueOn : item.routine.nextDueOn
    const source = item.kind === 'todo' ? item.todo : item.routine
    return [closed ? 2 : paused ? 1 : 0, due ?? '9999-12-31', source.priority === 'high' ? 0 : source.priority === 'normal' ? 1 : 2, source.title]
  }
  return [...todos.map(todoItem), ...routines.map(routineItem)].sort((a, b) => {
    const [x, y] = [rank(a), rank(b)]
    return x[0] - y[0] || x[1].localeCompare(y[1]) || x[2] - y[2] || x[3].localeCompare(y[3], 'fr')
  })
}
