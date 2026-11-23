import { Component, effect, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { matchesQuery } from '@/app/shared/ui/list-page.component'
import { BudgetApiService } from './budget-api.service'
import { SELECTED_MONTH } from './budget-month'
import { classifiedLabel, cleanLabel, dayLabel, euro } from './budget-rules'
import type { BudgetImportDto, BudgetLineDto, BudgetOperationDto, BudgetOperationListDto } from './budget.models'
import { ClassifyDialogComponent, type ClassifyDialogData } from './classify-dialog.component'
import { MonthSwitchComponent } from './month-switch.component'
import { OperationDialogComponent, type OperationDialogData } from './operation-dialog.component'

type Filter = 'all' | 'unclassified' | 'spread'

/** Operations of a month (ADR 0020): OFX import, typed operations, classification and spreading. */
@Component({
  selector: 'app-budget-operations-page',
  standalone: true,
  imports: [MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MonthSwitchComponent],
  template: `
    <div class="lo-page">
      <header class="lo-page-head">
        <div class="lo-page-title"><h1 class="lo-bar-title">Opérations</h1></div>
        <div class="lo-actions">
          <button mat-flat-button type="button" [disabled]="importing()" (click)="picker.click()"><mat-icon svgIcon="upload_file" />Importer un relevé OFX</button>
          <input #picker class="sr-only" type="file" accept=".ofx,.qfx,application/x-ofx" (change)="importFile($event)" />
          <button mat-stroked-button type="button" (click)="addManual()"><mat-icon svgIcon="add" />Saisir une opération</button>
        </div>
      </header>
      @if (imported(); as i) {
        <div class="lo-notice import" role="status">
          <mat-icon svgIcon="check_circle" />
          <span>Relevé {{ i.from && i.to ? 'du ' + dayLabel(i.from) + ' au ' + dayLabel(i.to) : '' }} : {{ i.read }} opérations lues, {{ i.duplicates }} déjà importées ignorées,
            {{ i.created }} nouvelles. {{ i.classified }} rangées par les règles, {{ i.unclassified }} à ranger.</span>
          <button mat-button type="button" (click)="imported.set(null)" aria-label="Fermer le message"><mat-icon svgIcon="close" /></button>
        </div>
      }
      <p class="lo-help">BoursoBank : Mes comptes › le compte › Opérations › Exporter, format OFX. Réimporter une période déjà importée est sans risque :
        les opérations déjà là sont reconnues et ignorées.</p>
      <app-month-switch [month]="month()" (changed)="month.set($event)" />
      <div class="lo-filters">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix svgIcon="search" />
          <mat-label>Rechercher dans les libellés</mat-label>
          <input matInput type="search" [value]="query()" (input)="query.set($any($event.target).value)" />
        </mat-form-field>
        @for (f of filters; track f.value) {
          <button type="button" class="lo-filter-chip" [class.is-on]="filter() === f.value" [attr.aria-pressed]="filter() === f.value" (click)="filter.set(f.value)">
            @if (filter() === f.value) { <mat-icon svgIcon="check" /> }{{ f.label }}{{ f.value === 'unclassified' && list() ? ' (' + list()!.unclassifiedCount + ')' : '' }}</button>
        }
      </div>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (list(); as l) {
        @if (shown().length) {
          <div class="lo-section-head"><h2>{{ shown().length }} opération{{ shown().length > 1 ? 's' : '' }}</h2><span class="lo-muted">{{ euro(total(), true) }}</span></div>
          <div class="lo-list">
            @for (o of shown(); track o.id) {
              <div class="lo-row">
                <button type="button" class="lo-row-main" (click)="classify(o)" [attr.aria-label]="cleanLabel(o.label) + ', ' + euro(o.amount, true) + ', ' + (o.lineName ?? 'non classée')">
                  <span class="lo-lead-icon" [class.is-pending]="!o.lineId"><mat-icon [svgIcon]="o.lineId ? icon(o) : 'help'" /></span>
                  <span class="lo-row-text">
                    <span class="lo-row-title">{{ cleanLabel(o.label) }}</span>
                    <span class="lo-row-sub">{{ dayLabel(o.date) }} · {{ o.lineName ?? 'Non classée' }}{{ o.spread > 1 ? ' · étalée sur ' + o.spread + ' mois' : '' }}{{ o.source === 'manual' ? ' · saisie' : '' }}</span>
                  </span>
                  <span class="amount"><strong [class.is-positive]="o.amount > 0">{{ euro(o.amount, true) }}</strong><span class="lo-row-sub">{{ classifiedLabel(o) }}</span></span>
                </button>
              </div>
            }
          </div>
        } @else {
          <p class="lo-empty">{{ filter() === 'unclassified' ? 'Tout est rangé pour ce mois.' : 'Aucune opération. Importe un relevé OFX de ta banque.' }}</p>
        }
        @if (filter() === 'spread') { <p class="lo-help">Une opération étalée compte pour une part dans chacun des mois qu’elle couvre, à partir de son mois.</p> }
        <p class="lo-help">La date est celle de l’achat lue dans le libellé (« CARTE 29/10/26 … »), sinon la date de débit.
          @for (a of l.accounts; track a.account) { Compte ····{{ a.account.slice(-4) }}{{ a.balance !== null ? ' : solde ' + euro(a.balance) + (a.balanceOn ? ' au ' + dayLabel(a.balanceOn) : '') : '' }}. }</p>
      }
    </div>
  `,
  styles: [`
    .import { display: flex; align-items: center; gap: 8px; }
    .import span { flex: 1 1 auto; }
    .amount { display: flex; flex: 0 0 auto; flex-direction: column; align-items: flex-end; text-align: right; }
    .is-positive { color: var(--mat-sys-primary); }
    .lo-lead-icon.is-pending { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .search { flex: 1 1 240px; }
  `],
})
export class BudgetOperationsPageComponent implements OnInit {
  private readonly api = inject(BudgetApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly route = inject(ActivatedRoute)
  readonly month = SELECTED_MONTH
  readonly filter = signal<Filter>('all')
  readonly query = signal('')
  readonly list = signal<BudgetOperationListDto | null>(null)
  readonly imported = signal<BudgetImportDto | null>(null)
  readonly importing = signal(false)
  readonly error = signal('')
  readonly filters: { value: Filter, label: string }[] = [{ value: 'all', label: 'Toutes' }, { value: 'unclassified', label: 'Non classées' }, { value: 'spread', label: 'Étalées' }]
  readonly euro = euro
  readonly dayLabel = dayLabel
  readonly cleanLabel = cleanLabel
  readonly classifiedLabel = classifiedLabel
  private lines: BudgetLineDto[] = []

  constructor() {
    effect(() => { void this.load(this.month(), this.filter()) })
  }

  async ngOnInit(): Promise<void> {
    const f = this.route.snapshot.queryParamMap.get('filter')
    if (f === 'unclassified' || f === 'spread') this.filter.set(f)
    try { this.lines = (await firstValueFrom(this.api.lines(true))).lines } catch { this.lines = [] }
  }

  shown(): BudgetOperationDto[] { return (this.list()?.operations ?? []).filter(o => matchesQuery(o.label, this.query())) }
  total(): number { return this.shown().reduce((t, o) => t + o.amount, 0) }
  icon(o: BudgetOperationDto): string { return o.classifiedBy === 'rule' ? 'rule' : o.classifiedBy === 'assistant' ? 'smart_toy' : 'touch_app' }

  async importFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return
    this.importing.set(true); this.error.set('')
    try {
      const result = await firstValueFrom(this.api.importStatement(file))
      this.imported.set(result)
      if (result.to) this.month.set(result.to.slice(0, 7))
      await this.load(this.month(), this.filter())
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.importing.set(false) }
  }

  async classify(o: BudgetOperationDto): Promise<void> {
    if (!this.lines.length) { try { this.lines = (await firstValueFrom(this.api.lines(true))).lines } catch { /* shown in the dialog */ } }
    if (!this.lines.length) { this.snack.open('Crée d’abord tes lignes prévues.', undefined, { duration: 4000 }); return }
    const result = await firstValueFrom(openEditor<ClassifyDialogComponent, ClassifyDialogData, unknown>(this.dialog, ClassifyDialogComponent, { operation: o, lines: this.lines }).afterClosed())
    if (!result) return
    if (result === 'deleted') this.snack.open('Opération supprimée', undefined, { duration: 4000 })
    else {
      const r = result as { operation: BudgetOperationDto, rule: unknown, classified: number }
      this.snack.open(r.operation.lineName ? `Rangée dans « ${r.operation.lineName} »${r.rule ? ' · règle créée' + (r.classified ? `, ${r.classified} autre${r.classified > 1 ? 's' : ''} rangée${r.classified > 1 ? 's' : ''}` : '') : ''}` : 'Remise en non classée',
        undefined, { duration: 5000 })
    }
    await this.load(this.month(), this.filter())
  }

  async addManual(): Promise<void> {
    if (!this.lines.length) { try { this.lines = (await firstValueFrom(this.api.lines(true))).lines } catch { /* empty list */ } }
    const today = new Date().toISOString().slice(0, 10)
    const result = await firstValueFrom(openEditor<OperationDialogComponent, OperationDialogData, BudgetOperationDto>(this.dialog, OperationDialogComponent, { lines: this.lines, today }).afterClosed())
    if (!result) return
    this.snack.open('Opération ajoutée', undefined, { duration: 4000 })
    this.month.set(result.date.slice(0, 7))
    await this.load(this.month(), this.filter())
  }

  private async load(month: string, filter: Filter): Promise<void> {
    try { this.list.set(await firstValueFrom(this.api.operations(month, filter))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
