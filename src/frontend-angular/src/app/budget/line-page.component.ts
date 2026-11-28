import { Component, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { BarChartComponent, type Bar } from './bar-chart.component'
import { BudgetApiService } from './budget-api.service'
import { addMonths, amountLabel, cleanLabel, dayLabel, euro, kindOf, monthLabel, shortMonth } from './budget-rules'
import type { BudgetLineDetailDto, BudgetLineListDto, BudgetOperationDto } from './budget.models'
import { LineDialogComponent, type LineDialogData, type LineDialogResult } from './line-dialog.component'
import { RuleDialogComponent, type RuleDialogData, type RuleDialogResult } from './rule-dialog.component'

/** One line (ADR 0020): planned and real month by month, its operations of the last two months and its rules. */
@Component({
  selector: 'app-budget-line-page',
  standalone: true,
  imports: [BarChartComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page">
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (detail(); as d) {
        <header class="head">
          <a mat-icon-button class="lo-back-link" routerLink="/budget/lines" aria-label="Retour aux lignes prévues"><mat-icon svgIcon="arrow_back" /></a>
          <h1 class="lo-bar-title head-title">Ligne</h1>
          <button mat-flat-button type="button" class="lo-desktop-only" (click)="edit()"><mat-icon svgIcon="edit" />Modifier</button>
          <button mat-icon-button type="button" class="lo-phone-only" (click)="edit()" aria-label="Modifier la ligne" title="Modifier la ligne"><mat-icon svgIcon="edit" /></button>
        </header>
        <div class="hero">
          <span class="lo-lead-icon big"><mat-icon [svgIcon]="kindOf(d.line.kind).icon" /></span>
          <div class="hero-text">
            <h2 class="hero-title">{{ d.line.name }}</h2>
            <span class="lo-muted">{{ d.line.category }} · {{ amountLabel(d.line) }}</span>
            <span class="lo-tags">
              <span class="lo-tag is-ok">{{ kindOf(d.line.kind).label }}</span>
              @if (d.line.reduce) { <span class="lo-tag">À diminuer</span> }
              @if (d.line.fromSupplements) { <span class="lo-tag">Prévu calculé par la Santé</span> }
              @if (d.line.stoppedFrom) { <span class="lo-tag is-off">Arrêtée depuis {{ monthLabel(d.line.stoppedFrom).toLowerCase() }}</span> }
            </span>
          </div>
        </div>
        @if (d.line.fromSupplements) {
          <p class="lo-notice">Prévu = coût par mois de tes compléments actifs, calculé par la Santé (cures lissées) : {{ euro(d.line.planned) }}. Le réel vient de tes achats, étalés sur la durée qu’ils couvrent.</p>
        }
        <div class="lo-stats">
          <div class="lo-stat"><span class="lo-stat-label">Ce mois</span><span class="lo-stat-value">{{ euro(last().real) }}</span><span class="lo-stat-note">{{ last().planned ? 'prévu ' + euro(last().planned) : 'non prévu' }}</span></div>
          <div class="lo-stat"><span class="lo-stat-label">Moyenne</span><span class="lo-stat-value">{{ euro(average()) }}</span><span class="lo-stat-note">des 6 mois terminés</span></div>
        </div>
        <app-bar-chart [label]="'Réel par mois de ' + d.line.name" [bars]="bars()" />
        <p class="lo-help">Trait pointillé : le prévu. Barre rouge : mois au-dessus du prévu. Le dernier mois est en cours.</p>
        <section class="lo-section">
          <div class="lo-section-head"><h2>Opérations de {{ monthLabel(previousMonth()).toLowerCase() }} et {{ monthLabel(currentMonth()).toLowerCase() }}</h2></div>
          @if (operations().length) {
            <div class="lo-list">
              @for (o of operations(); track o.id) {
                <div class="lo-row"><div class="lo-row-main">
                  <span class="lo-row-text"><span class="lo-row-title">{{ cleanLabel(o.label) }}</span>
                    <span class="lo-row-sub">{{ dayLabel(o.date) }}{{ o.spread > 1 ? ' · étalée sur ' + o.spread + ' mois' : '' }}</span></span>
                  <strong>{{ euro(o.amount, true) }}</strong>
                </div></div>
              }
            </div>
          } @else { <p class="lo-empty">Aucune opération rangée ici.</p> }
        </section>
        <section class="lo-section">
          <div class="lo-section-head"><h2>Règles vers cette ligne</h2>
            <button mat-button type="button" (click)="addRule()"><mat-icon svgIcon="add" />Règle</button></div>
          @if (d.rules.length) {
            <div class="lo-list">
              @for (r of d.rules; track r.id) {
                <div class="lo-row"><div class="lo-row-main"><span class="lo-lead-icon"><mat-icon svgIcon="rule" /></span>
                  <span class="lo-row-text"><span class="lo-row-title">Libellé contient « {{ r.text }} »</span><span class="lo-row-sub">{{ r.matches }} opération{{ r.matches > 1 ? 's' : '' }}</span></span></div></div>
              }
            </div>
          } @else { <p class="lo-help">Aucune règle : les opérations de cette ligne sont rangées à la main ou par Claude.</p> }
        </section>
        @if (d.line.notes) { <p class="lo-muted notes">{{ d.line.notes }}</p> }
      }
    </div>
  `,
  styles: [`
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; font: var(--mat-sys-title-large); }
    .hero { display: flex; align-items: center; gap: 16px; }
    .hero-text { display: flex; min-width: 0; flex-direction: column; gap: 6px; }
    .hero-title { margin: 0; font: var(--mat-sys-headline-small); }
    .big { width: 64px; height: 64px; flex-basis: 64px; border-radius: 16px; }
    .lo-stat { display: flex; flex-direction: column; }
    .notes { white-space: pre-line; }
  `],
})
export class BudgetLinePageComponent implements OnInit {
  private readonly api = inject(BudgetApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly route = inject(ActivatedRoute)
  private readonly router = inject(Router)
  readonly detail = signal<BudgetLineDetailDto | null>(null)
  readonly operations = signal<BudgetOperationDto[]>([])
  readonly error = signal('')
  readonly euro = euro
  readonly kindOf = kindOf
  readonly amountLabel = amountLabel
  readonly monthLabel = monthLabel
  readonly cleanLabel = cleanLabel
  readonly dayLabel = dayLabel
  private list: BudgetLineListDto | null = null

  async ngOnInit(): Promise<void> { await this.load() }

  currentMonth(): string { return this.list?.month ?? new Date().toISOString().slice(0, 7) }
  previousMonth(): string { return addMonths(this.currentMonth(), -1) }
  last() { return this.detail()?.months.at(-1) ?? { month: '', planned: 0, real: 0 } }
  average(): number {
    const done = (this.detail()?.months ?? []).slice(-7, -1)
    return done.length ? done.reduce((t, m) => t + m.real, 0) / done.length : 0
  }
  bars(): Bar[] {
    const months = this.detail()?.months ?? []
    return months.map((m, i) => ({ label: shortMonth(m.month), value: Math.max(0, m.real), valueLabel: euro(Math.round(m.real)), planned: m.planned || undefined,
      highlight: i === months.length - 1, over: m.planned > 0 && m.real > m.planned + 0.5 }))
  }

  async edit(): Promise<void> {
    const d = this.detail()
    if (!d) return
    const data: LineDialogData = { line: d.line, categories: this.list?.categories ?? [], currentMonth: this.currentMonth(), supplementsMonthlyCost: this.list?.supplementsMonthlyCost ?? 0 }
    const result = await firstValueFrom(openEditor<LineDialogComponent, LineDialogData, LineDialogResult>(this.dialog, LineDialogComponent, data).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    if (!result.line) { await this.router.navigate(['/budget/lines']); return }
    await this.load()
  }

  async addRule(): Promise<void> {
    const d = this.detail()
    if (!d) return
    const result = await firstValueFrom(openEditor<RuleDialogComponent, RuleDialogData, RuleDialogResult>(this.dialog, RuleDialogComponent,
      { rule: null, lines: this.list?.lines ?? [d.line], lineId: d.line.id }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    const lineId = this.route.snapshot.paramMap.get('id') ?? ''
    try {
      const [detail, list] = await Promise.all([firstValueFrom(this.api.line(lineId)), firstValueFrom(this.api.lines(true))])
      this.list = list
      this.detail.set(detail)
      const months = [this.previousMonth(), this.currentMonth()]
      const pages = await Promise.all(months.flatMap(m => [firstValueFrom(this.api.operations(m, 'all')), firstValueFrom(this.api.operations(m, 'spread'))]))
      const seen = new Set<string>()
      this.operations.set(pages.flatMap(p => p.operations).filter(o => o.lineId === lineId && !seen.has(o.id) && seen.add(o.id))
        .sort((a, b) => b.date.localeCompare(a.date)))
      this.error.set('')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
