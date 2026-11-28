import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { addDays, dayLabel } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { TaskRoutineDto } from './tasks.models'
import type { RoutineDialogResult } from './routine-dialog.component'

export interface RoutineDoneDialogData { routine: TaskRoutineDto, today: string }

/** Marks a routine done on a chosen day, with an optional note (ADR 0017). */
@Component({
  selector: 'app-routine-done-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="'Fait : ' + data.routine.title" formId="routine-done-form" saveLabel="Valider" [busy]="busy()"
      [dirty]="!!note.trim()" [error]="error()">
      <form id="routine-done-form" class="lo-form" (ngSubmit)="save()">
        @if (data.routine.nextDueOn) {
          <p class="lo-muted">Fait au plus tard {{ due() }}, la série continue ; après, elle repart de zéro.</p>
        }
        <div class="lo-chips" role="group" aria-label="Quand">
          @for (choice of choices; track choice.day) {
            <button type="button" class="lo-filter-chip" [class.is-on]="day === choice.day" [attr.aria-pressed]="day === choice.day" (click)="day = choice.day">
              @if (day === choice.day) { <mat-icon svgIcon="check" /> }{{ choice.label }}
            </button>
          }
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Jour</mat-label>
          <input matInput type="date" name="day" required [max]="data.today" [(ngModel)]="day" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Note (facultative)</mat-label>
          <textarea matInput name="note" rows="2" maxlength="500" placeholder="Ex. draps d’hiver sortis" [(ngModel)]="note"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class RoutineDoneDialogComponent {
  private readonly api = inject(TasksApiService)
  private readonly ref = inject<MatDialogRef<RoutineDoneDialogComponent, RoutineDialogResult>>(MatDialogRef)
  readonly data = inject<RoutineDoneDialogData>(MAT_DIALOG_DATA)
  readonly choices = [0, 1, 2].map(gap => ({ day: addDays(this.data.today, -gap), label: ['Aujourd’hui', 'Hier', 'Avant-hier'][gap]! }))
  readonly busy = signal(false)
  readonly error = signal('')
  day = this.data.today
  note = ''

  due(): string {
    const due = this.data.routine.nextDueOn!
    const label = dayLabel(due, this.data.today)
    return /^[A-Z]/.test(label) && !label.includes(' ') ? label.toLocaleLowerCase('fr') : `le ${label}`
  }

  async save(): Promise<void> {
    if (this.busy()) return
    if (!this.day) { this.error.set('Choisis le jour.'); return }
    this.busy.set(true); this.error.set('')
    try {
      const detail = await firstValueFrom(this.api.routineDone(this.data.routine.id, this.day, this.note.trim() || null))
      this.ref.close({ detail, message: `« ${this.data.routine.title} » fait` })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
