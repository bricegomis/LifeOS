import { Component, input, model } from '@angular/core'
import { MatIconModule } from '@angular/material/icon'

/** A small number set with − on the left and + on the right (ADR 0015 feeling, 0016 conventions). */
@Component({
  selector: 'app-stepper',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <div class="stepper" role="group" [attr.aria-label]="label() + (subject() ? ' pour ' + subject() : '')" [class.is-row]="layout() === 'row'">
      <div class="stepper-text">
        <span class="stepper-label">{{ label() }}</span>
        @if (hint()) { <span class="stepper-hint">{{ hint() }}</span> }
      </div>
      <div class="stepper-control">
        <button type="button" [disabled]="disabled() || value() <= min()" (click)="step(-1)"
          [attr.aria-label]="'Diminuer : ' + label().toLowerCase() + (subject() ? ' pour ' + subject() : '')"><mat-icon svgIcon="remove" /></button>
        <span class="stepper-value" aria-live="polite">{{ value() }}</span>
        <button type="button" [disabled]="disabled() || value() >= max()" (click)="step(1)"
          [attr.aria-label]="'Augmenter : ' + label().toLowerCase() + (subject() ? ' pour ' + subject() : '')"><mat-icon svgIcon="add" /></button>
      </div>
    </div>
  `,
  styles: [`
    .stepper { display: flex; min-width: 0; flex-direction: column; gap: 4px; }
    .stepper.is-row { flex-direction: row; align-items: center; gap: 12px; }
    .stepper.is-row .stepper-text { width: 128px; text-align: right; }
    .stepper-text { display: flex; flex-direction: column; }
    .stepper-label { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-large); }
    .stepper-hint { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .stepper-control {
      display: flex; height: 48px; box-sizing: border-box; align-items: center; justify-content: space-between; gap: 4px;
      padding: 0 4px; border: 1px solid var(--mat-sys-outline); border-radius: 24px;
    }
    .is-row .stepper-control { width: 168px; }
    .stepper-control button {
      display: inline-flex; width: 40px; height: 40px; align-items: center; justify-content: center; border: 0; border-radius: 20px;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container);
    }
    .stepper-control button:disabled { opacity: 0.38; }
    .stepper-value { font: var(--mat-sys-title-large); font-weight: 600; }
  `],
})
export class StepperComponent {
  readonly label = input.required<string>()
  readonly hint = input('')
  /** What the value is about, for screen readers: "pour Chaise contre le mur". */
  readonly subject = input('')
  readonly min = input(0)
  readonly max = input(10)
  readonly disabled = input(false)
  readonly layout = input<'column' | 'row'>('column')
  readonly value = model.required<number>()

  step(direction: 1 | -1): void {
    this.value.set(Math.max(this.min(), Math.min(this.max(), this.value() + direction)))
  }
}
