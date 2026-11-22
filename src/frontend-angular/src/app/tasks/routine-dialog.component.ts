import { Component, DestroyRef, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { routineDraftOf, routineRequest, WEEKDAY_CHIPS } from './routine-rules'
import { TaskImageComponent } from './task-image.component'
import { initial, PRIORITIES, withTag } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { TaskRoutineDetailDto, TaskRoutineDto } from './tasks.models'

export interface RoutineDialogData { routine: TaskRoutineDto | null, today: string, tags: string[] }
export interface RoutineDialogResult { detail: TaskRoutineDetailDto, message: string }

/**
 * Creates or edits a household routine (ADR 0017): photo, title, recurrence (fixed dates or a delay
 * after the last time), start day, duration, priority, tags and notes. Stopped from here (second click).
 */
@Component({
  selector: 'app-routine-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule, TaskImageComponent],
  template: `
    <app-editor-frame [heading]="routine ? 'Modifier la routine' : 'Nouvelle routine'" formId="routine-form" [saveLabel]="routine ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="routine && routine.state !== 'stopped' ? 'Arrêter la routine' : ''" dangerIcon="block"
      (danger)="stop()">
      <form id="routine-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-field-group">
          <span class="lo-field-label">Photo</span>
          <div class="photo-row">
            <div class="photo">
              @if (preview(); as source) {
                <img [src]="source" alt="Nouvelle photo de la routine" />
              } @else if (routine && routine.hasImage && !removePhoto()) {
                <app-task-image kind="routines" [todoId]="routine.id" [hasImage]="true" [title]="routine.title" [size]="104" [alt]="'Photo : ' + routine.title" />
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
          <input matInput name="title" required maxlength="200" placeholder="Ex. Sortir les poubelles" [(ngModel)]="draft.title" />
        </mat-form-field>

        <div class="lo-field-group">
          <span class="lo-field-label">Récurrence</span>
          <mat-button-toggle-group name="kind" aria-label="Récurrence" [(ngModel)]="draft.kind" hideSingleSelectionIndicator="false">
            <mat-button-toggle value="fixed">Date fixe</mat-button-toggle>
            <mat-button-toggle value="delay">Délai après la dernière fois</mat-button-toggle>
          </mat-button-toggle-group>
          @if (routine) { <span class="lo-help">Une récurrence modifiée vaut pour les échéances suivantes ; l’historique ne change pas.</span> }
        </div>
        @if (draft.kind === 'fixed') {
          <div class="lo-field-group">
            <span class="lo-field-label">Répéter</span>
            <mat-button-toggle-group name="unit" aria-label="Répéter" [(ngModel)]="draft.unit" hideSingleSelectionIndicator="false">
              <mat-button-toggle value="day">Jours</mat-button-toggle>
              <mat-button-toggle value="week">Semaines</mat-button-toggle>
              <mat-button-toggle value="month">Mois</mat-button-toggle>
            </mat-button-toggle-group>
          </div>
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ draft.unit === 'day' ? 'Tous les … jours' : draft.unit === 'week' ? 'Toutes les … semaines' : 'Tous les … mois' }}</mat-label>
              <input matInput type="number" name="every" min="1" max="365" step="1" inputmode="numeric" [(ngModel)]="draft.every" />
            </mat-form-field>
            @if (draft.unit === 'month') {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Jour du mois</mat-label>
                <input matInput type="number" name="monthDay" min="1" max="31" step="1" inputmode="numeric" [(ngModel)]="draft.monthDay" />
                <mat-hint>Le 31 tombe le dernier jour d’un mois plus court.</mat-hint>
              </mat-form-field>
            }
          </div>
          @if (draft.unit === 'week') {
            <div class="lo-chips" role="group" aria-label="Jours de la semaine">
              @for (label of weekdays; track label; let i = $index) {
                <button type="button" class="lo-filter-chip" [class.is-on]="hasDay(i + 1)" [attr.aria-pressed]="hasDay(i + 1)" (click)="flipDay(i + 1)">
                  @if (hasDay(i + 1)) { <mat-icon svgIcon="check" /> }{{ label }}
                </button>
              }
            </div>
          }
        } @else {
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="short">
            <mat-label>Jours après la dernière fois</mat-label>
            <input matInput type="number" name="delayDays" min="1" max="365" step="1" inputmode="numeric" [(ngModel)]="draft.delayDays" />
            <mat-hint>L’échéance suivante part du dernier passage.</mat-hint>
          </mat-form-field>
        }
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>À partir du</mat-label>
            <input matInput type="date" name="startOn" required [(ngModel)]="draft.startOn" />
            <mat-hint>Première échéance possible.</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Durée estimée (min)</mat-label>
            <input matInput type="number" name="duration" min="1" max="1440" step="1" inputmode="numeric" placeholder="Ex. 10" [(ngModel)]="draft.durationMinutes" />
          </mat-form-field>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Priorité</span>
          <mat-button-toggle-group name="priority" aria-label="Priorité" [(ngModel)]="draft.priority" hideSingleSelectionIndicator="false">
            @for (p of priorities; track p.value) { <mat-button-toggle [value]="p.value">{{ p.label }}</mat-button-toggle> }
          </mat-button-toggle-group>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Étiquettes</span>
          <div class="lo-chips" role="group" aria-label="Étiquettes">
            @for (tag of choices(); track tag) {
              <button type="button" class="lo-filter-chip" [class.is-on]="hasTag(tag)" [attr.aria-pressed]="hasTag(tag)" (click)="flipTag(tag)">
                @if (hasTag(tag)) { <mat-icon svgIcon="check" /> }{{ tag }}
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
    .short { max-width: 320px; }
  `],
})
export class RoutineDialogComponent {
  private readonly api = inject(TasksApiService)
  private readonly ref = inject<MatDialogRef<RoutineDialogComponent, RoutineDialogResult>>(MatDialogRef)
  private readonly data = inject<RoutineDialogData>(MAT_DIALOG_DATA)
  readonly routine = this.data.routine
  readonly priorities = PRIORITIES
  readonly weekdays = WEEKDAY_CHIPS
  readonly busy = signal(false)
  readonly error = signal('')
  readonly preview = signal<string | null>(null)
  readonly removePhoto = signal(false)
  readonly draft = routineDraftOf(this.routine, this.data.today)
  readonly choices = signal([...this.data.tags, ...this.draft.tags.filter(t => !this.data.tags.includes(t))])
  newTag = ''
  private file: File | null = null
  private readonly initial = formSnapshot(this.draft)

