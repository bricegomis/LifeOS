import { BreakpointObserver } from '@angular/cdk/layout'
import { NgTemplateOutlet } from '@angular/common'
import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom, map } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HANDSET_QUERY } from '@/app/shared/ui/breakpoints'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { RoutineCardComponent } from './routine-card.component'
import { longDay, tagsOf, todaySections } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { TaskRoutineDto, TaskTodayDto, TodoDto } from './tasks.models'
import { TodoActionsService } from './todo-actions.service'
import { TodoCardComponent } from './todo-card.component'
import { TodoDialogComponent, type TodoDialogData, type TodoDialogResult } from './todo-dialog.component'

/**
 * Home of the Tasks module (ADR 0017): late, today, then the next seven days, todos and routines
 * as cards; the undated open todos in a folded section; a filter by tag.
 */
@Component({
  selector: 'app-tasks-today-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, NgTemplateOutlet, RoutineCardComponent, TodoCardComponent],
  template: `
    <div class="lo-page">
      <header class="lo-page-head" [class.is-phone]="handset()">
        <div class="lo-page-title">
          <h1 class="lo-bar-title">Aujourd’hui</h1>
          @if (page(); as p) { <p class="lo-intro">{{ subtitle() }}</p> }
        </div>
        @if (!handset()) {
          <div class="lo-actions"><button mat-flat-button type="button" (click)="add()"><mat-icon svgIcon="add" />Nouveau todo</button></div>
        }
      </header>
      @if (tags().length) {
        <div class="lo-chips" role="group" aria-label="Filtrer par étiquette">
          @for (tag of ['', ...tags()]; track tag) {
            <button type="button" class="lo-filter-chip" [class.is-on]="tag === filter()" [attr.aria-pressed]="tag === filter()" (click)="filter.set(tag)">
              @if (tag === filter()) { <mat-icon svgIcon="check" /> }{{ tag || 'Toutes' }}
            </button>
          }
        </div>
      }
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (page(); as p) {
        @for (section of view().sections; track section.id) {
          <section class="lo-section" [attr.aria-labelledby]="'s-' + section.id">
            <div class="lo-section-head"><h2 [id]="'s-' + section.id" [class.is-late]="section.late">{{ section.title }}</h2>
              @if (section.items.length) { <span class="lo-muted">{{ section.items.length }}</span> }</div>
            @if (section.items.length) {
              <div class="cards">
                @for (item of section.items; track item.key) {
                  <ng-container *ngTemplateOutlet="cardOf; context: { $implicit: item, today: p.today }" />
                }
              </div>
            } @else {
              <p class="lo-muted">Rien de prévu aujourd’hui.</p>
            }
          </section>
        }
        @if (view().undated.length) {
          <section class="lo-section">
            <button type="button" class="fold" [attr.aria-expanded]="showUndated()" (click)="showUndated.set(!showUndated())">
              <mat-icon svgIcon="chevron_right" [class.is-open]="showUndated()" /><span>Sans échéance</span>
              <span class="lo-muted">{{ view().undated.length }}</span>
            </button>
            @if (showUndated()) {
              <div class="cards">
                @for (item of view().undated; track item.key) {
                  <ng-container *ngTemplateOutlet="cardOf; context: { $implicit: item, today: p.today }" />
                }
              </div>
            }
          </section>
        }
      }
      <ng-template #cardOf let-item let-today="today">
        @if (item.kind === 'todo') {
          <app-todo-card [todo]="item.todo" [today]="today" [busy]="pending() === item.todo.id" (tick)="toggle($event)" />
        } @else {
          <app-routine-card [routine]="item.routine" [today]="today" [doneCopy]="item.key.startsWith('d-')" [busy]="pending() === item.routine.id"
            (done)="close($event, 'done')" (skip)="close($event, 'skip')" />
        }
      </ng-template>
      @if (handset()) {
        <button mat-fab type="button" class="lo-fab" (click)="add()" aria-label="Nouveau todo" title="Nouveau todo"><mat-icon svgIcon="add" /></button>
      }
    </div>
  `,
  styles: [`
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    h2.is-late { color: var(--mat-sys-error); }
    .fold { display: flex; align-items: center; gap: 8px; min-height: 40px; padding: 0; border: 0; background: transparent;
      color: var(--mat-sys-on-surface); font: var(--mat-sys-title-medium); font-weight: 600; cursor: pointer; }
    .fold mat-icon { transition: transform 150ms; }
    .fold mat-icon.is-open { transform: rotate(90deg); }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class TasksTodayPageComponent implements OnInit {
  private readonly api = inject(TasksApiService)
  private readonly actions = inject(TodoActionsService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
  readonly page = signal<TaskTodayDto | null>(null)
  readonly error = signal('')
  readonly filter = signal('')
  readonly showUndated = signal(false)
  readonly pending = signal('')
  readonly tags = computed(() => tagsOf([...(this.page()?.todos ?? []), ...(this.page()?.routines ?? [])]))
  readonly view = computed(() => todaySections(this.page()?.todos ?? [], this.page()?.routines ?? [], this.page()?.today ?? '', this.filter()))
  readonly subtitle = computed(() => {
    const p = this.page()
    if (!p) return ''
    const routines = p.routines.filter(r => r.state === 'active')
    const late = p.todos.filter(t => t.isLate).length + routines.filter(r => r.isLate).length
    const now = p.todos.filter(t => t.state === 'open' && t.dueOn === p.today).length + routines.filter(r => r.nextDueOn === p.today).length
    return `${longDay(p.today)} · ${late} en retard · ${now} pour aujourd’hui`
  })

  async ngOnInit(): Promise<void> { await this.load() }

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
    const data: TodoDialogData = { todo: null, dueOn: this.page()?.today, tags: await this.knownTags() }
    const result = await firstValueFrom(openEditor<TodoDialogComponent, TodoDialogData, TodoDialogResult>(this.dialog, TodoDialogComponent, data).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async knownTags(): Promise<string[]> {
    try { return await firstValueFrom(this.api.tags()) } catch { return this.tags() }
  }

  private async load(): Promise<void> {
    try { this.page.set(await firstValueFrom(this.api.today())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
