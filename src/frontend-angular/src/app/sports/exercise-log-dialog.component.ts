import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { Router } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { ExerciseImageComponent } from './exercise-image.component'
import { emptyLogDraft, exerciseLogRequest, logValueLabel } from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { ExerciseCompletionDto, ExerciseDto } from './sports.models'

/** The Paris day, "2026-11-27". */
function parisToday(): string { return new Intl.DateTimeFormat('fr-CA', { timeZone: 'Europe/Paris' }).format(new Date()) }

/**
 * Notes an exercise done alone, outside any routine: to try it, or because it is liked. It joins the
 * exercise's progress, not a routine.
 */
@Component({
  selector: 'app-exercise-log-dialog',
  standalone: true,
  imports: [EditorFrameComponent, ExerciseImageComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame heading="Faire l’exercice" formId="exercise-log-form" saveLabel="Noter" [busy]="busy()" [dirty]="dirty()" [error]="error()">
      <form id="exercise-log-form" class="lo-form" (ngSubmit)="save()">
        <div class="head">
          <app-exercise-image [exerciseId]="exercise.id" [hasImage]="exercise.hasImage" [size]="64" [alt]="''" />
          <div><strong>{{ exercise.name }}</strong>
            <p class="lo-help">Hors routine : il compte dans la progression de l’exercice, pas dans une routine.</p></div>
        </div>
        @if (exercise.instructions) { <p class="lo-muted">{{ exercise.instructions }}</p> }
        <mat-form-field appearance="outline">
          <mat-label>Jour</mat-label>
          <input matInput type="date" name="date" required [max]="today" [(ngModel)]="draft.date" />
        </mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Séries</mat-label>
            <input matInput type="number" name="sets" required min="1" max="20" step="1" inputmode="numeric" [(ngModel)]="draft.setsDone" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ valueLabel }}</mat-label>
            <input matInput type="number" name="value" required min="1" max="10000" step="1" inputmode="numeric" [(ngModel)]="draft.value" cdkFocusInitial />
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Charge (kg)</mat-label>
          <input matInput type="number" name="load" min="0" max="500" step="0.5" inputmode="decimal" placeholder="Poids du corps" [(ngModel)]="draft.loadKg" />
          <mat-hint>Vide ou 0 = au poids du corps.</mat-hint>
        </mat-form-field>
        @if (exercise.measure === 'hold') {
          <div class="lo-field-group">
            <div><button mat-stroked-button type="button" (click)="runTimer()"><mat-icon svgIcon="timer" />Faire au chrono</button></div>
            <span class="lo-help">Les séries s’enchaînent avec un compte à rebours et des bips, puis sont notées aujourd’hui.</span>
          </div>
        }
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .head { display: flex; align-items: center; gap: 12px; }
    .head app-exercise-image { flex: none; border-radius: 12px; }
    .head p { margin: 4px 0 0; }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
  `],
})
export class ExerciseLogDialogComponent {
  private readonly api = inject(SportsApiService)
  private readonly ref = inject<MatDialogRef<ExerciseLogDialogComponent, ExerciseCompletionDto>>(MatDialogRef)
  private readonly router = inject(Router)
  readonly exercise = inject<ExerciseDto>(MAT_DIALOG_DATA)
  readonly today = parisToday()
  readonly valueLabel = logValueLabel(this.exercise)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = emptyLogDraft(this.exercise, this.today)
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  /** Opens the chrono page with the sets, seconds and load typed so far. */
  async runTimer(): Promise<void> {
    const queryParams: Record<string, string> = {}
    if (this.draft.setsDone) queryParams['sets'] = String(this.draft.setsDone)
    if (this.draft.value) queryParams['value'] = String(this.draft.value)
    queryParams['load'] = this.draft.loadKg === null ? '' : String(this.draft.loadKg)
    this.ref.close()
    await this.router.navigate(['/sports/exercise', this.exercise.id, 'run'], { queryParams })
  }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = exerciseLogRequest(this.draft, this.today)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      this.ref.close(await firstValueFrom(this.api.logExercise(this.exercise.id, request)))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
