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
import { DIRECTIONS, SPREADS } from './budget-rules'
import type { BudgetLineDto, BudgetRuleDto, BudgetRuleResultDto } from './budget.models'

export interface RuleDialogData { rule: BudgetRuleDto | null, lines: BudgetLineDto[], lineId: string | null }
export interface RuleDialogResult { result: BudgetRuleResultDto | null, message: string }

/** A classification rule (ADR 0020): "label contains …" towards a line, for expenses, incomes or both, with an optional spread. */
@Component({
  selector: 'app-rule-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame [heading]="rule ? 'Modifier la règle' : 'Nouvelle règle'" formId="rule-form" [busy]="busy()" [dirty]="dirty()" [error]="error()"
      [dangerLabel]="rule ? 'Supprimer la règle' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="rule-form" class="lo-form" (ngSubmit)="save()">
        <p class="lo-help">Appliquée aux prochains imports. Les opérations déjà rangées ne changent pas ; les non classées qui correspondent sont rangées tout de suite.</p>
        <mat-form-field appearance="outline">
          <mat-label>Libellé contient</mat-label>
          <input matInput name="text" required maxlength="100" placeholder="Ex. LECLERC" [(ngModel)]="draft.text" />
          <mat-hint>Sans tenir compte des majuscules ni des accents.</mat-hint>
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Sens</span>
          <div class="lo-chips" role="group" aria-label="Sens">
            @for (d of directions; track d.value) {
              <button type="button" class="lo-filter-chip" [class.is-on]="draft.direction === d.value" [attr.aria-pressed]="draft.direction === d.value" (click)="draft.direction = d.value">
                @if (draft.direction === d.value) { <mat-icon svgIcon="check" /> }{{ d.label }}</button>
            }
          </div>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Ligne</mat-label>
          <mat-select name="line" required [(ngModel)]="draft.lineId">
            @for (l of lines(); track l.id) { <mat-option [value]="l.id">{{ l.category }} · {{ l.name }}</mat-option> }
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
export class RuleDialogComponent {
  private readonly api = inject(BudgetApiService)
  private readonly ref = inject<MatDialogRef<RuleDialogComponent, RuleDialogResult>>(MatDialogRef)
  private readonly data = inject<RuleDialogData>(MAT_DIALOG_DATA)
  readonly rule = this.data.rule
  readonly directions = DIRECTIONS
  readonly spreads = SPREADS
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = { text: this.rule?.text ?? '', direction: this.rule?.direction ?? 'out', lineId: this.rule?.lineId ?? this.data.lineId ?? '', spread: this.rule?.spread ?? 1 }
  private readonly initial = formSnapshot(this.draft)

  lines(): BudgetLineDto[] { return this.data.lines.filter(l => l.active && (this.draft.direction !== 'out' || l.kind !== 'income')) }
  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  async save(): Promise<void> {
    if (this.busy()) return
    if (!this.draft.text.trim() || !this.draft.lineId) { this.error.set('Indique le texte et la ligne.'); return }
    const request = { text: this.draft.text.trim(), lineId: this.draft.lineId, direction: this.draft.direction, spread: this.draft.spread, position: null }
    this.busy.set(true); this.error.set('')
    try {
      const result = await firstValueFrom(this.rule ? this.api.updateRule(this.rule.id, request) : this.api.createRule(request))
      this.ref.close({ result, message: 'Règle enregistrée' + (result.classified ? ` · ${result.classified} opération${result.classified > 1 ? 's' : ''} rangée${result.classified > 1 ? 's' : ''}` : '') })
    } catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }

  async remove(): Promise<void> {
    if (!this.rule) return
    this.busy.set(true); this.error.set('')
    try { await firstValueFrom(this.api.deleteRule(this.rule.id)); this.ref.close({ result: null, message: 'Règle supprimée ; les opérations rangées restent dans leur ligne' }) }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
