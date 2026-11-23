import { Component, computed, effect, inject, signal } from '@angular/core'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MatIconModule } from '@angular/material/icon'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { BarChartComponent } from './bar-chart.component'
import { BudgetApiService } from './budget-api.service'
import { euro, percent, shortMonth } from './budget-rules'
import type { BudgetTrendDto } from './budget.models'

/** Evolution (ADR 0020): spending per month by poste, unplanned extras, and the lines to reduce. */
@Component({
  selector: 'app-budget-trend-page',
  standalone: true,
  imports: [BarChartComponent, MatButtonToggleModule, MatIconModule],
  template: `
    <div class="lo-page">
      <header class="lo-page-head"><div class="lo-page-title"><h1 class="lo-bar-title">Évolution</h1></div>
        <mat-button-toggle-group aria-label="Période" [value]="months()" (change)="months.set($event.value)" hideSingleSelectionIndicator="false">
          <mat-button-toggle [value]="6">6 mois</mat-button-toggle><mat-button-toggle [value]="12">12 mois</mat-button-toggle>
        </mat-button-toggle-group>
      </header>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (trend(); as t) {
        <div class="lo-stats">
          <div class="lo-stat"><span class="lo-stat-label">Dépenses moyennes</span><span class="lo-stat-value">{{ euro(average('spent')) }}</span><span class="lo-stat-note">par mois terminé</span></div>
          <div class="lo-stat"><span class="lo-stat-label">Extras moyens</span><span class="lo-stat-value">{{ euro(average('extras')) }}</span><span class="lo-stat-note">par mois terminé</span></div>
          <div class="lo-stat"><span class="lo-stat-label">Revenus moyens</span><span class="lo-stat-value">{{ euro(average('received')) }}</span><span class="lo-stat-note">par mois terminé</span></div>
        </div>
        <section class="lo-section">
          <div class="lo-section-head"><h2>Dépenses par mois et par poste</h2></div>
          <div class="stack" role="img" aria-label="Dépenses par mois et par poste">
            @for (m of t.months; track m.month) {
              <div class="col" [attr.title]="m.month + ' : ' + euro(m.spent)">
                <span class="value">{{ euro(Math.round(m.spent)) }}</span>
                <div class="track">
                  @for (c of categories(); track c.name) {
                    @if (m.byCategory[c.name]; as v) { <span class="seg" [class]="'seg c' + c.index" [style.height.%]="percent(v, max())"></span> }
                  }
                </div>
                <span class="name">{{ shortMonth(m.month) }}</span>
              </div>
            }
          </div>
          <div class="legend">
            @for (c of categories(); track c.name) { <span class="key"><span [class]="'dot c' + c.index"></span>{{ c.name }}</span> }
          </div>
        </section>
        <section class="lo-section">
          <div class="lo-section-head"><h2>Extras non planifiés</h2></div>
          <app-bar-chart label="Extras non planifiés par mois" [bars]="extraBars()" />
        </section>
        <section class="lo-section">
          <div class="lo-section-head"><h2>Lignes à diminuer</h2><span class="lo-muted">premier → dernier mois terminé</span></div>
          @if (t.reduce.length) {
            <div class="lo-list">
              @for (r of reduceRows(); track r.name) {
                <div class="lo-row"><div class="lo-row-main">
                  <span class="lo-lead-icon"><mat-icon [svgIcon]="r.icon" /></span>
                  <span class="lo-row-text"><span class="lo-row-title">{{ r.name }}</span><span class="lo-row-sub">{{ r.text }}</span></span>
                  <strong [class.is-up]="r.up">{{ r.change }}</strong>
                </div></div>
              }
            </div>
          } @else { <p class="lo-help">Aucune ligne marquée « à diminuer ».</p> }
        </section>
      }
    </div>
  `,
  styles: [`
    .lo-stat { display: flex; flex-direction: column; }
    .stack { display: flex; gap: 6px; height: 220px; padding: 12px 12px 0; border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; overflow-x: auto; }
    .col { display: flex; flex: 1 1 0; min-width: 40px; flex-direction: column; align-items: center; gap: 4px; }
    .value, .name { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-small); white-space: nowrap; }
    .name { padding-bottom: 6px; }
    .track { display: flex; width: 100%; max-width: 44px; flex: 1 1 auto; flex-direction: column-reverse; }
    .seg { display: block; width: 100%; min-height: 1px; }
    .legend { display: flex; flex-wrap: wrap; gap: 8px 16px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-medium); }
    .key { display: inline-flex; align-items: center; gap: 6px; }
    .dot { width: 12px; height: 12px; border-radius: 3px; }
    .c0 { background: var(--mat-sys-primary); } .c1 { background: var(--mat-sys-tertiary); } .c2 { background: var(--mat-sys-secondary); }
    .c3 { background: var(--mat-sys-primary-fixed-dim); } .c4 { background: var(--mat-sys-tertiary-fixed-dim); } .c5 { background: var(--mat-sys-secondary-fixed-dim); }
    .c6 { background: var(--mat-sys-outline); } .c7 { background: var(--mat-sys-error); }
    .is-up { color: var(--mat-sys-error); }
  `],
})
export class BudgetTrendPageComponent {
  private readonly api = inject(BudgetApiService)
  readonly months = signal(6)
  readonly trend = signal<BudgetTrendDto | null>(null)
  readonly error = signal('')
  readonly euro = euro
  readonly percent = percent
  readonly shortMonth = shortMonth
  readonly Math = Math
  /** The 6 biggest postes keep their colour; the others are grouped in « Autres postes ». */
  readonly categories = computed(() => {
    const totals = new Map<string, number>()
    for (const m of this.trend()?.months ?? []) for (const [k, v] of Object.entries(m.byCategory)) totals.set(k, (totals.get(k) ?? 0) + v)
    return [...totals.entries()].sort((a, b) => b[1] - a[1]).map(([name], index) => ({ name, index: Math.min(index, 7) }))
  })
  readonly max = computed(() => Math.max(1, ...(this.trend()?.months ?? []).map(m => m.spent)))

  constructor() {
    effect(() => { void this.load(this.months()) })
  }

  average(key: 'spent' | 'extras' | 'received'): number {
    const done = (this.trend()?.months ?? []).slice(0, -1)
    return done.length ? done.reduce((t, m) => t + m[key], 0) / done.length : 0
  }

  extraBars() {
    const months = this.trend()?.months ?? []
    return months.map((m, i) => ({ label: shortMonth(m.month), value: m.extras, valueLabel: euro(Math.round(m.extras)), highlight: i === months.length - 1 }))
  }

  reduceRows() {
    const t = this.trend()
    if (!t) return []
    return t.reduce.map(r => {
      const values = r.values.slice(0, -1)
      const first = values.find(v => v !== 0) ?? 0
      const last = values.at(-1) ?? 0
      const diff = last - first
      const pct = first ? Math.round(diff / first * 100) : 0
      return { name: r.name, text: `${euro(first)} → ${euro(last)}`, change: Math.abs(diff) < 0.5 ? 'stable' : `${pct > 0 ? '+' : ''}${pct} %`, up: diff > 0.5,
        icon: diff < -0.5 ? 'trending_down' : diff > 0.5 ? 'trending_up' : 'trending_flat' }
    })
  }

  private async load(months: number): Promise<void> {
    try { this.trend.set(await firstValueFrom(this.api.trend(months))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
