import { Component, input, output } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { addMonths, monthLabel } from './budget-rules'

/** ‹ Novembre 2026 › : moves from month to month. */
@Component({
  selector: 'app-month-switch',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="switch">
      <button mat-icon-button type="button" (click)="changed.emit(addMonths(month(), -1))" aria-label="Mois précédent" title="Mois précédent"><mat-icon svgIcon="chevron_left" /></button>
      <div class="label"><span class="name">{{ monthLabel(month()) }}</span>@if (note()) { <span class="note">{{ note() }}</span> }</div>
      <button mat-icon-button type="button" (click)="changed.emit(addMonths(month(), 1))" aria-label="Mois suivant" title="Mois suivant"><mat-icon svgIcon="chevron_right" /></button>
    </div>
  `,
  styles: [`
    .switch { display: flex; align-items: center; gap: 8px; }
    .label { display: flex; flex: 1 1 auto; min-width: 0; flex-direction: column; align-items: center; text-align: center; }
    .name { font: var(--mat-sys-title-large); }
    .note { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `],
})
export class MonthSwitchComponent {
  readonly month = input.required<string>()
  readonly note = input('')
  readonly changed = output<string>()
  readonly addMonths = addMonths
  readonly monthLabel = monthLabel
}
