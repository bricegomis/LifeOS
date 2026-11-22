import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { addDays, postponeChoices, postponed } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { TodoDto } from './tasks.models'
import type { TodoDialogResult } from './todo-dialog.component'

export interface PostponeDialogData { todo: TodoDto, today: string }

/** Postpones a todo (ADR 0017): a new due day, or none. Nothing slides by itself. */
@Component({
  selector: 'app-postpone-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame heading="Reporter" formId="postpone-form" saveLabel="Reporter" [busy]="busy()" [dirty]="false" [error]="error()">
      <form id="postpone-form" class="lo-form" (ngSubmit)="save()">
        <p class="lo-muted">Un todo en retard le reste tant qu’il n’est ni fait, ni reporté, ni abandonné.</p>
        <div class="lo-chips" role="group" aria-label="Choix rapides">
          @for (choice of choices; track choice.label) {
            <button type="button" class="lo-filter-chip" [class.is-on]="dueOn === choice.date" [attr.aria-pressed]="dueOn === choice.date"
              (click)="dueOn = choice.date">{{ choice.label }}</button>
          }
          <button type="button" class="lo-filter-chip" [class.is-on]="!dueOn" [attr.aria-pressed]="!dueOn" (click)="dueOn = ''">Sans échéance</button>
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Nouvelle échéance</mat-label>
          <input matInput type="date" name="dueOn" [(ngModel)]="dueOn" />
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class PostponeDialogComponent {
  private readonly api = inject(TasksApiService)
  private readonly ref = inject<MatDialogRef<PostponeDialogComponent, TodoDialogResult>>(MatDialogRef)
  private readonly data = inject<PostponeDialogData>(MAT_DIALOG_DATA)
  readonly choices = postponeChoices(this.data.today)
  readonly busy = signal(false)
  readonly error = signal('')
  dueOn = addDays(this.data.todo.dueOn && this.data.todo.dueOn > this.data.today ? this.data.todo.dueOn : this.data.today, 1)

  async save(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      const todo = await firstValueFrom(this.api.updateTodo(this.data.todo.id, postponed(this.data.todo, this.dueOn || null)))
      this.ref.close({ todo, message: todo.dueOn ? 'Todo reporté' : 'Todo sans échéance' })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
