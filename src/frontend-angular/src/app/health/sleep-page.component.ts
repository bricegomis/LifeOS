import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { LineChartComponent } from '@/app/shared/ui/line-chart.component'
import { HealthApiService } from './health-api.service'
import type { WellnessDto } from './health.models'
import { average, hoursMinutes, hrvBand, periodStart, series, sleepColumns, type WellnessPeriod } from './wellness-rules'

/** Sleep and recovery imported from Garmin (ADR 0019): sleep phases, HRV, resting heart rate, Body Battery and stress. */
@Component({
  selector: 'app-sleep-page',
  standalone: true,
  imports: [LineChartComponent],
  template: `
    <div class="lo-page">
      <header class="lo-page-head"><h1 class="lo-bar-title">Sommeil et récupération</h1></header>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      <div class="lo-chips" role="group" aria-label="Période">
        @for (option of periods; track option.id) {
          <button type="button" class="lo-filter-chip" [class.is-on]="period() === option.id" [attr.aria-pressed]="period() === option.id"
            (click)="period.set(option.id)">{{ option.label }}</button>
        }
      </div>
      @if (loading()) { <p class="lo-status" role="status">Chargement…</p> }
      @else if (!days().length) { <p class="lo-empty">Pas encore de données : elles arrivent avec la synchro Garmin (Sport › Garmin).</p> }
      @else {
        <div class="lo-stats">
          <div class="lo-stat"><div class="lo-stat-label">Sommeil moyen</div><div class="lo-stat-value">{{ hoursMinutes(avg('sleepSeconds')) }}</div></div>
          <div class="lo-stat"><div class="lo-stat-label">Profond</div><div class="lo-stat-value">{{ hoursMinutes(avg('deepSleepSeconds')) }}</div></div>
          <div class="lo-stat"><div class="lo-stat-label">FC au repos</div><div class="lo-stat-value">{{ whole(avg('restingHeartRate'), ' bpm') }}</div></div>
          <div class="lo-stat"><div class="lo-stat-label">HRV</div><div class="lo-stat-value">{{ whole(avg('hrvNightAverage'), ' ms') }}</div>
            @if (band(); as b) { <div class="lo-help">plage habituelle {{ b[0] }}–{{ b[1] }}</div> }</div>
          <div class="lo-stat"><div class="lo-stat-label">Body Battery au réveil</div><div class="lo-stat-value">{{ whole(avg('bodyBatteryWake'), '') }}</div></div>
        </div>

        <section class="lo-panel" aria-labelledby="sleep-phases">
          <div class="lo-panel-head"><h2 id="sleep-phases">Sommeil</h2>
            <span class="lo-muted lo-small">{{ period() === '4w' ? 'par nuit' : period() === '3m' ? 'moyenne par semaine' : 'moyenne par mois' }}</span></div>
          <div class="stack" role="img" aria-label="Durée et phases du sommeil sur la période">
            @for (column of columns(); track column.label) {
              <div class="stack-col" [title]="column.tip">
                @if (column.empty) { <span class="stack-empty"></span> } @else {
                  <span class="seg awake" [style.height.%]="column.awake"></span>
                  <span class="seg rem" [style.height.%]="column.rem"></span>
                  <span class="seg light" [style.height.%]="column.light"></span>
                  <span class="seg deep" [style.height.%]="column.deep"></span>
                }
              </div>
            }
          </div>
          <div class="ticks" aria-hidden="true"><span>{{ columns()[0]?.label }}</span><span>{{ columns().at(-1)?.label }}</span></div>
          <div class="legend">
            <span><i class="seg deep"></i>Profond</span><span><i class="seg light"></i>Léger</span><span><i class="seg rem"></i>Paradoxal</span><span><i class="seg awake"></i>Éveil</span>
            <span><i class="stack-empty"></i>Pas de données (montre non portée)</span>
          </div>
        </section>

        <div class="lo-grid-2">
          <app-line-chart name="HRV de la nuit" unit="ms" [points]="line('hrvNightAverage')" [band]="band()" label="HRV de la nuit et plage habituelle"
            note="Bande : ta plage habituelle selon Garmin." />
          <app-line-chart name="FC au repos" unit="bpm" [points]="line('restingHeartRate')" label="Fréquence cardiaque au repos" />
          <app-line-chart name="Body Battery au réveil" [points]="line('bodyBatteryWake')" label="Body Battery au réveil" />
          <app-line-chart name="Stress moyen" [points]="line('stressAverage')" label="Stress moyen de la journée"
            note="Aussi gardés de Garmin, lisibles par l’assistant : SpO2, préparation et charge d’entraînement, âge physique…" />
        </div>
      }
    </div>
  `,
  styles: [`
    .stack { display: flex; height: 170px; align-items: flex-end; gap: 3px; }
    .stack-col { display: flex; min-width: 0; height: 100%; flex: 1 1 0; flex-direction: column; justify-content: flex-end; }
    .seg { display: block; }
    .seg.deep { background: var(--mat-sys-primary); }
    .seg.light { background: color-mix(in srgb, var(--mat-sys-primary) 45%, transparent); }
    .seg.rem { background: var(--mat-sys-tertiary); }
    .seg.awake { background: var(--mat-sys-error-container); }
    .stack-empty { display: block; height: 24px; border: 1px dashed var(--mat-sys-outline); border-radius: 4px; }
    .ticks { display: flex; justify-content: space-between; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-small); }
    .legend { display: flex; flex-wrap: wrap; gap: 12px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-small); }
    .legend span { display: inline-flex; align-items: center; gap: 6px; }
    .legend i { display: inline-block; width: 12px; height: 12px; border-radius: 3px; }
  `],
})
export class SleepPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  readonly periods: { id: WellnessPeriod, label: string }[] = [{ id: '4w', label: '4 semaines' }, { id: '3m', label: '3 mois' }, { id: '1y', label: '1 an' }]
  readonly period = signal<WellnessPeriod>('4w')
  readonly all = signal<WellnessDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  private readonly today = new Intl.DateTimeFormat('fr-CA', { timeZone: 'Europe/Paris' }).format(new Date())
  readonly days = computed(() => { const from = periodStart(this.period(), this.today); return this.all().filter(day => day.date >= from) })
  readonly columns = computed(() => sleepColumns(this.all(), this.period(), this.today))
  readonly band = computed(() => hrvBand(this.all()))
  readonly hoursMinutes = hoursMinutes

  avg(field: Parameters<typeof average>[1]): number | null { return average(this.days(), field) }
  whole(value: number | null, unit: string): string { return value === null ? '—' : `${Math.round(value)}${unit}` }
  line(field: Parameters<typeof series>[3]) { return series(this.all(), this.period(), this.today, field) }

  async ngOnInit(): Promise<void> {
    try { this.all.set(await firstValueFrom(this.api.wellness(periodStart('1y', this.today), this.today))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }
}
