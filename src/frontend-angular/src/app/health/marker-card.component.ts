import { Component, computed, input } from '@angular/core'
import { RouterLink } from '@angular/router'
import { HealthImageComponent } from './health-image.component'
import { dateLabel, rangeLabel, STATUS, TREND, valueLabel } from './marker-rules'
import type { MarkerDto } from './health.models'

/** A marker as a card (ADR 0018): last value, position against the target, trend and whether to measure it again. */
@Component({
  selector: 'app-marker-card',
  standalone: true,
  imports: [HealthImageComponent, RouterLink],
  template: `
    @let m = marker();
    <a class="lo-card card" [routerLink]="['/health/marker', m.id]" [attr.aria-label]="'Ouvrir le marqueur « ' + m.name + ' »'">
      <app-health-image [name]="m.name" />
      <span class="card-text">
        <span class="card-title">{{ m.name }}</span>
        <span class="card-meta">{{ meta() }}</span>
        <span class="lo-tags">
          @if (m.status) { <span class="lo-tag" [class]="'lo-tag ' + status[m.status].tag">{{ status[m.status].label }}</span> }
          @if (m.trend) { <span class="lo-tag" [class.is-ok]="m.trend === 'better' || m.trend === 'in_target'" [class.is-off]="m.trend === 'worse'">{{ trend[m.trend] }}</span> }
          @if (m.due) { <span class="lo-tag" [class.is-off]="m.late || !m.values.length" [class.is-warn]="!m.late && m.values.length">{{ !m.values.length ? 'À faire' : m.late ? 'À refaire, en retard' : 'À refaire bientôt' }}</span> }
          @if (m.isArchived) { <span class="lo-tag">Archivé</span> }
        </span>
      </span>
      @if (last(); as l) { <span class="value"><strong>{{ valueLabel(l.value, m.unit) }}</strong><span class="lo-muted">{{ m.values.length }} mesure{{ m.values.length > 1 ? 's' : '' }}</span></span> }
    </a>
  `,
  styles: [`
    .card { display: flex; align-items: center; gap: 12px; padding: 8px; color: inherit; text-decoration: none; }
    .card:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .card:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: -3px; }
    .card-text { display: flex; min-width: 0; flex: 1 1 auto; flex-direction: column; gap: 2px; }
    .card-title { font: var(--mat-sys-title-small); font-size: 15px; }
    .card-meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .lo-tags:empty { display: none; }
    .value { display: flex; flex: 0 0 auto; flex-direction: column; align-items: flex-end; padding-right: 8px; }
  `],
})
export class MarkerCardComponent {
  readonly marker = input.required<MarkerDto>()
  readonly status = STATUS
  readonly trend = TREND
  readonly valueLabel = valueLabel
  readonly last = computed(() => this.marker().values[this.marker().values.length - 1] ?? null)
  readonly meta = computed(() => {
    const m = this.marker()
    const last = this.last()
    return `${last ? 'Dernière : ' + dateLabel(last.day) : 'Jamais mesuré'} · cible ${rangeLabel(m.targetMin, m.targetMax, m.unit)}`
  })
}
