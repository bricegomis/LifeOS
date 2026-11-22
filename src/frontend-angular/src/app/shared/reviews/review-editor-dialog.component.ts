import { Component, inject } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import type { MemberProfileDto } from '@/app/core/api/api.models'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'

/** The draft owned by `app-household-reviews`: the window only edits it and asks it to save. */
export interface ReviewEditorHost {
  profiles: MemberProfileDto[]
  target: string
  score: number | null
  comment: string
  editingId(): string
  busy(): boolean
  dirty(): boolean
  error(): string
  save(): Promise<void>
}

/** Window of one household review: profile, optional score 0..10 and optional comment. */
@Component({
  selector: 'app-review-editor-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame [heading]="host.editingId() ? 'Modifier l’avis' : 'Ajouter un avis'" formId="review-form"
      [busy]="host.busy()" [dirty]="host.dirty()" [error]="host.error()">
      <form id="review-form" class="lo-form" (ngSubmit)="host.save()">
        <mat-form-field appearance="outline">
          <mat-label>Profil</mat-label>
          <mat-select name="target" required [disabled]="!!host.editingId() || host.busy()" [(ngModel)]="host.target">
            @for (profile of host.profiles; track profile.id) { <mat-option [value]="profile.id">{{ profile.displayName }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="score">
          <mat-label>Note sur 10 (facultative)</mat-label>
          <input matInput name="score" type="number" inputmode="numeric" min="0" max="10" step="1" [disabled]="host.busy()" [(ngModel)]="host.score" />
          <mat-hint>0 est une note ; vide = non noté.</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Commentaire (facultatif)</mat-label>
          <textarea matInput name="comment" maxlength="2000" rows="3" [disabled]="host.busy()" [(ngModel)]="host.comment"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`.score { max-width: 260px; }`],
})
export class ReviewEditorDialogComponent {
  readonly host = inject<ReviewEditorHost>(MAT_DIALOG_DATA)
}
