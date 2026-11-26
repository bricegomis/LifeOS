import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { challengeAmount } from './today-items'

export interface ChallengeSetData { title: string, sub: string, unit: 'reps' | 'minutes', steps: number[] }

/** Adds one set to a daily challenge from the home page: a proposed amount or a typed one. */
@Component({
  selector: 'app-challenge-set-dialog',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }} — ajouter une série</h2>
    <form mat-dialog-content id="challenge-set" class="body" (ngSubmit)="save()">
      <p class="lo-muted">{{ data.sub }}</p>
      <div class="lo-chips" role="group" aria-label="Valeurs proposées">
        @for (step of data.steps; track step) {
          <button type="button" class="lo-chip" [class.is-on]="amount() === step" [attr.aria-pressed]="amount() === step" (click)="amount.set(step)">
            {{ label(step) }}</button>
        }
      </div>
      <mat-form-field appearance="outline">
        <mat-label>{{ data.unit === 'reps' ? 'Répétitions' : 'Minutes' }}</mat-label>
        <input matInput type="number" min="1" step="1" inputmode="numeric" name="amount" required
          [ngModel]="amount()" (ngModelChange)="amount.set($event)" />
      </mat-form-field>
    </form>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Annuler</button>
      <button mat-flat-button type="submit" form="challenge-set" [disabled]="!valid()">Ajouter</button>
    </mat-dialog-actions>
  `,
  styles: [`.body { display: flex; flex-direction: column; gap: 16px; } .lo-muted { margin-top: 4px; }`],
})
export class ChallengeSetDialogComponent {
  readonly data = inject<ChallengeSetData>(MAT_DIALOG_DATA)
  private readonly ref = inject<MatDialogRef<ChallengeSetDialogComponent, number>>(MatDialogRef)
  readonly amount = signal<number | null>(this.data.steps[0] ?? null)

  label(step: number): string { return challengeAmount(this.data.unit, step) }
  valid(): boolean { const value = Number(this.amount()); return Number.isInteger(value) && value > 0 }
  save(): void { if (this.valid()) this.ref.close(Number(this.amount())) }
}
