import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { HealthApiService } from './health-api.service'
import type { MarkerDto } from './health.models'
import { MarkerDialogComponent, type MarkerDialogData } from './marker-dialog.component'
import { dateLabel, dueLabel, frequencyLabel, monthLabel, num, rangeBar, rangeLabel, STATUS, TREND, valueLabel } from './marker-rules'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

/** One marker (ADR 0018): last value placed against the lab range and the target, what it is for and what a low or high value can mean, evolution, details and values. */
@Component({
  selector: 'app-marker-page',
  standalone: true,
  imports: [SkeletonComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page detail-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (marker(); as m) {
        <header class="head">
          <a mat-icon-button class="lo-back-link" routerLink="/health/analyses" aria-label="Retour aux analyses"><mat-icon svgIcon="arrow_back" /></a>
          <h1 class="lo-bar-title head-title">{{ m.name }}</h1>
          <button mat-icon-button type="button" (click)="edit(m)" aria-label="Modifier le marqueur" title="Modifier le marqueur"><mat-icon svgIcon="edit" /></button>
        </header>
        <p class="lo-muted">{{ last() ? 'Dernière valeur : ' + valueLabel(last()!.value, m.unit) + ', le ' + date(last()!.day) : 'Jamais mesuré' }}</p>
        <span class="lo-tags">
          @if (m.status) { <span [class]="'lo-tag ' + status[m.status].tag">{{ status[m.status].label }}</span> }
          @if (m.trend) { <span class="lo-tag" [class.is-ok]="m.trend === 'better' || m.trend === 'in_target'" [class.is-off]="m.trend === 'worse'">{{ trend[m.trend] }}</span> }
        </span>
        <p class="lo-notice" [class.is-off]="m.due">{{ due(m) }}. Fréquence : {{ frequency(m.frequencyMonths).toLocaleLowerCase('fr') }}.</p>
        <section class="lo-card range" aria-label="Position de la dernière valeur">
          <div class="bar">
            <span class="track"></span>
            <span class="lab" [style.left.%]="bar().labLeft" [style.width.%]="bar().labWidth"></span>
            <span class="target" [style.left.%]="bar().targetLeft" [style.width.%]="bar().targetWidth"></span>
            @if (bar().value !== null) { <span class="mark" [style.left.%]="bar().value"></span> }
          </div>
          <div class="legend">
            <span><i class="key lab-key"></i>Labo : {{ range(m.labMin, m.labMax, m.unit) }}</span>
            <span><i class="key target-key"></i>Ta cible : {{ range(m.targetMin, m.targetMax, m.unit) }}</span>
          </div>
        </section>
        @if (m.description || m.whenLow || m.whenHigh) {
          <section class="lo-card about" aria-label="À savoir">
            @if (m.description) { <div class="detail"><span class="lo-field-label">À quoi il sert</span><p class="text">{{ m.description }}</p></div> }
            @if (m.whenLow) { <div class="detail" [class.is-now]="m.status === 'below_lab'"><span class="lo-field-label">Si trop bas</span><p class="text">{{ m.whenLow }}</p></div> }
            @if (m.whenHigh) { <div class="detail" [class.is-now]="m.status === 'above_lab'"><span class="lo-field-label">Si trop haut</span><p class="text">{{ m.whenHigh }}</p></div> }
            <p class="lo-muted note">Informatif, ne remplace pas l’avis d’un médecin.</p>
          </section>
        }
        @if (m.values.length) {
          <section class="lo-section" aria-labelledby="evolution">
            <h2 id="evolution">Évolution</h2>
            <div class="lo-card chart" role="img" [attr.aria-label]="'Valeurs de ' + m.name + ' dans le temps'">
              @for (v of m.values; track v.analysisId) {
                <div class="col"><span class="col-value">{{ num(v.value) }}</span>
                  <span class="col-bar" [attr.data-status]="v.status" [style.height.px]="height(v.value)"></span>
                  <span class="lo-muted col-label">{{ month(v.day) }}</span></div>
              }
            </div>
          </section>
        }
        <section class="lo-card details" aria-label="Détails">
          <div class="detail"><span class="lo-field-label">Unité</span><span>{{ m.unit }}</span></div>
          <div class="detail"><span class="lo-field-label">Fréquence</span><span>{{ frequency(m.frequencyMonths) }}</span></div>
          <div class="detail"><span class="lo-field-label">Bilans</span><span>{{ m.panels.join(', ') || 'Aucun' }}</span></div>
        </section>
        @if (m.values.length) {
          <section class="lo-section" aria-labelledby="values"><h2 id="values">Valeurs</h2>
            <div class="lo-card">
              @for (v of reversed(); track v.analysisId) {
                <a class="row" [routerLink]="['/health/analysis', v.analysisId]"><span>{{ date(v.day) }}</span>
                  <span class="row-end"><strong>{{ valueLabel(v.value, m.unit) }}</strong><span [class]="'lo-tag ' + status[v.status].tag">{{ status[v.status].label }}</span></span></a>
              }
            </div></section>
        }
      } @else if (!error()) { <app-skeleton variant="page" /> }
    </div>
  `,
  styles: [`
    .detail-page { max-width: 880px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .range { display: flex; flex-direction: column; gap: 10px; padding: 16px; }
    .bar { position: relative; height: 28px; }
    .track { position: absolute; left: 0; right: 0; top: 10px; height: 8px; border-radius: 4px; background: var(--mat-sys-error-container); }
    .lab { position: absolute; top: 10px; height: 8px; background: var(--mat-sys-surface-container-highest); }
    .target { position: absolute; top: 6px; height: 16px; border-radius: 4px; background: var(--mat-sys-primary-container); }
    .mark { position: absolute; top: 0; width: 4px; height: 28px; margin-left: -2px; border-radius: 2px; background: var(--mat-sys-on-surface); }
    .legend { display: flex; flex-wrap: wrap; gap: 12px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .key { display: inline-block; width: 12px; height: 12px; margin-right: 6px; border-radius: 3px; vertical-align: -2px; }
    .lab-key { background: var(--mat-sys-surface-container-highest); }
    .target-key { background: var(--mat-sys-primary-container); }
    .chart { display: flex; align-items: flex-end; gap: 8px; height: 160px; padding: 12px 12px 0; overflow-x: auto; }
    .col { display: flex; min-width: 40px; flex: 1 1 0; flex-direction: column; align-items: center; justify-content: flex-end; gap: 4px; height: 100%; }
    .col-value { font: var(--mat-sys-label-medium); }
    .col-bar { width: 100%; max-width: 48px; border-radius: 6px 6px 0 0; background: var(--mat-sys-tertiary); }
    .col-bar[data-status='in_target'] { background: var(--mat-sys-primary); }
    .col-bar[data-status='below_lab'], .col-bar[data-status='above_lab'] { background: var(--mat-sys-error); }
    .col-label { padding-bottom: 6px; white-space: nowrap; }
    .details, .about { display: flex; flex-direction: column; }
    .text { margin: 4px 0 0; white-space: pre-line; }
    .is-now { border-left: 4px solid var(--mat-sys-error); }
    .note { margin: 0; padding: 8px 16px 12px; font: var(--mat-sys-body-small); }
    .detail { display: flex; flex-direction: column; padding: 12px 16px; }
    .detail + .detail { border-top: 1px solid var(--mat-sys-outline-variant); }
    .row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 16px; color: inherit; text-decoration: none; }
    .row + .row { border-top: 1px solid var(--mat-sys-outline-variant); }
    .row-end { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: 8px; }
  `],
})
export class MarkerPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? ''
  readonly marker = signal<MarkerDto | null>(null)
  readonly error = signal('')
  readonly status = STATUS
  readonly trend = TREND
  readonly valueLabel = valueLabel
  readonly date = dateLabel
  readonly month = monthLabel
  readonly range = rangeLabel
  readonly frequency = frequencyLabel
  readonly due = dueLabel
  readonly num = num
  readonly last = computed(() => this.marker()?.values.at(-1) ?? null)
  readonly reversed = computed(() => [...(this.marker()?.values ?? [])].reverse())
  readonly bar = computed(() => this.marker() ? rangeBar(this.marker()!) : { labLeft: 0, labWidth: 0, targetLeft: 0, targetWidth: 0, value: null })

  async ngOnInit(): Promise<void> { await this.load() }

  height(value: number): number {
    const m = this.marker()
    const max = Math.max(...(m?.values.map(v => v.value) ?? [1]), m?.targetMax ?? 0) || 1
    return Math.max(6, Math.round(value / max * 100))
  }

  async edit(m: MarkerDto): Promise<void> {
    const panels = (await firstValueFrom(this.api.markers(true)).catch(() => null))?.panels ?? m.panels
    const message = await firstValueFrom(openEditor<MarkerDialogComponent, MarkerDialogData, string>(this.dialog, MarkerDialogComponent, { marker: m, panels }).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.marker.set(await firstValueFrom(this.api.marker(this.id))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
