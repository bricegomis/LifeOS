import { Component, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { HealthImageComponent } from './health-image.component'
import { HealthApiService } from './health-api.service'
import type { SupplementDayDto, SupplementDto } from './health.models'
import { SupplementActionsService } from './supplement-actions.service'
import { SupplementDialogComponent, type SupplementDialogData, type SupplementDialogResult } from './supplement-dialog.component'
import { dayOfMonth, decimal, doseLabel, euro, momentLabel, periodLabel, statusLabel, unitLabel, unitPrice, weekday } from './supplement-rules'

/** One supplement (ADR 0018): tick of the day, cost, streak, the last 14 days, package, period and purchase links. */
@Component({
  selector: 'app-supplement-page',
  standalone: true,
  imports: [HealthImageComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page detail-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (supplement(); as s) {
        <header class="head">
          <a mat-icon-button routerLink="/health/supplements" aria-label="Retour aux compléments"><mat-icon svgIcon="arrow_back" /></a>
          <h1 class="lo-bar-title head-title">Complément</h1>
          <button mat-flat-button type="button" class="lo-desktop-only" (click)="edit(s)"><mat-icon svgIcon="edit" />Modifier</button>
          <button mat-icon-button type="button" class="lo-phone-only" (click)="edit(s)" aria-label="Modifier le complément" title="Modifier le complément"><mat-icon svgIcon="edit" /></button>
        </header>
        <div class="hero">
          <app-health-image [path]="s.hasImage ? '/health/supplements/' + s.id + '/image' : null" [name]="s.name" [version]="s.updatedAt" [size]="96"
            [alt]="s.hasImage ? 'Photo : ' + s.name : 'Pas de photo'" />
          <div class="hero-text">
            <h2 class="hero-title">{{ s.name }}</h2>
            <span class="lo-muted">{{ doseLabel(s) }} · {{ momentLabel(s.moment) }}</span>
            <span class="lo-tags"><span class="lo-tag" [class.is-ok]="s.status.active">{{ statusLabel(s) }}</span></span>
          </div>
        </div>
        <div class="actions">
          @if (s.isStopped) {
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="resume(s)"><mat-icon svgIcon="play_arrow" />Reprendre</button>
          } @else if (s.status.active) {
            <button [class.tonal]="s.takenToday" mat-flat-button type="button" [disabled]="busy()" (click)="toggle(s)">
              <mat-icon [svgIcon]="s.takenToday ? 'check_circle' : 'radio_button_unchecked'" />{{ s.takenToday ? 'Pris aujourd’hui' : 'Marquer comme pris aujourd’hui' }}</button>
          }
        </div>
        <div class="lo-stats">
          <div class="lo-stat"><span class="lo-stat-value">{{ s.monthlyCost === null ? '—' : euro(s.monthlyCost) }}</span><span class="lo-stat-label">par mois</span></div>
          <div class="lo-stat"><span class="lo-stat-value">{{ s.streak }} j</span><span class="lo-stat-label">série en cours</span></div>
          <div class="lo-stat"><span class="lo-stat-value">{{ s.status.daysLeft ? s.status.daysLeft + ' j' : '—' }}</span><span class="lo-stat-label">restants dans la cure</span></div>
        </div>
        <section class="lo-section" aria-labelledby="days">
          <h2 id="days">14 derniers jours</h2>
          <div class="days" role="list">
            @for (d of s.recentDays; track d.day) {
              <button type="button" role="listitem" class="day" [class.is-taken]="d.taken" [class.is-off]="!d.active && !d.taken" [disabled]="busy() || s.isStopped"
                [attr.aria-label]="dayLabel(d)" [attr.title]="dayLabel(d)" (click)="setDay(s, d)">
                <span class="day-name">{{ weekday(d.day) }}</span><span class="day-num">{{ dayOfMonth(d.day) }}</span>
              </button>
            }
          </div>
          <p class="lo-help">Touche un jour pour le cocher ou le décocher.</p>
        </section>
        <section class="lo-card details" aria-label="Détails">
          <div class="detail"><mat-icon svgIcon="medication" /><div><span class="lo-field-label">Dose</span><span>{{ doseLabel(s) }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="schedule" /><div><span class="lo-field-label">Moment</span><span>{{ momentLabel(s.moment) }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="sell" /><div><span class="lo-field-label">Conditionnement</span>
            <span>{{ decimal(s.packageContent) }} {{ unitLabel(s.unit, s.packageContent) }}{{ s.packagePrice !== null ? ' pour ' + euro(s.packagePrice) + ' (' + unitPrice(s) + ')' : '' }}</span></div></div>
          @if (s.shop) { <div class="detail"><mat-icon svgIcon="storefront" /><div><span class="lo-field-label">Acheté chez</span><span>{{ s.shop }}</span></div></div> }
          <div class="detail"><mat-icon svgIcon="event_repeat" /><div><span class="lo-field-label">Période</span><span>{{ periodLabel(s) }}</span></div></div>
          @if (s.notes) { <div class="detail"><mat-icon svgIcon="notes" /><div><span class="lo-field-label">Notes</span><span class="notes">{{ s.notes }}</span></div></div> }
        </section>
        <section class="lo-section" aria-labelledby="links">
          <h2 id="links">Où l’acheter</h2>
          @if (s.links.length) {
            <div class="lo-card">
              @for (link of s.links; track link.url) {
                <a class="link" [href]="link.url" target="_blank" rel="noopener">
                  <mat-icon svgIcon="shopping_cart" /><span class="link-text"><strong>{{ link.label }}</strong><span class="lo-muted">{{ host(link.url) }}</span></span>
                  <mat-icon svgIcon="open_in_new" />
                </a>
              }
            </div>
          } @else {
            <p class="lo-muted">Aucun lien. Ajoute-en dans la fenêtre du complément.</p>
          }
        </section>
      } @else if (!error()) {
        <p class="lo-muted" role="status">Chargement…</p>
      }
    </div>
  `,
  styles: [`
    .detail-page { max-width: 880px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .hero { display: flex; align-items: center; gap: 16px; }
    .hero-text { display: flex; min-width: 0; flex-direction: column; gap: 6px; }
    .hero-title { margin: 0; font: var(--mat-sys-title-large); font-weight: 600; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .tonal { background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
    .lo-stat { display: flex; flex-direction: column; }
    .days { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 6px; }
    .day { display: flex; flex-direction: column; align-items: center; gap: 2px; padding: 6px 0; border: 1px solid var(--mat-sys-outline); border-radius: 12px;
      background: transparent; color: var(--mat-sys-on-surface); cursor: pointer; }
    .day.is-taken { border-color: var(--mat-sys-primary); background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); }
    .day.is-off { border-color: var(--mat-sys-outline-variant); color: var(--mat-sys-outline); }
    .day-name { font: var(--mat-sys-label-small); }
    .day-num { font: var(--mat-sys-title-small); }
    .details { display: flex; flex-direction: column; }
    .detail { display: flex; align-items: flex-start; gap: 12px; padding: 12px 16px; }
    .detail + .detail { border-top: 1px solid var(--mat-sys-outline-variant); }
    .detail mat-icon { flex: 0 0 auto; color: var(--mat-sys-on-surface-variant); }
    .detail > div { display: flex; min-width: 0; flex-direction: column; }
    .notes { white-space: pre-line; }
    .link { display: flex; align-items: center; gap: 12px; min-height: 56px; padding: 8px 16px; color: var(--mat-sys-primary); text-decoration: none; }
    .link + .link { border-top: 1px solid var(--mat-sys-outline-variant); }
    .link-text { display: flex; min-width: 0; flex: 1 1 auto; flex-direction: column; }
  `],
})
export class SupplementPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly actions = inject(SupplementActionsService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? ''
  readonly supplement = signal<SupplementDto | null>(null)
  readonly error = signal('')
  readonly busy = signal(false)
  readonly doseLabel = doseLabel
  readonly momentLabel = momentLabel
  readonly statusLabel = statusLabel
  readonly periodLabel = periodLabel
  readonly unitLabel = unitLabel
  readonly unitPrice = unitPrice
  readonly euro = euro
  readonly decimal = decimal
  readonly weekday = weekday
  readonly dayOfMonth = dayOfMonth

  async ngOnInit(): Promise<void> {
    try { this.supplement.set(await firstValueFrom(this.api.supplement(this.id))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }

  host(url: string): string { try { return new URL(url).host.replace(/^www\./, '') } catch { return url } }
  dayLabel(d: SupplementDayDto): string { return `${weekday(d.day)} ${dayOfMonth(d.day)} : ${d.taken ? 'pris' : 'non pris'}` }

  async toggle(s: SupplementDto): Promise<void> {
    this.busy.set(true)
    const updated = await this.actions.toggle(s)
    if (updated) this.supplement.set(updated)
    this.busy.set(false)
  }

  async setDay(s: SupplementDto, d: SupplementDayDto): Promise<void> {
    this.busy.set(true)
    try { this.supplement.set(await firstValueFrom(this.api.setTaken(s.id, !d.taken, d.day))) }
    catch (error) { this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 }) }
    finally { this.busy.set(false) }
  }

  async resume(s: SupplementDto): Promise<void> {
    this.busy.set(true)
    try { this.supplement.set(await firstValueFrom(this.api.resumeSupplement(s.id))); this.snack.open('Complément repris', undefined, { duration: 3000 }) }
    catch (error) { this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 }) }
    finally { this.busy.set(false) }
  }

  async edit(s: SupplementDto): Promise<void> {
    const data: SupplementDialogData = { supplement: s, today: s.recentDays[s.recentDays.length - 1]?.day ?? new Date().toISOString().slice(0, 10) }
    const result = await firstValueFrom(openEditor<SupplementDialogComponent, SupplementDialogData, SupplementDialogResult>(this.dialog, SupplementDialogComponent, data).afterClosed())
    if (!result) return
    this.supplement.set(result.supplement)
    this.snack.open(result.message, undefined, { duration: 4000 })
  }
}
