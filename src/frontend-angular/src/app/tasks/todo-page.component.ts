import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatCheckboxModule } from '@angular/material/checkbox'
import { MatDialog } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { PostponeDialogComponent, type PostponeDialogData } from './postpone-dialog.component'
import { TaskImageComponent } from './task-image.component'
import { dayLabel, durationLabel, priorityLabel } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { ChecklistItemDto, TodoDto } from './tasks.models'
import { TodoActionsService } from './todo-actions.service'
import { TodoDialogComponent, type TodoDialogData, type TodoDialogResult } from './todo-dialog.component'

/** One todo (ADR 0017): its details, Fait / Reporter / Rouvrir, and its checklist; edits in a window. */
@Component({
  selector: 'app-todo-page',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, RouterLink, TaskImageComponent],
  template: `
    <div class="lo-page todo-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (todo(); as t) {
        <header class="head">
          <a mat-icon-button routerLink="/tasks" aria-label="Retour à Aujourd’hui"><mat-icon svgIcon="arrow_back" /></a>
          <h1 class="lo-bar-title head-title">Todo</h1>
          <button mat-flat-button type="button" class="lo-desktop-only" (click)="edit(t)"><mat-icon svgIcon="edit" />Modifier</button>
          <button mat-icon-button type="button" class="lo-phone-only" (click)="edit(t)" aria-label="Modifier le todo" title="Modifier le todo">
            <mat-icon svgIcon="edit" /></button>
        </header>

        <div class="hero">
          <app-task-image [todoId]="t.id" [hasImage]="t.hasImage" [title]="t.title" [version]="t.updatedAt" [size]="96"
            [alt]="t.hasImage ? 'Photo : ' + t.title : 'Pas de photo'" />
          <div class="hero-text">
            <h2 class="hero-title" [class.is-closed]="t.state !== 'open'">{{ t.title }}</h2>
            <span class="lo-muted">{{ t.dueOn ? 'Échéance : ' + day(t.dueOn).toLocaleLowerCase('fr') : 'Sans échéance' }}</span>
            <span class="lo-tags">
              @if (t.state === 'done') { <span class="lo-tag is-ok">Fait {{ day(t.doneOn!).toLocaleLowerCase('fr') }}</span> }
              @if (t.state === 'abandoned') { <span class="lo-tag">Abandonné</span> }
              @if (t.isLate) { <span class="lo-tag is-off">En retard</span> }
              @if (t.priority === 'high') { <span class="lo-tag is-off">Priorité haute</span> }
              @if (t.priority === 'low') { <span class="lo-tag">Priorité basse</span> }
            </span>
          </div>
        </div>

        <div class="actions">
          @if (t.state === 'open') {
            <button mat-flat-button type="button" [disabled]="busy()" (click)="toggle(t)"><mat-icon svgIcon="check" />Fait</button>
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="postpone(t)"><mat-icon svgIcon="event_repeat" />Reporter</button>
          } @else {
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="reopen(t)"><mat-icon svgIcon="undo" />Rouvrir</button>
          }
        </div>

        <section class="lo-card details" aria-label="Détails">
          <div class="detail"><mat-icon svgIcon="today" /><div><span class="lo-field-label">Échéance</span>
            <span [class.is-late]="t.isLate">{{ t.dueOn ? day(t.dueOn) : 'Aucune' }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="timer" /><div><span class="lo-field-label">Durée estimée</span>
            <span>{{ duration(t.durationMinutes) || 'Non renseignée' }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="flag" /><div><span class="lo-field-label">Priorité</span><span>{{ priority(t.priority) }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="folder" /><div><span class="lo-field-label">Projet</span>
            @if (t.projectId) { <a [routerLink]="['/tasks/project', t.projectId]">{{ t.projectTitle }}</a> } @else { <span>Aucun</span> }</div></div>
          <div class="detail"><mat-icon svgIcon="sell" /><div><span class="lo-field-label">Étiquettes</span><span>{{ t.tags.join(', ') || 'Aucune' }}</span></div></div>
          @if (t.notes) {
            <div class="detail"><mat-icon svgIcon="notes" /><div><span class="lo-field-label">Notes</span><span class="notes">{{ t.notes }}</span></div></div>
          }
        </section>

        <section class="lo-section" aria-labelledby="checklist">
          <div class="lo-section-head"><h2 id="checklist">Checklist</h2>
            @if (t.checklist.length) { <span class="lo-muted">{{ doneCount() }} / {{ t.checklist.length }}</span> }</div>
          <div class="lo-card checklist">
            @for (item of t.checklist; track item.id) {
              <div class="item">
                <mat-checkbox [checked]="item.done" [disabled]="busy()" (change)="tick(t, item, $event.checked)"><span [class.is-done]="item.done">{{ item.label }}</span></mat-checkbox>
                <button mat-icon-button type="button" [disabled]="busy()" (click)="removeItem(t, item)" [attr.aria-label]="'Retirer « ' + item.label + ' »'" title="Retirer">
                  <mat-icon svgIcon="close" /></button>
              </div>
            }
            <form class="item-add" (ngSubmit)="addItem(t)">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Nouvelle sous-tâche</mat-label>
                <input matInput name="label" maxlength="200" [(ngModel)]="newItem" />
              </mat-form-field>
              <button mat-button type="submit" [disabled]="busy() || !newItem.trim()">Ajouter</button>
            </form>
          </div>
          <p class="lo-help">Cocher toutes les sous-tâches ne termine pas le todo.</p>
        </section>
      } @else if (!error()) {
        <p class="lo-muted" role="status">Chargement…</p>
      }
    </div>
  `,
  styles: [`
    .todo-page { max-width: 880px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .hero { display: flex; align-items: center; gap: 16px; }
    .hero-text { display: flex; min-width: 0; flex-direction: column; gap: 6px; }
    .hero-title { margin: 0; font: var(--mat-sys-title-large); font-weight: 600; }
    .hero-title.is-closed { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .details { display: flex; flex-direction: column; }
    .detail { display: flex; align-items: flex-start; gap: 12px; padding: 12px 16px; }
    .detail + .detail { border-top: 1px solid var(--mat-sys-outline-variant); }
    .detail mat-icon { flex: 0 0 auto; color: var(--mat-sys-on-surface-variant); }
    .detail > div { display: flex; min-width: 0; flex-direction: column; }
    .is-late { color: var(--mat-sys-error); }
    .notes { white-space: pre-line; }
    .checklist { display: flex; flex-direction: column; }
    .item { display: flex; align-items: center; justify-content: space-between; gap: 4px; padding: 0 4px 0 8px; }
    .item + .item, .item-add { border-top: 1px solid var(--mat-sys-outline-variant); }
    .item .is-done { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .item-add { display: flex; align-items: center; gap: 8px; padding: 12px 8px 12px 16px; }
    .item-add mat-form-field { flex: 1 1 auto; }
  `],
})
export class TodoPageComponent implements OnInit {
  private readonly api = inject(TasksApiService)
  private readonly actions = inject(TodoActionsService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? ''
  readonly todo = signal<TodoDto | null>(null)
  readonly today = signal(new Date().toISOString().slice(0, 10))
  readonly error = signal('')
  readonly busy = signal(false)
  readonly doneCount = computed(() => this.todo()?.checklist.filter(i => i.done).length ?? 0)
  readonly duration = durationLabel
  readonly priority = priorityLabel
  newItem = ''

  async ngOnInit(): Promise<void> {
    try { this.today.set((await firstValueFrom(this.api.today())).today) } catch { /* local day */ }
    await this.load()
  }

  day(iso: string): string { return dayLabel(iso, this.today()) }

  async toggle(todo: TodoDto): Promise<void> {
    this.busy.set(true)
    const changed = await this.actions.toggle(todo, () => void this.load())
    if (changed) this.todo.set(changed)
    this.busy.set(false)
  }

  reopen(todo: TodoDto): Promise<void> { return this.run(this.api.reopen(todo.id), 'Todo rouvert') }

  async edit(todo: TodoDto): Promise<void> {
    let tags: string[] = []
    try { tags = await firstValueFrom(this.api.tags()) } catch { tags = todo.tags }
    const data: TodoDialogData = { todo, tags }
    const result = await firstValueFrom(openEditor<TodoDialogComponent, TodoDialogData, TodoDialogResult>(this.dialog, TodoDialogComponent, data).afterClosed())
    if (!result) return
    this.todo.set(result.todo)
    this.snack.open(result.message, undefined, { duration: 4000 })
  }

  async postpone(todo: TodoDto): Promise<void> {
    const data: PostponeDialogData = { todo, today: this.today() }
    const result = await firstValueFrom(openEditor<PostponeDialogComponent, PostponeDialogData, TodoDialogResult>(this.dialog, PostponeDialogComponent, data).afterClosed())
    if (!result) return
    this.todo.set(result.todo)
    this.snack.open(result.message, undefined, { duration: 4000 })
  }

  tick(todo: TodoDto, item: ChecklistItemDto, done: boolean): Promise<void> {
    return this.run(this.api.updateChecklistItem(todo.id, item.id, item.label, done))
  }

  removeItem(todo: TodoDto, item: ChecklistItemDto): Promise<void> {
    return this.run(this.api.removeChecklistItem(todo.id, item.id))
  }

  async addItem(todo: TodoDto): Promise<void> {
    const label = this.newItem.trim()
    if (!label) return
    await this.run(this.api.addChecklistItem(todo.id, label))
    if (!this.error()) this.newItem = ''
  }

  private async run(request: Observable<TodoDto>, message = ''): Promise<void> {
    this.busy.set(true)
    try {
      this.todo.set(await firstValueFrom(request))
      if (message) this.snack.open(message, undefined, { duration: 4000 })
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
    } finally {
      this.busy.set(false)
    }
  }

  private async load(): Promise<void> {
    try { this.todo.set(await firstValueFrom(this.api.todo(this.id))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