  constructor() {
    inject(DestroyRef).onDestroy(() => this.releasePreview())
  }

  letter(): string { return initial(this.draft.title) }
  hasPhoto(): boolean { return !!this.preview() || (!!this.routine?.hasImage && !this.removePhoto()) }
  dirty(): boolean { return hasChanges(this.initial, this.draft) || !!this.file || this.removePhoto() }
  hasDay(day: number): boolean { return this.draft.days.includes(day) }
  flipDay(day: number): void { this.draft.days = this.hasDay(day) ? this.draft.days.filter(d => d !== day) : [...this.draft.days, day] }
  hasTag(tag: string): boolean { return this.draft.tags.includes(tag) }
  flipTag(tag: string): void { this.draft.tags = this.hasTag(tag) ? this.draft.tags.filter(t => t !== tag) : [...this.draft.tags, tag] }

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
    this.removePhoto.set(!!this.routine?.hasImage)
  }

  async save(): Promise<void> {
    if (this.busy()) return
    if (this.newTag.trim()) this.addTag()
    const request = routineRequest(this.draft)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      let detail = await firstValueFrom(this.routine ? this.api.updateRoutine(this.routine.id, request) : this.api.createRoutine(request))
      if (this.file) detail = await firstValueFrom(this.api.uploadRoutineImage(detail.routine.id, this.file))
      else if (this.removePhoto() && detail.routine.hasImage) {
        await firstValueFrom(this.api.removeRoutineImage(detail.routine.id))
        detail = { ...detail, routine: { ...detail.routine, hasImage: false } }
      }
      this.ref.close({ detail, message: this.routine ? 'Routine enregistrée' : 'Routine ajoutée' })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async stop(): Promise<void> {
    if (!this.routine || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      const detail = await firstValueFrom(this.api.stopRoutine(this.routine.id))
      this.ref.close({ detail, message: `« ${this.routine.title} » arrêtée ; son historique reste` })
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
