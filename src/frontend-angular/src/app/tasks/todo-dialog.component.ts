import { Component, DestroyRef, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { TaskImageComponent } from './task-image.component'
import { draftOf, initial, PRIORITIES, todoRequest, withTag } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { TodoDto } from './tasks.models'

/** `projectId`: the project of a new todo (from its project page). */
export interface TodoDialogData { todo: TodoDto | null, dueOn?: string, tags: string[], projectId?: string }
export interface TodoDialogResult { todo: TodoDto, message: string }

/**
 * Creates or edits a todo (ADR 0017): optional photo (else the initial), title, due day, estimated
 * duration, priority, project, free tags and notes. An open todo is abandoned from here (second click).
 */
@Component({
  selector: 'app-todo-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, TaskImageComponent],
  template: `
    <app-editor-frame [heading]="todo ? 'Modifier le todo' : 'Nouveau todo'" formId="todo-form" [saveLabel]="todo ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="todo?.state === 'open' ? 'Abandonner le todo' : ''" dangerIcon="block"
      (danger)="abandon()">
      <form id="todo-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-field-group">
          <span class="lo-field-label">Photo</span>
          <div class="photo-row">
            <div class="photo">
              @if (preview(); as source) {
                <img [src]="source" alt="Nouvelle photo du todo" />
              } @else if (todo && todo.hasImage && !removePhoto()) {
                <app-task-image [todoId]="todo.id" [hasImage]="true" [title]="todo.title" [size]="104" [alt]="'Photo : ' + todo.title" />
              } @else {
                <span class="photo-empty" role="img" [attr.aria-label]="'Pas de photo, initiale ' + letter()">{{ letter() }}</span>
              }
              <label class="photo-edit" [attr.title]="hasPhoto() ? 'Changer la photo' : 'Ajouter une photo'">
                <mat-icon svgIcon="photo_camera" /><span class="sr-only">{{ hasPhoto() ? 'Changer la photo' : 'Ajouter une photo' }}</span>
                <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" (change)="pick($event)" />
              </label>
            </div>
            <div class="photo-side">
              <span class="lo-help">Facultative. Sans photo, la carte affiche la première lettre du titre.</span>
              @if (hasPhoto()) {
                <button mat-button type="button" class="photo-delete" (click)="dropPhoto()"><mat-icon svgIcon="delete" />Retirer la photo</button>
              }
            </div>
          </div>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Titre</mat-label>
          <input matInput name="title" required maxlength="200" placeholder="Ex. Renouveler l’assurance" [(ngModel)]="draft.title" />
        </mat-form-field>
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Échéance</mat-label>
            <input matInput type="date" name="dueOn" [(ngModel)]="draft.dueOn" />
            <mat-hint>Vide : sans échéance.</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Durée estimée (min)</mat-label>
            <input matInput type="number" name="duration" min="1" max="1440" step="1" inputmode="numeric" placeholder="Ex. 30" [(ngModel)]="draft.durationMinutes" />
          </mat-form-field>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Priorité</span>
          <mat-button-toggle-group name="priority" aria-label="Priorité" [(ngModel)]="draft.priority" hideSingleSelectionIndicator="false">
            @for (p of priorities; track p.value) { <mat-button-toggle [value]="p.value">{{ p.label }}</mat-button-toggle> }
          </mat-button-toggle-group>
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="project">
          <mat-label>Projet</mat-label>
          <mat-select name="project" [(ngModel)]="draft.projectId">
            <mat-option value="">Aucun</mat-option>
            @for (project of projects(); track project.id) { <mat-option [value]="project.id">{{ project.title }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Étiquettes</span>
          <div class="lo-chips" role="group" aria-label="Étiquettes">
            @for (tag of choices(); track tag) {
              <button type="button" class="lo-filter-chip" [class.is-on]="has(tag)" [attr.aria-pressed]="has(tag)" (click)="flip(tag)">
                @if (has(tag)) { <mat-icon svgIcon="check" /> }{{ tag }}
              </button>
            }
          </div>
          <div class="tag-add">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Nouvelle étiquette</mat-label>
              <input matInput name="newTag" maxlength="50" placeholder="Ex. maison" [(ngModel)]="newTag" (keydown.enter)="addTag($event)" />
            </mat-form-field>
            <button mat-stroked-button type="button" (click)="addTag()">Ajouter</button>
          </div>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Notes</mat-label>
          <textarea matInput name="notes" rows="3" maxlength="4000" [(ngModel)]="draft.notes"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .photo-row { display: flex; align-items: flex-end; gap: 16px; }
    .photo { position: relative; width: 104px; height: 104px; flex: 0 0 auto; }
    .photo img, .photo-empty { display: flex; width: 104px; height: 104px; border-radius: 16px; object-fit: cover; }
    .photo-empty {
      align-items: center; justify-content: center; font-size: 44px; font-weight: 600;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container);
    }
    .photo-edit {
      position: absolute; right: -8px; bottom: -8px; display: inline-flex; width: 40px; height: 40px; align-items: center; justify-content: center;
      border-radius: 12px; background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); box-shadow: var(--mat-sys-level2); cursor: pointer;
    }
    .photo-edit:focus-within { outline: 3px solid var(--mat-sys-primary); outline-offset: 2px; }
    .photo-side { display: flex; min-width: 0; flex-direction: column; align-items: flex-start; gap: 4px; }
    .photo-delete { color: var(--mat-sys-error); }
    .tag-add { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-top: 8px; }
    .tag-add mat-form-field { flex: 1 1 200px; max-width: 320px; }
    .project { max-width: 360px; }
  `],
})
export class TodoDialogComponent {
  private readonly api = inject(TasksApiService)
  private readonly ref = inject<MatDialogRef<TodoDialogComponent, TodoDialogResult>>(MatDialogRef)
  private readonly data = inject<TodoDialogData>(MAT_DIALOG_DATA)
  readonly todo = this.data.todo
  readonly priorities = PRIORITIES
  readonly busy = signal(false)
  readonly error = signal('')
  readonly preview = signal<string | null>(null)
  readonly removePhoto = signal(false)
  readonly draft = draftOf(this.todo, this.data.dueOn, this.data.projectId)
  /** Projects in progress, plus the todo's current one even when closed. */
  readonly projects = signal<{ id: string, title: string }[]>(this.todo?.projectId ? [{ id: this.todo.projectId, title: this.todo.projectTitle ?? 'Projet' }] : [])
  readonly choices = signal([...this.data.tags, ...this.draft.tags.filter(t => !this.data.tags.includes(t))])
  newTag = ''
  private file: File | null = null
  private readonly initial = formSnapshot(this.draft)

