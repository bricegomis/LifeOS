import { NgTemplateOutlet } from '@angular/common'
import { Component, effect, inject, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { BudgetApiService } from './budget-api.service'
import { SELECTED_MONTH } from './budget-month'
import { amountLabel, euro, kindOf, percent } from './budget-rules'
import type { BudgetMonthDto, BudgetMonthLineDto } from './budget.models'
import { MonthSwitchComponent } from './month-switch.component'

/** The month (ADR 0020): incomes, expenses and rest, envelopes, then every line by poste with planned, real and gap. */
@Component({
  selector: 'app-budget-month-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MonthSwitchComponent, NgTemplateOutlet, RouterLink],
  template: `
    <div class="lo-page">
      <header class="lo-page-head">
        <div class="lo-page-title"><h1 class="lo-bar-title">Budget du mois</h1></div>
        <div class="lo-actions"><a mat-stroked-button routerLink="/budget/operations"><mat-icon svgIcon="upload_file" />Opérations</a></div>
      </header>
      <app-month-switch [month]="month()" [note]="note()" (changed)="month.set($event)" />
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (data(); as d) {
        @if (d.unclassifiedCount) {
          <div class="lo-notice notice" role="status">
            <mat-icon svgIcon="help" />
            <span>{{ d.unclassifiedCount }} opération{{ d.unclassifiedCount > 1 ? 's' : '' }} non classée{{ d.unclassifiedCount > 1 ? 's' : '' }} ({{ euro(d.unclassifiedTotal) }}) :
              comptées à part tant qu’elles ne sont pas rangées.</span>
            <button mat-flat-button type="button" (click)="toUnclassified()">Ranger</button>
          </div>
        }
        <div class="lo-stats">
          <div class="lo-stat"><span class="lo-stat-label">Revenus</span><span class="lo-stat-value">{{ euro(d.received) }}</span><span class="lo-stat-note">prévu {{ euro(d.plannedIncome) }}</span></div>
          <div class="lo-stat"><span class="lo-stat-label">Dépenses</span><span class="lo-stat-value">{{ euro(d.spent) }}</span><span class="lo-stat-note">prévu {{ euro(d.plannedExpenses) }}</span></div>
          <div class="lo-stat"><span class="lo-stat-label">Reste du mois</span><span class="lo-stat-value" [class.is-negative]="d.rest < 0">{{ euro(d.rest) }}</span><span class="lo-stat-note">prévu {{ euro(d.plannedRest) }}</span></div>
          <div class="lo-stat"><span class="lo-stat-label">Extras</span><span class="lo-stat-value">{{ euro(d.extras) }}</span><span class="lo-stat-note">non planifiés</span></div>
        </div>
        @if (!d.categories.length && !d.incomes.length) {
          <p class="lo-empty">Aucune ligne prévue. Crée tes lignes dans <a routerLink="/budget/lines">Lignes prévues</a>, ou demande à Claude de les reprendre de ton fichier Excel.</p>
        }
        @if (d.envelopes.length) {
          <section class="lo-section" aria-labelledby="envelopes">
            <h2 id="envelopes">Enveloppes</h2>
            <div class="lo-card envelopes">
              @for (e of d.envelopes; track e.line.id) {
                <a class="envelope" [routerLink]="['/budget/line', e.line.id]">
                  <span class="env-head"><span>{{ e.line.name }}{{ e.line.fromSupplements ? ' (calculé par la Santé)' : '' }}</span>
                    <strong>{{ euro(e.real) }} / {{ euro(e.planned) }}</strong></span>
                  <span class="bar" role="progressbar" [attr.aria-valuenow]="percent(e.real, e.planned)" aria-valuemin="0" aria-valuemax="100"
                    [attr.aria-label]="e.line.name"><span class="fill" [class.is-over]="e.over" [style.width.%]="percent(e.real, e.planned)"></span></span>
                  <span class="lo-row-sub">{{ e.over ? 'Dépassée de ' + euro(e.real - e.planned) : 'Reste ' + euro(e.planned - e.real) }}</span>
                </a>
              }
            </div>
          </section>
        }
        @for (c of d.categories; track c.category) {
          <section class="lo-section">
            <div class="lo-section-head"><h2>{{ c.category }}</h2><span class="lo-muted">{{ euro(c.real) }}{{ c.planned ? ' / ' + euro(c.planned) : '' }}</span></div>
            <div class="lo-list">@for (r of c.lines; track r.line.id) { <ng-container *ngTemplateOutlet="row; context: { $implicit: r }" /> }</div>
          </section>
        }
        @if (d.incomes.length) {
          <section class="lo-section">
            <div class="lo-section-head"><h2>Revenus</h2><span class="lo-muted">{{ euro(d.received) }} / {{ euro(d.plannedIncome) }}</span></div>
            <div class="lo-list">@for (r of d.incomes; track r.line.id) { <ng-container *ngTemplateOutlet="row; context: { $implicit: r }" /> }</div>
          </section>
        }
        @if (d.off) {
          <p class="lo-help">Hors budget ce mois : {{ euro(d.off, true) }} (virements entre tes comptes, avances entre proches). Ils ne comptent ni en dépense ni en revenu.</p>
        }
      }
    </div>
    <ng-template #row let-r>
      <div class="lo-row">
        <a class="lo-row-main" [routerLink]="['/budget/line', r.line.id]">
          <span class="lo-lead-icon"><mat-icon [svgIcon]="kindOf(r.line.kind).icon" /></span>
          <span class="lo-row-text">
            <span class="lo-row-title">{{ r.line.name }}</span>
            <span class="lo-row-sub">{{ amountLabel(r.line) }}{{ r.line.reduce ? ' · à diminuer' : '' }}{{ r.line.active ? '' : ' · arrêtée' }}</span>
          </span>
          <span class="amount">
            <strong [class.is-negative]="r.over">{{ euro(r.real) }}</strong>
            <span class="lo-row-sub">{{ sub(r) }}</span>
          </span>
        </a>
      </div>
    </ng-template>
  `,
  styles: [`
    .notice { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; }
    .notice span { flex: 1 1 240px; }
    .lo-stat { display: flex; flex-direction: column; }
    .is-negative { color: var(--mat-sys-error); }
    .envelopes { display: flex; flex-direction: column; gap: 16px; padding: 16px; }
    .envelope { display: flex; flex-direction: column; gap: 6px; color: inherit; text-decoration: none; }
    .env-head { display: flex; justify-content: space-between; gap: 8px; }
    .bar { display: block; height: 8px; border-radius: 4px; background: var(--mat-sys-surface-container-highest); overflow: hidden; }
    .fill { display: block; height: 8px; border-radius: 4px; background: var(--mat-sys-primary); }
    .fill.is-over { background: var(--mat-sys-error); }
    .amount { display: flex; flex: 0 0 auto; flex-direction: column; align-items: flex-end; text-align: right; }
    a.lo-row-main { text-decoration: none; }
  `],
})
export class BudgetMonthPageComponent {
  private readonly api = inject(BudgetApiService)
  private readonly router = inject(Router)
  readonly month = SELECTED_MONTH
  readonly data = signal<BudgetMonthDto | null>(null)
  readonly error = signal('')
  readonly euro = euro
  readonly percent = percent
  readonly kindOf = kindOf
  readonly amountLabel = amountLabel

  constructor() {
    effect(() => { void this.load(this.month()) })
  }

  note(): string {
    const status = this.data()?.status
    return status === 'current' ? 'En cours' : status === 'future' ? 'À venir : prévu et achats étalés seulement' : status === 'past' ? 'Terminé' : ''
  }

  sub(r: BudgetMonthLineDto): string {
    if (r.line.kind === 'extra') return 'non planifié'
    if (r.line.kind === 'off') return 'ne compte pas'
    if (r.line.kind === 'income') return r.planned ? 'prévu ' + euro(r.planned) : 'non prévu'
    const gap = Math.abs(r.gap) >= 0.5 ? ` · ${r.gap > 0 ? '+' : '−'}${euro(Math.abs(r.gap))}` : ''
    return 'prévu ' + euro(r.planned) + gap
  }

  toUnclassified(): void { void this.router.navigate(['/budget/operations'], { queryParams: { filter: 'unclassified' } }) }

  private async load(month: string): Promise<void> {
    try { this.data.set(await firstValueFrom(this.api.month(month))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
