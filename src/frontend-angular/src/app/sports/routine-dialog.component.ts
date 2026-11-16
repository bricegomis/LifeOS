import { Component, computed, inject, signal } from '@angular/core'
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
import { ExerciseImageComponent } from './exercise-image.component'
import {
  DAY_LETTERS, DAY_NAMES, draftFromRoutine, emptyRoutineDraft, moveLine, newLine, routineRequest, type RoutineDraft,
} from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { ExerciseDto, RoutineDto } from './sports.models'

export interface RoutineDialogData { routine: RoutineDto | null, exercises: ExerciseDto[] }
export interface RoutineDialogResult { routineId: string, message: string }

/** Creates or edits a routine (ADR 0014): days, jokers and its exercises with their targets. */
@Component({
  selector: 'app-routine-dialog',
  standalone: true,
  imports: [EditorFrameComponent, ExerciseImageComponent, FormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame [heading]="routine ? 'Modifier la routine' : 'Nouvelle routine'" formId="routine-form" [saveLabel]="routine ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="routine?.isActive ? 'Arrêter' : ''" (danger)="stop()">
      <form id="routine-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" required maxlength="100" placeholder="Ex. Tendons" [(ngModel)]="draft.name" />
        </mat-form-field>
        <div class="lo-field-group" role="group" aria-label="Jours de la routine">
          <span class="lo-field-label">Jours</span>
          <div class="lo-chips">
            @for (letter of dayLetters; track $index) {
              <button type="button" class="lo-chip" [class.is-on]="draft.days[$index]" [attr.aria-pressed]="draft.days[$index]"
                [attr.aria-label]="dayNames[$index]" (click)="draft.days[$index] = !draft.days[$index]">
                @if (draft.days[$index]) { <mat-icon svgIcon="check" /> }{{ letter }}</button>
            }
          </div>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Jokers par semaine</span>
          <mat-button-toggle-group name="jokers" aria-label="Jokers par semaine" [(ngModel)]="draft.jokersPerWeek">
            @for (count of [0, 1, 2]; track count) { <mat-button-toggle [value]="count">{{ count }}</mat-button-toggle> }
          </mat-button-toggle-group>
          <p class="lo-help">Un joker couvre un jour prévu manqué sans casser la série.</p>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Exercices et cibles</span>
          @for (line of draft.lines; track line.exerciseId; let index = $index) {
            @if (exerciseOf(line.exerciseId); as exercise) {
              <div class="lo-card lo-media-card line">
                <app-exercise-image [exerciseId]="exercise.id" [hasImage]="exercise.hasImage" [size]="80" [fill]="true" [alt]="'Photo : ' + exercise.name" />
                <div class="line-body">
                  <strong>{{ exercise.name }}</strong>
                  <div class="line-fields">
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="small">
                      <mat-label>Séries</mat-label>
                      <input matInput type="number" inputmode="numeric" min="1" max="20" [name]="'sets-' + line.exerciseId" [(ngModel)]="line.sets" />
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="small">
                      <mat-label>{{ exercise.measure === 'hold' ? 'Secondes' : 'Répétitions' }}</mat-label>
                      <input matInput type="number" inputmode="numeric" min="1" max="10000" [name]="'target-' + line.exerciseId" [(ngModel)]="line.target" />
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="load">
                      <mat-label>Charge (kg)</mat-label>
                      <input matInput type="number" inputmode="decimal" min="0" max="500" step="0.5" placeholder="Poids du corps"
                        [name]="'load-' + line.exerciseId" [(ngModel)]="line.loadKg" />
                    </mat-form-field>
                  </div>
                </div>
                <div class="line-actions">
                  <button mat-icon-button type="button" [disabled]="index === 0" (click)="move(index, -1)" [attr.aria-label]="'Monter ' + exercise.name"><mat-icon svgIcon="arrow_upward" /></button>
                  <button mat-icon-button type="button" [disabled]="index === draft.lines.length - 1" (click)="move(index, 1)" [attr.aria-label]="'Descendre ' + exercise.name"><mat-icon svgIcon="arrow_downward" /></button>
                  <button mat-icon-button type="button" class="remove" (click)="draft.lines.splice(index, 1)" [attr.aria-label]="'Retirer ' + exercise.name"><mat-icon svgIcon="delete" /></button>
                </div>
              </div>
            }
          }
          @if (addable().length) {
            <div class="add-row">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="add-select">
                <mat-label>Exercice à ajouter</mat-label>
                <mat-select name="add" [(ngModel)]="addId">
                  @for (exercise of addable(); track exercise.id) { <mat-option [value]="exercise.id">{{ exercise.name }}</mat-option> }
                </mat-select>
              </mat-form-field>
              <button mat-flat-button type="button" class="tonal" (click)="addLine()"><mat-icon svgIcon="add" />Ajouter</button>
            </div>
          } @else if (!exercises.length) {
            <p class="lo-help">Créez d’abord un exercice, sur la page Exercices.</p>
          }
          <p class="lo-help">Charge vide ou 0 : au poids du corps.@if (routine) { Les changements valent à partir d’aujourd’hui ; les jours passés restent jugés avec la routine d’alors.}</p>
        </div>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .line { min-height: 96px; }
    .line + .line { margin-top: 8px; }
    .line-body { display: flex; min-width: 0; flex: 1 1 auto; flex-direction: column; justify-content: center; gap: 12px; padding: 10px 12px; }
    .line-fields { display: flex; flex-wrap: wrap; gap: 8px; }
    .small { width: 96px; }
    .load { width: 136px; }
    .line-actions { display: flex; flex: 0 0 auto; align-items: center; padding: 4px; }
    .remove { color: var(--mat-sys-error); }
    .add-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding-top: 8px; }
    .add-select { min-width: 0; flex: 1 1 220px; }
    .tonal { --mat-button-filled-container-color: var(--mat-sys-secondary-container); --mat-button-filled-label-text-color: var(--mat-sys-on-secondary-container); }
    @media (max-width: 760px) {
      .line { flex-wrap: wrap; }
      .line-body { flex-basis: calc(100% - 96px); }
      .line-actions { width: 100%; justify-content: flex-end; padding-top: 0; }
    }
  `],
})
export class RoutineDialogComponent {
  private readonly api = inject(SportsApiService)
  private readonly ref = inject<MatDialogRef<RoutineDialogComponent, RoutineDialogResult>>(MatDialogRef)
  private readonly data = inject<RoutineDialogData>(MAT_DIALOG_DATA)
  readonly routine = this.data.routine
  readonly exercises = this.data.exercises
  readonly dayLetters = DAY_LETTERS
  readonly dayNames = DAY_NAMES
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft: RoutineDraft = this.routine ? draftFromRoutine(this.routine) : emptyRoutineDraft()
  private readonly initial = formSnapshot(this.draft)
  private readonly revision = signal(0)
  addId = ''
  readonly addable = computed(() => {
    this.revision()
    return this.exercises.filter(exercise => !exercise.isArchived && !this.draft.lines.some(line => line.exerciseId === exercise.id))
  })

  constructor() { this.addId = this.addable()[0]?.id ?? '' }

  dirty(): boolean { return hasChanges(this.initial, this.draft) }
  exerciseOf(id: string): ExerciseDto | undefined { return this.exercises.find(exercise => exercise.id === id) }

  move(index: number, direction: 1 | -1): void { this.draft.lines = moveLine(this.draft.lines, index, direction) }

  addLine(): void {
    const exercise = this.exercises.find(item => item.id === this.addId)
    if (!exercise) return
    this.draft.lines.push(newLine(exercise))
    this.revision.update(value => value + 1)
    this.addId = this.addable()[0]?.id ?? ''
  }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = routineRequest(this.draft, this.exercises)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      const report = await firstValueFrom(this.routine ? this.api.updateRoutine(this.routine.id, request) : this.api.createRoutine(request))
      this.ref.close({ routineId: report.routine.id, message: this.routine ? 'Routine enregistrée' : 'Routine ajoutée' })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async stop(): Promise<void> {
    if (!this.routine || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.stopRoutine(this.routine.id))
      this.ref.close({ routineId: this.routine.id, message: `« ${this.routine.name} » arrêtée ; son historique est conservé` })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
