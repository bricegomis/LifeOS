import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { BudgetApiService } from './budget-api.service'
import { parseAmount, SPREADS } from './budget-rules'
import type { BudgetLineDto, BudgetOperationDto } from './budget.models'

export interface OperationDialogData { lines: BudgetLineDto[], today: string }

/** An operation typed by hand (ADR 0020): cash, or a spending missing from the statements. */
@Component({
  selector: 'app-operation-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame heading="Nouvelle opération" formId="operation-form" saveLabel="Ajouter" [busy]="busy()" [dirty]="dirty()" [error]="error()">
      <form id="operation-form" class="lo-form" (ngSubmit)="save()">
        <p class="lo-help">Pour une dépense en espèces ou absente du relevé. Montant négatif pour une dépense, positif pour un revenu ou un remboursement.</p>
        <mat-form-field appearance="outline"><mat-label>Date</mat-label><input matInput type="date" name="date" required [(ngModel)]="draft.date" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Libellé</mat-label><input matInput name="label" required maxlength="300" placeholder="Ex. Marché du samedi" [(ngModel)]="draft.label" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Montant (€)</mat-label><input matInput name="amount" required inputmode="decimal" placeholder="Ex. -18,50" [(ngModel)]="draft.amount" /></mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Ligne</mat-label>
          <mat-select name="line" [(ngModel)]="draft.lineId">
            <mat-option value="">Non classée</mat-option>
            @for (l of lines; track l.id) { <mat-option [value]="l.id">{{ l.category }} · {{ l.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Étaler sur plusieurs mois</span>
          <div class="lo-chips" role="group" aria-label="Étaler sur plusieurs mois">
            @for (s of spreads; track s) {
              <button type="button" class="lo-filter-chip" [class.is-on]="draft.spread === s" [attr.aria-pressed]="draft.spread === s" (click)="draft.spread = s">
                @if (draft.spread === s) { <mat-icon svgIcon="check" /> }{{ s === 1 ? 'Non' : s + ' mois' }}</button>
            }
          </div>
        </div>
      </form>
    </app-editor-frame>
  `,
})
export class OperationDialogComponent {
  private readonly api = inject(BudgetApiService)
  private readonly ref = inject<MatDialogRef<OperationDialogComponent, BudgetOperationDto>>(MatDialogRef)
  private readonly data = inject<OperationDialogData>(MAT_DIALOG_DATA)
  readonly lines = this.data.lines.filter(l => l.active)
  readonly spreads = SPREADS
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = { date: this.data.today, label: '', amount: '', lineId: '', spread: 1 }
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  async save(): Promise<void> {
    if (this.busy()) return
    const amount = parseAmount(this.draft.amount)
    if (amount === null || Number.isNaN(amount) || amount === 0) { this.error.set('Montant invalide (différent de 0).'); return }
    if (!this.draft.label.trim()) { this.error.set('Indique un libellé.'); return }
    this.busy.set(true); this.error.set('')
    try {
      this.ref.close(await firstValueFrom(this.api.addOperation({ date: this.draft.date, label: this.draft.label.trim(), amount,
        lineId: this.draft.lineId || null, spread: this.draft.lineId ? this.draft.spread : 1 })))
    } catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
