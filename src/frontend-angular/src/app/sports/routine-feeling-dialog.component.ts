import { BreakpointObserver } from '@angular/cdk/layout'
import { Component, inject, signal } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom, map } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HANDSET_QUERY } from '@/app/shared/ui/breakpoints'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { StepperComponent } from '@/app/shared/ui/stepper.component'
import { ExerciseImageComponent } from './exercise-image.component'
import { clockTime, doneText, feelingDrafts, type FeelingDraft } from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { RoutineLineDto, RoutineReportDto, RoutineSessionDto, SportSettingsDto } from './sports.models'

export interface FeelingDialogData { report: RoutineReportDto, session: RoutineSessionDto, settings: SportSettingsDto }

/**
 * End of a pass (ADR 0015): effort and discomfort of each exercise, set with − and + from the profile's
 * values, and the pass's comment. « Passer » records nothing.
 */
@Component({
  selector: 'app-routine-feeling-dialog',
  standalone: true,
  imports: [EditorFrameComponent, ExerciseImageComponent, FormsModule, MatFormFieldModule, MatInputModule, StepperComponent],
  template: `
    <app-editor-frame heading="Passage terminé" formId="feeling-form" saveLabel="Enregistrer le ressenti" cancelLabel="Passer"
      [busy]="busy()" [error]="error()">
      <form id="feeling-form" class="lo-form" (ngSubmit)="save()">
        <p class="lo-muted">{{ data.report.routine.name }} · {{ times() }}. Ajustez l’effort et la gêne de chaque exercice ; « Passer » n’enregistre aucun ressenti.</p>
        @for (feeling of feelings; track feeling.exerciseId) {
          @if (lineOf(feeling.exerciseId); as line) {
            <article class="lo-card lo-media-card feeling" [class.is-row]="!handset()">
              <app-exercise-image [exerciseId]="line.exerciseId" [hasImage]="line.hasImage" [size]="handset() ? 72 : 112" [fill]="true" [alt]="'Photo : ' + line.exerciseName" />
              <div class="feeling-body">
                <div class="feeling-name"><strong>{{ line.exerciseName }}</strong><span class="lo-muted">{{ done(line) }}</span></div>
                <div class="feeling-steppers">
                  <app-stepper label="Effort" hint="1 facile · 10 maximal" [subject]="line.exerciseName" [min]="1" [max]="10"
                    [layout]="handset() ? 'column' : 'row'" [(value)]="feeling.effort" />
                  <app-stepper label="Gêne" hint="0 = aucune gêne" [subject]="line.exerciseName" [min]="0" [max]="10"
                    [layout]="handset() ? 'column' : 'row'" [(value)]="feeling.discomfort" />
                </div>
              </div>
            </article>
          }
        }
        <mat-form-field appearance="outline" class="comment">
          <mat-label>Commentaire du passage</mat-label>
          <textarea matInput name="comment" rows="3" maxlength="1000" [(ngModel)]="comment"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .feeling + .feeling { margin-top: 8px; }
    .feeling-body { display: flex; min-width: 0; flex: 1 1 auto; flex-direction: column; gap: 10px; padding: 12px; }
    .feeling-name { display: flex; flex-direction: column; }
    .feeling-steppers { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
    .is-row .feeling-body { flex-direction: row; align-items: center; justify-content: space-between; padding: 12px 16px; }
    .is-row .feeling-steppers { display: flex; flex-direction: column; gap: 8px; }
    .comment { margin-top: 12px; }
  `],
})
export class RoutineFeelingDialogComponent {
  private readonly api = inject(SportsApiService)
  private readonly ref = inject<MatDialogRef<RoutineFeelingDialogComponent, RoutineReportDto>>(MatDialogRef)
  readonly data = inject<FeelingDialogData>(MAT_DIALOG_DATA)
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
  readonly busy = signal(false)
  readonly error = signal('')
  readonly feelings: FeelingDraft[] = feelingDrafts(this.data.session, this.data.settings)
  comment = this.data.session.comment ?? ''

  lineOf(exerciseId: string): RoutineLineDto | undefined { return this.data.report.routine.lines.find(line => line.exerciseId === exerciseId) }
  done(line: RoutineLineDto): string {
    const completion = this.data.session.completions.find(item => item.exerciseId === line.exerciseId)
    return completion ? doneText(line, completion) : ''
  }
  times(): string {
    const session = this.data.session
    return session.finishedAt ? `${clockTime(session.startedAt)} – ${clockTime(session.finishedAt)}` : clockTime(session.startedAt)
  }

  async save(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      const report = await firstValueFrom(this.api.setFeeling(this.data.report.routine.id, this.data.session.id, this.comment.trim() || null, this.feelings))
      this.ref.close(report)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
