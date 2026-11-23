import { Component, input } from '@angular/core'

export interface Bar { label: string, value: number, valueLabel: string, planned?: number, highlight?: boolean, over?: boolean }

/** Vertical bars in theme colours, with an optional planned mark per bar. */
@Component({
  selector: 'app-bar-chart',
  standalone: true,
  template: `
    <div class="bars" role="img" [attr.aria-label]="label()">
      @for (b of bars(); track $index) {
        <div class="col" [attr.title]="b.label + ' : ' + b.valueLabel">
          <span class="value">{{ b.valueLabel }}</span>
          <div class="track">
            @if (b.planned) { <span class="planned" [style.bottom.%]="height(b.planned)"></span> }
            <span class="bar" [class.is-over]="b.over" [class.is-current]="b.highlight" [style.height.%]="height(b.value)"></span>
          </div>
          <span class="name">{{ b.label }}</span>
        </div>
      }
    </div>
  `,
  styles: [`
    .bars { display: flex; align-items: stretch; gap: 6px; height: 180px; padding: 12px 12px 0; border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; overflow-x: auto; }
    .col { display: flex; flex: 1 1 0; min-width: 34px; flex-direction: column; align-items: center; gap: 4px; }
    .value, .name { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-small); white-space: nowrap; }
    .name { padding-bottom: 6px; }
    .track { position: relative; display: flex; width: 100%; max-width: 40px; flex: 1 1 auto; align-items: flex-end; }
    .bar { display: block; width: 100%; border-radius: 6px 6px 0 0; background: var(--mat-sys-primary); }
    .bar.is-current { background: var(--mat-sys-primary-container); }
    .bar.is-over { background: var(--mat-sys-error); }
    .planned { position: absolute; left: -3px; right: -3px; height: 0; border-top: 2px dashed var(--mat-sys-on-surface-variant); }
  `],
})
export class BarChartComponent {
  readonly label = input.required<string>()
  readonly bars = input.required<Bar[]>()

  height(value: number): number {
    const max = Math.max(1, ...this.bars().map(b => Math.max(b.value, b.planned ?? 0)))
    return Math.max(0, Math.min(100, value / max * 100))
  }
}
