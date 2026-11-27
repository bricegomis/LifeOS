import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { clockTime } from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { RoutineReportDto, RoutineSessionDto } from './sports.models'

export interface CommentDialogData { report: RoutineReportDto, session: RoutineSessionDto }

/** Comment of a pass (ADR 0015), finished or not; an empty text clears it. */
@Component({
  selector: 'app-routine-comment-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule],
  template: `
    <app-editor-frame heading="Commentaire du passage" formId="comment-form" saveLabel="Enregistrer" [busy]="busy()" [error]="error()">
      <form id="comment-form" class="lo-form" (ngSubmit)="save()">
        <p class="lo-muted">{{ data.report.routine.name }} · {{ times() }}. Laissez vide pour effacer le commentaire.</p>
        <mat-form-field appearance="outline">
          <mat-label>Commentaire</mat-label>
          <textarea matInput name="comment" rows="4" maxlength="1000" [(ngModel)]="comment" cdkFocusInitial></textarea>
          <mat-hint align="end">{{ comment.length }} / 1000</mat-hint>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class RoutineCommentDialogComponent {
  private readonly api = inject(SportsApiService)
  private readonly ref = inject<MatDialogRef<RoutineCommentDialogComponent, RoutineReportDto>>(MatDialogRef)
  readonly data = inject<CommentDialogData>(MAT_DIALOG_DATA)
  readonly busy = signal(false)
  readonly error = signal('')
  comment = this.data.session.comment ?? ''

  times(): string {
    const session = this.data.session
    return session.finishedAt ? `${clockTime(session.startedAt)} – ${clockTime(session.finishedAt)}` : clockTime(session.startedAt)
  }

  async save(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      this.ref.close(await firstValueFrom(this.api.setComment(this.data.report.routine.id, this.data.session.id, this.comment.trim() || null)))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
