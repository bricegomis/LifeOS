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
import { ExerciseImageComponent } from './exercise-image.component'
import { emptyExerciseDraft, exerciseRequest, measureLabel } from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { ExerciseDto, ExerciseRequest } from './sports.models'

export interface ExerciseDialogResult { exercise: ExerciseDto, message: string }

/** Creates or edits an exercise (ADR 0015): its photo is chosen in the form and saved with it. */
@Component({
  selector: 'app-exercise-dialog',
  standalone: true,
  imports: [EditorFrameComponent, ExerciseImageComponent, FormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="exercise ? 'Modifier l’exercice' : 'Nouvel exercice'" formId="exercise-form" [saveLabel]="exercise ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="exercise && !exercise.isArchived ? 'Archiver' : ''" (danger)="archive()">
      <form id="exercise-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-field-group">
          <span class="lo-field-label">Photo</span>
          <div class="photo-row">
            <div class="photo">
              @if (preview(); as source) {
                <img [src]="source" alt="Nouvelle photo de l’exercice" />
              } @else if (exercise && !removePhoto()) {
                <app-exercise-image [exerciseId]="exercise.id" [hasImage]="exercise.hasImage" [size]="120" [alt]="'Photo : ' + exercise.name" />
              } @else {
                <span class="photo-empty"><mat-icon svgIcon="image" /><span>Pas de photo</span></span>
              }
              <label class="photo-edit" [attr.title]="hasPhoto() ? 'Changer la photo' : 'Ajouter une photo'">
                <mat-icon svgIcon="edit" /><span class="sr-only">{{ hasPhoto() ? 'Changer la photo' : 'Ajouter une photo' }}</span>
                <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" (change)="pick($event)" />
              </label>
            </div>
            @if (hasPhoto()) {
              <button mat-icon-button type="button" class="photo-delete" (click)="dropPhoto()" aria-label="Supprimer la photo" title="Supprimer la photo">
                <mat-icon svgIcon="delete" />
              </button>
            }
          </div>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" required maxlength="100" placeholder="Ex. Chaise contre le mur" [(ngModel)]="draft.name" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Consigne (position, points d’attention)</mat-label>
          <textarea matInput name="instructions" rows="3" maxlength="2000" [(ngModel)]="draft.instructions"></textarea>
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Mesure</span>
          @if (exercise?.measureLocked) {
            <span>{{ measureLabel(draft.measure) }}</span>
          } @else {
            <mat-button-toggle-group name="measure" aria-label="Mesure de l’exercice" [(ngModel)]="draft.measure" hideSingleSelectionIndicator="false">
              <mat-button-toggle value="hold">Tenue (secondes)</mat-button-toggle>
              <mat-button-toggle value="reps">Répétitions</mat-button-toggle>
            </mat-button-toggle-group>
          }
        </div>
        <mat-form-field appearance="outline" class="load-field">
          <mat-label>Charge habituelle (kg)</mat-label>
          <input matInput type="number" name="load" min="0" max="500" step="0.5" inputmode="decimal" placeholder="Poids du corps" [(ngModel)]="draft.defaultLoadKg" />
          <mat-hint>Facultative : vide ou 0 = au poids du corps. Proposée comme cible dans une routine.</mat-hint>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .photo-row { display: flex; align-items: flex-end; gap: 16px; }
    .photo { position: relative; width: 120px; height: 120px; }
    .photo img, .photo-empty, .photo app-exercise-image { width: 120px; height: 120px; border-radius: 12px; object-fit: cover; }
    .photo-empty {
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px;
      background: var(--mat-sys-surface-container-high); color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small);
    }
    .photo-edit {
      position: absolute; right: -8px; bottom: -8px; display: inline-flex; width: 40px; height: 40px; align-items: center; justify-content: center;
      border-radius: 12px; background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); box-shadow: var(--mat-sys-level2); cursor: pointer;
    }
    .photo-edit:focus-within { outline: 3px solid var(--mat-sys-primary); outline-offset: 2px; }
    .photo-delete { color: var(--mat-sys-error); }
    .load-field { max-width: 360px; }
  `],
})
export class ExerciseDialogComponent {
  private readonly api = inject(SportsApiService)
  private readonly ref = inject<MatDialogRef<ExerciseDialogComponent, ExerciseDialogResult>>(MatDialogRef)
  readonly exercise = inject<ExerciseDto | null>(MAT_DIALOG_DATA)
  readonly measureLabel = measureLabel
  readonly busy = signal(false)
  readonly error = signal('')
  readonly preview = signal<string | null>(null)
  readonly removePhoto = signal(false)
  private file: File | null = null
  readonly draft: ExerciseRequest = this.exercise
    ? { name: this.exercise.name, instructions: this.exercise.instructions ?? '', measure: this.exercise.measure, defaultLoadKg: this.exercise.defaultLoadKg }
    : emptyExerciseDraft()
  private readonly initial = formSnapshot(this.draft)

  constructor() {
    inject(DestroyRef).onDestroy(() => this.releasePreview())
  }

  hasPhoto(): boolean { return !!this.preview() || (!!this.exercise?.hasImage && !this.removePhoto()) }
  dirty(): boolean { return hasChanges(this.initial, this.draft) || !!this.file || this.removePhoto() }

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
    this.removePhoto.set(!!this.exercise?.hasImage)
  }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = exerciseRequest(this.draft)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      let exercise = await firstValueFrom(this.exercise ? this.api.updateExercise(this.exercise.id, request) : this.api.createExercise(request))
      if (this.file) exercise = await firstValueFrom(this.api.uploadExerciseImage(exercise.id, this.file))
      else if (this.removePhoto() && exercise.hasImage) {
        await firstValueFrom(this.api.removeExerciseImage(exercise.id))
        exercise = { ...exercise, hasImage: false }
      }
      this.ref.close({ exercise, message: this.exercise ? 'Exercice enregistré' : 'Exercice ajouté' })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async archive(): Promise<void> {
    if (!this.exercise || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.archiveExercise(this.exercise.id))
      this.ref.close({ exercise: { ...this.exercise, isArchived: true }, message: `« ${this.exercise.name} » archivé` })
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
