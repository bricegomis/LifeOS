import { Component, computed, input, output } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { RouterLink } from '@angular/router'
import { HealthImageComponent } from './health-image.component'
import { doseLabel, euro, momentLabel, statusLabel } from './supplement-rules'
import type { SupplementDto } from './health.models'

/**
 * A supplement as a card (ADR 0018). On the today page, the round button ticks it as taken today;
 * in the list, the monthly cost shows on the right.
 */
@Component({
  selector: 'app-supplement-card',
  standalone: true,
  imports: [HealthImageComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    @let s = supplement();
    <article class="lo-card card" [class.is-done]="tick() && s.takenToday">
      <a class="card-main" [routerLink]="['/health/supplement', s.id]" [attr.aria-label]="'Ouvrir « ' + s.name + ' »'">
        <app-health-image [path]="s.hasImage ? '/health/supplements/' + s.id + '/image' : null" [name]="s.name" [version]="s.updatedAt" />
        <span class="card-text">
          <span class="card-title">{{ s.name }}</span>
          <span class="card-meta">{{ meta() }}</span>
          @if (s.isStopped || !s.status.active || s.streak > 1) {
            <span class="lo-tags">
              @if (s.isStopped) { <span class="lo-tag">Arrêté</span> }
              @else if (!s.status.active) { <span class="lo-tag">{{ status() }}</span> }
              @if (s.streak > 1 && !s.isStopped) { <span class="lo-tag is-ok">Série {{ s.streak }} j</span> }
            </span>
          }
        </span>
        @if (!tick()) {
          <span class="cost"><strong>{{ s.monthlyCost === null ? '—' : euro(s.monthlyCost) }}</strong><span class="lo-muted">par mois</span></span>
        }
      </a>
      @if (tick()) {
        <button mat-icon-button type="button" class="check" [class.is-on]="s.takenToday" [disabled]="busy()"
          [attr.aria-label]="s.takenToday ? 'Décocher « ' + s.name + ' »' : 'Marquer « ' + s.name + ' » comme pris aujourd’hui'"
          [attr.title]="s.takenToday ? 'Décocher' : 'Pris aujourd’hui'" (click)="taken.emit(s)">
          <mat-icon [svgIcon]="s.takenToday ? 'check_circle' : 'radio_button_unchecked'" />
        </button>
      }
    </article>
  `,
  styles: [`
    .card { display: flex; align-items: center; padding-right: 4px; }
    .card-main { display: flex; min-width: 0; flex: 1 1 auto; align-items: center; gap: 12px; padding: 8px; color: inherit; text-decoration: none; }
    .card-main:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .card-main:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: -3px; }
    .card-text { display: flex; min-width: 0; flex: 1 1 auto; flex-direction: column; gap: 2px; }
    .card-title { font: var(--mat-sys-title-small); font-size: 15px; }
    .card-meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .is-done .card-title { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .cost { display: flex; flex: 0 0 auto; flex-direction: column; align-items: flex-end; padding-right: 8px; }
    .check { flex: 0 0 auto; color: var(--mat-sys-on-surface-variant); }
    .check.is-on { color: var(--mat-sys-primary); }
  `],
})
export class SupplementCardComponent {
  readonly supplement = input.required<SupplementDto>()
  /** Today page: a round button to tick it. */
  readonly tick = input(false)
  readonly busy = input(false)
  readonly taken = output<SupplementDto>()
  readonly euro = euro
  readonly status = computed(() => statusLabel(this.supplement()))
  readonly meta = computed(() => {
    const s = this.supplement()
    return `${doseLabel(s)} · ${momentLabel(s.moment)}${this.tick() ? '' : ' · ' + this.status()}`
  })
}
