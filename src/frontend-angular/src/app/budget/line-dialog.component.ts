import { Component, computed, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatAutocompleteModule } from '@angular/material/autocomplete'
import { MatButtonModule } from '@angular/material/button'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MatCheckboxModule } from '@angular/material/checkbox'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { BudgetApiService } from './budget-api.service'
import { addMonths, euro, hasAmount, KINDS, kindOf, lineDraftOf, lineRequest, monthLabel, monthlyOf, PERIODS } from './budget-rules'
import type { BudgetLineDto } from './budget.models'

export interface LineDialogData { line: BudgetLineDto | null, categories: string[], currentMonth: string, supplementsMonthlyCost: number }
export interface LineDialogResult { line: BudgetLineDto | null, message: string }

/**
 * Creates or edits a budget line (ADR 0020): name, type, poste, planned amount per month, per year or every N months (a change
 * applies from a chosen month), "à diminuer", or the supplements' cost computed by Health. Stopped or deleted from here.
 */
@Component({
  selector: 'app-line-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatAutocompleteModule, MatButtonModule, MatButtonToggleModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="line ? 'Modifier la ligne' : 'Nouvelle ligne'" formId="line-form" [saveLabel]="line ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="line ? (line.active ? 'Arrêter la ligne' : 'Supprimer la ligne') : ''"
      [dangerIcon]="line?.active ? 'block' : 'delete'" (danger)="line?.active ? stop() : remove()">
      <form id="line-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" required maxlength="100" placeholder="Ex. Travaux" [(ngModel)]="draft.name" />
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Type</span>
          <div class="lo-chips" role="group" aria-label="Type">
            @for (k of kinds; track k.value) {
              <button type="button" class="lo-filter-chip" [class.is-on]="draft.kind === k.value" [attr.aria-pressed]="draft.kind === k.value" (click)="draft.kind = k.value">
                @if (draft.kind === k.value) { <mat-icon svgIcon="check" /> }{{ k.label }}</button>
            }
          </div>
          <span class="lo-help">{{ kindOf(draft.kind).help }}</span>
        </div>
        @if (expense()) {
          <mat-form-field appearance="outline">
            <mat-label>Poste</mat-label>
            <input matInput name="category" required maxlength="60" [(ngModel)]="draft.category" [matAutocomplete]="postes" />
            <mat-autocomplete #postes="matAutocomplete">
              @for (c of categories(); track c) { <mat-option [value]="c">{{ c }}</mat-option> }
            </mat-autocomplete>
            <mat-hint>Choisis un poste ou écris-en un nouveau.</mat-hint>
          </mat-form-field>
        }
        @if (draft.kind === 'fixed' || draft.kind === 'envelope') {
          <mat-checkbox name="supplements" [(ngModel)]="draft.fromSupplements">
            Prévu = coût par mois de mes compléments, calculé par la Santé ({{ euro(data.supplementsMonthlyCost) }} aujourd’hui)
          </mat-checkbox>
        }
        @if (hasAmount(draft.kind) && !(draft.fromSupplements && draft.kind !== 'income')) {
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ draft.kind === 'income' ? 'Prévu par mois (€)' : 'Montant (€)' }}</mat-label>
              <input matInput name="amount" inputmode="decimal" placeholder="Ex. 150" [(ngModel)]="draft.amount" />
              @if (monthly() !== null && draft.period !== 'month' && draft.kind !== 'income') { <mat-hint>Prévu par mois : {{ euro(monthly()!) }}</mat-hint> }
              @if (draft.kind === 'income') { <mat-hint>Selon les congés et RTT : modifiable mois par mois.</mat-hint> }
            </mat-form-field>
            @if (draft.kind !== 'income') {
              <mat-button-toggle-group name="period" aria-label="Période" [(ngModel)]="draft.period" hideSingleSelectionIndicator="false">
                @for (p of periods; track p.value) { <mat-button-toggle [value]="p.value">{{ p.label }}</mat-button-toggle> }
              </mat-button-toggle-group>
            }
          </div>
          @if (draft.period === 'months' && draft.kind !== 'income') {
            <mat-form-field appearance="outline" class="short">
              <mat-label>Tous les … mois</mat-label>
              <input matInput name="months" type="number" min="1" max="60" step="1" [(ngModel)]="draft.months" />
            </mat-form-field>
          }
          @if (line) {
            <div class="lo-field-group">
              <span class="lo-field-label">Un nouveau montant compte à partir de</span>
              <mat-button-toggle-group name="from" aria-label="À partir de" [(ngModel)]="draft.fromMonth" hideSingleSelectionIndicator="false">
                @for (m of fromMonths; track m) { <mat-button-toggle [value]="m">{{ monthLabel(m) }}</mat-button-toggle> }
              </mat-button-toggle-group>
              <span class="lo-help">Les mois d’avant gardent leur prévu.</span>
            </div>
          }
        }
        @if (expense()) {
          <mat-checkbox name="reduce" [(ngModel)]="draft.reduce">À diminuer : suivie dans l’évolution</mat-checkbox>
        }
        <mat-form-field appearance="outline">
          <mat-label>Notes</mat-label>
          <textarea matInput name="notes" rows="2" maxlength="1000" [(ngModel)]="draft.notes"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`.short { max-width: 220px; } .lo-form-row { align-items: center; }`],
})
export class LineDialogComponent {
  private readonly api = inject(BudgetApiService)
  private readonly ref = inject<MatDialogRef<LineDialogComponent, LineDialogResult>>(MatDialogRef)
  readonly data = inject<LineDialogData>(MAT_DIALOG_DATA)
  readonly line = this.data.line
  readonly kinds = KINDS
  readonly periods = PERIODS
  readonly kindOf = kindOf
  readonly hasAmount = hasAmount
  readonly euro = euro
  readonly monthLabel = monthLabel
  readonly fromMonths = [this.data.currentMonth, addMonths(this.data.currentMonth, 1)]
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = lineDraftOf(this.line, this.data.currentMonth)
  private readonly initial = formSnapshot(this.draft)
  readonly categories = computed(() => this.data.categories)

  expense(): boolean { return this.draft.kind === 'fixed' || this.draft.kind === 'envelope' || this.draft.kind === 'extra' }
  monthly(): number | null { return monthlyOf(this.draft) }
  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = lineRequest(this.draft, this.line)
    if (typeof request === 'string') { this.error.set(request); return }
    await this.run(async () => {
      const line = await firstValueFrom(this.line ? this.api.updateLine(this.line.id, request) : this.api.createLine(request))
      return { line, message: this.line ? 'Ligne enregistrée' : 'Ligne ajoutée' }
    })
  }

  async stop(): Promise<void> {
    if (!this.line) return
    await this.run(async () => ({ line: await firstValueFrom(this.api.stopLine(this.line!.id, this.data.currentMonth)),
      message: `« ${this.line!.name} » arrêtée depuis ${monthLabel(this.data.currentMonth).toLowerCase()} ; son historique reste` }))
  }

  async remove(): Promise<void> {
    if (!this.line) return
    await this.run(async () => { await firstValueFrom(this.api.deleteLine(this.line!.id)); return { line: null, message: 'Ligne supprimée' } })
  }

  private async run(action: () => Promise<LineDialogResult>): Promise<void> {
    this.busy.set(true); this.error.set('')
    try { this.ref.close(await action()) }
    catch (error) {
      const message = apiErrorMessage(error)
      this.error.set(/409|conflict|conflit/i.test(message) ? 'Des opérations ou des règles utilisent cette ligne : elle reste arrêtée.' : message)
      this.busy.set(false)
    }
  }
}
