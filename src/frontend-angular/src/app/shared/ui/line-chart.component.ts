import { Component, computed, input } from '@angular/core'
import { CHART_HEIGHT, CHART_WIDTH, type ChartPoint, lineGeometry } from './chart-geometry'

/** A small line chart in theme colours, with an optional band (e.g. the usual HRV range). */
@Component({
  selector: 'app-line-chart',
  standalone: true,
  template: `
    <figure class="line-chart">
      <figcaption>
        <span class="name">{{ name() }}</span>
        @if (geometry().last !== null) { <span><strong>{{ format()(geometry().last!) }}</strong> {{ unit() }}</span> }
      </figcaption>
      @if (geometry().segments.length) {
        <div class="plot">
          <div class="axis" aria-hidden="true"><span>{{ format()(geometry().max) }}</span><span>{{ format()(geometry().min) }}</span></div>
          <svg role="img" [attr.aria-label]="label()" [attr.viewBox]="viewBox" preserveAspectRatio="none">
            @if (band()) { <rect class="band" x="0" [attr.y]="geometry().bandY" [attr.width]="width" [attr.height]="geometry().bandHeight" /> }
            @for (segment of geometry().segments; track $index) {
              <polyline [attr.points]="segment" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round" vector-effect="non-scaling-stroke" />
            }
          </svg>
        </div>
        <div class="ticks" aria-hidden="true">@for (tick of geometry().ticks; track $index) { <span>{{ tick }}</span> }</div>
      } @else {
        <p class="lo-muted lo-small">Pas encore de données.</p>
      }
      @if (note()) { <p class="lo-help">{{ note() }}</p> }
    </figure>
  `,
  styles: [`
    .line-chart { display: flex; flex-direction: column; gap: 6px; margin: 0; padding: 12px 16px; border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; background: var(--mat-sys-surface); }
    figcaption { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; }
    figcaption .name { font: var(--mat-sys-title-small); }
    figcaption strong { font: var(--mat-sys-title-large); }
    .plot { display: flex; gap: 8px; }
    .axis { display: flex; flex-direction: column; justify-content: space-between; height: 140px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-small); text-align: right; }
    svg { display: block; flex: 1 1 auto; min-width: 0; height: 140px; color: var(--mat-sys-primary); background: var(--mat-sys-surface-container-lowest); border-radius: 8px; }
    .band { fill: var(--mat-sys-primary-container); }
    .ticks { display: flex; justify-content: space-between; padding-left: 32px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-small); }
  `],
})
export class LineChartComponent {
  readonly name = input.required<string>()
  readonly label = input.required<string>()
  readonly points = input.required<ChartPoint[]>()
  readonly unit = input('')
  readonly band = input<[number, number] | null>(null)
  readonly note = input('')
  readonly format = input<(value: number) => string>(value => String(Math.round(value)))
  readonly geometry = computed(() => lineGeometry(this.points(), this.band()))
  readonly width = CHART_WIDTH
  readonly viewBox = `0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`
}
