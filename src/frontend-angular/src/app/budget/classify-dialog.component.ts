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
import { dayLabel, euro, needsJudgement, ruleTextOf, SPREADS } from './budget-rules'
import type { BudgetClassifyResultDto, BudgetLineDto, BudgetOperationDto } from './budget.models'

export interface ClassifyDialogData { operation: BudgetOperationDto, lines: BudgetLineDto[] }

/** Puts an operation in a line (ADR 0020), with an optional spread and an optional rule for the next imports. */
@Component({
  selector: 'app-classify-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame heading="Ranger l’opération" formId="classify-form" saveLabel="Ranger" [busy]="busy()" [dirty]="dirty()" [error]="error()"
      [dangerLabel]="o.lineId ? 'Remettre en non classée' : (o.source === 'manual' ? 'Supprimer l’opération' : '')" [dangerIcon]="o.lineId ? 'undo' : 'delete'"
      (danger)="o.lineId ? unclassify() : remove()">
      <form id="classify-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-card summary">
          <span class="label">{{ o.label }}</span>
          <span class="lo-muted">{{ dayLabel(o.date) }}{{ o.postedOn && o.postedOn !== o.date ? ' (débitée le ' + dayLabel(o.postedOn) + ')' : '' }} · <strong>{{ euro(o.amount, true) }}</strong></span>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Ligne</mat-label>
          <mat-select name="line" [(ngModel)]="draft.lineId" required>
            @for (group of groups(); track group.name) {
              <mat-optgroup [label]="group.name">
                @for (l of group.lines; track l.id) { <mat-option [value]="l.id">{{ l.name }}</mat-option> }
              </mat-optgroup>
            }
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
          <span class="lo-help">Pour un achat qui couvre plusieurs mois : compléments pour 6 mois, assurance annuelle.</span>
        </div>
        @if (judgement) {
          <p class="lo-notice">Une règle ne suffit pas ici : ce libellé cache des achats de toute nature ou une personne. Demande à Claude de le ranger, il peut retrouver l’achat dans tes emails.</p>
        } @else {
          <div class="lo-field-group">
            <span class="lo-field-label">Créer une règle pour les prochaines fois</span>
            <div class="lo-chips" role="group" aria-label="Créer une règle">
              @for (c of [true, false]; track c) {
                <button type="button" class="lo-filter-chip" [class.is-on]="draft.makeRule === c" [attr.aria-pressed]="draft.makeRule === c" (click)="draft.makeRule = c">
                  @if (draft.makeRule === c) { <mat-icon svgIcon="check" /> }{{ c ? 'Oui' : 'Non' }}</button>
              }
            </div>
          </div>
          @if (draft.makeRule) {
            <mat-form-field appearance="outline">
              <mat-label>Libellé contient</mat-label>
              <input matInput name="ruleText" maxlength="100" [(ngModel)]="draft.ruleText" />
              <mat-hint>Sans tenir compte des majuscules ni des accents. Les autres opérations non classées qui correspondent seront rangées aussi.</mat-hint>
            </mat-form-field>
          }
        }
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .summary { display: flex; flex-direction: column; gap: 4px; padding: 12px 16px; }
    .label { font: var(--mat-sys-title-small); overflow-wrap: anywhere; }
  `],
})
export class ClassifyDialogComponent {
  private readonly api = inject(BudgetApiService)
  private readonly ref = inject<MatDialogRef<ClassifyDialogComponent, BudgetClassifyResultDto | 'deleted'>>(MatDialogRef)
  private readonly data = inject<ClassifyDialogData>(MAT_DIALOG_DATA)
  readonly o = this.data.operation
  readonly spreads = SPREADS
  readonly euro = euro
  readonly dayLabel = dayLabel
  readonly judgement = needsJudgement(this.o.label)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = { lineId: this.o.lineId ?? '', spread: this.o.spread, makeRule: !this.o.lineId && !this.judgement, ruleText: ruleTextOf(this.o.label) }
  private readonly initial = formSnapshot(this.draft)

  groups(): { name: string, lines: BudgetLineDto[] }[] {
    const usable = this.data.lines.filter(l => l.active && (this.o.amount > 0 || l.kind !== 'income'))
    const names = [...new Set(usable.map(l => l.category))]
    return names.map(name => ({ name, lines: usable.filter(l => l.category === name) }))
  }

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  async save(): Promise<void> {
    if (this.busy()) return
    if (!this.draft.lineId) { this.error.set('Choisis une ligne.'); return }
    const ruleText = !this.judgement && this.draft.makeRule && this.draft.ruleText.trim() ? this.draft.ruleText.trim() : null
    await this.run(() => firstValueFrom(this.api.classify(this.o.id, this.draft.lineId, this.draft.spread, ruleText)))
  }

  async unclassify(): Promise<void> { await this.run(() => firstValueFrom(this.api.classify(this.o.id, null, 1, null))) }

  async remove(): Promise<void> {
    this.busy.set(true); this.error.set('')
    try { await firstValueFrom(this.api.deleteOperation(this.o.id)); this.ref.close('deleted') }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }

  private async run(action: () => Promise<BudgetClassifyResultDto>): Promise<void> {
    this.busy.set(true); this.error.set('')
    try { this.ref.close(await action()) }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