  constructor() {
    inject(DestroyRef).onDestroy(() => this.releasePreview())
    void this.loadProjects()
  }

  private async loadProjects(): Promise<void> {
    try {
      const active = await firstValueFrom(this.api.projects('active'))
      const current = this.projects().filter(p => !active.some(a => a.id === p.id))
      this.projects.set([...active.map(p => ({ id: p.id, title: p.title })), ...current])
    } catch { /* the select keeps the current project */ }
  }

  letter(): string { return initial(this.draft.title) }
  hasPhoto(): boolean { return !!this.preview() || (!!this.todo?.hasImage && !this.removePhoto()) }
  dirty(): boolean { return hasChanges(this.initial, this.draft) || !!this.file || this.removePhoto() }
  has(tag: string): boolean { return this.draft.tags.includes(tag) }

  flip(tag: string): void {
    this.draft.tags = this.has(tag) ? this.draft.tags.filter(t => t !== tag) : [...this.draft.tags, tag]
  }

  addTag(event?: Event): void {
    event?.preventDefault()
    const tags = withTag(this.draft.tags, this.newTag)
    const added = tags.length > this.draft.tags.length ? tags[tags.length - 1]! : null
    this.draft.tags = tags
    if (added && !this.choices().some(c => c.localeCompare(added, 'fr', { sensitivity: 'base' }) === 0)) this.choices.update(c => [...c, added])
    this.newTag = ''
  }

  pick(event: Event): void {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return
    this.releasePreview()
    this.file = file
    this.removePhoto.set(false)
    this.preview.set(URL.createObjectURL(file))
  }

  dropPhoto(): void {
    this.releasePreview()
    this.file = null
    this.removePhoto.set(!!this.todo?.hasImage)
  }

  async save(): Promise<void> {
    if (this.busy()) return
    if (this.newTag.trim()) this.addTag()
    const request = todoRequest(this.draft)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      let todo = await firstValueFrom(this.todo ? this.api.updateTodo(this.todo.id, request) : this.api.createTodo(request))
      if (this.file) todo = await firstValueFrom(this.api.uploadImage(todo.id, this.file))
      else if (this.removePhoto() && todo.hasImage) {
        await firstValueFrom(this.api.removeImage(todo.id))
        todo = { ...todo, hasImage: false }
      }
      this.ref.close({ todo, message: this.todo ? 'Todo enregistré' : 'Todo ajouté' })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async abandon(): Promise<void> {
    if (!this.todo || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      const todo = await firstValueFrom(this.api.abandon(this.todo.id))
      this.ref.close({ todo, message: `« ${this.todo.title} » abandonné` })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  private releasePreview(): void {
    const url = this.preview()
    if (url) URL.revokeObjectURL(url)
    this.preview.set(null)
  }
}
