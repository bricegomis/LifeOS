import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { SessionEditorComponent, type SessionEditorData } from './session-editor.component'
import { decimal, duration, integer, longDay, sessionPace, sessionTitle, timeRange, zoneRows } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { SportOption, SportSessionDto, SportTemplateDto } from './sports.models'

const SPORT_ICONS: Record<string, string> = {
  running: 'directions_run', walking: 'directions_walk', hiking: 'hiking', cycling: 'directions_bike', swimming: 'pool',
  crossfit: 'sports_gymnastics', strength: 'fitness_center', bodyweight: 'fitness_center', mobility: 'self_improvement',
}

/** One session (ADR 0019): the watch's values, heart rate zones, effort and feeling, and the LifeOS notes. */
@Component({
  selector: 'app-sport-session-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page is-narrow">
      <a mat-button class="lo-back" routerLink="/sports/sessions"><mat-icon svgIcon="arrow_back" />Séances</a>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (session(); as s) {
        <header class="lo-page-head">
          <div class="head">
            <span class="lo-lead-icon big" aria-hidden="true"><mat-icon [svgIcon]="icon(s.sport)" /></span>
            <div class="lo-page-title">
              <h1>{{ title() }}</h1>
              <span class="lo-muted">{{ longDay(s.date) }}, {{ timeRange(s) }}</span>
              <div class="lo-tags">
                <span class="lo-tag" [class.is-ok]="s.source === 'garmin'"><mat-icon svgIcon="watch" />{{ s.source === 'garmin' ? 'Garmin' : 'Saisie à la main' }}</span>
                @if (s.garminType && s.sportOverridden) { <span class="lo-tag">Type Garmin : {{ s.garminType }}</span> }
              </div>
            </div>
          </div>
          <button mat-stroked-button type="button" [disabled]="!ready()" (click)="edit()"><mat-icon svgIcon="edit" />Modifier</button>
        </header>

        <div class="lo-stats">
          <div class="lo-stat"><div class="lo-stat-label">Durée active</div><div class="lo-stat-value">{{ duration(s.durationMinutes) }}</div></div>
          @if (s.distanceKm !== null) { <div class="lo-stat"><div class="lo-stat-label">Distance</div><div class="lo-stat-value">{{ decimal(s.distanceKm, 2) }} km</div></div> }
          @if (pace()) { <div class="lo-stat"><div class="lo-stat-label">{{ pace().split(' : ')[0] }}</div><div class="lo-stat-value">{{ pace().split(' : ')[1] }}</div></div> }
          @if (s.elevationGainMeters !== null) { <div class="lo-stat"><div class="lo-stat-label">Dénivelé +</div><div class="lo-stat-value">{{ s.elevationGainMeters }} m</div></div> }
          <div class="lo-stat"><div class="lo-stat-label">Calories</div><div class="lo-stat-value">{{ s.calories === null ? '?' : integer(s.calories) + ' kcal' }}</div></div>
          @if (s.averageHeartRate !== null) {
            <div class="lo-stat"><div class="lo-stat-label">FC moyenne</div><div class="lo-stat-value">{{ s.averageHeartRate }} bpm</div>
              @if (s.maxHeartRate !== null) { <div class="lo-help">max {{ s.maxHeartRate }} bpm</div> }</div>
          }
        </div>

        @if (zones().length) {
          <section class="lo-panel" aria-labelledby="zones-title">
            <h2 id="zones-title">Temps par zone de fréquence cardiaque</h2>
            @for (zone of zones(); track zone.label) {
              <div class="zone-row">
                <span class="zone-label">{{ zone.label }}</span>
                <span class="zone-track"><span class="zone-bar" [class]="'zone-bar z' + ($index + 1)" [style.width.%]="zone.percent"></span></span>
                <strong>{{ zone.minutes }} min</strong>
              </div>
            }
          </section>
        }

        <section class="lo-panel" aria-labelledby="feel-title">
          <h2 id="feel-title">Effort et ressenti</h2>
          <div class="lo-stats">
            <div class="lo-stat"><div class="lo-stat-label">Effort perçu</div><div class="lo-stat-value">{{ s.effort === null ? '—' : s.effort + ' / 10' }}</div></div>
            <div class="lo-stat"><div class="lo-stat-label">Sensation</div><div class="lo-stat-value">{{ s.feelLabel ?? '—' }}</div></div>
            @if (s.aerobicTrainingEffect !== null) {
              <div class="lo-stat"><div class="lo-stat-label">Training Effect</div><div class="lo-stat-value">{{ decimal(s.aerobicTrainingEffect) }}</div>
                @if (s.anaerobicTrainingEffect) { <div class="lo-help">anaérobie {{ decimal(s.anaerobicTrainingEffect) }}</div> }</div>
            }
          </div>
          @if (s.source === 'garmin' && s.effort === null) { <p class="lo-help">Non renseignés sur la montre en fin de séance.</p> }
        </section>

        <section class="lo-panel" aria-labelledby="notes-title">
          <div class="lo-panel-head"><h2 id="notes-title">Notes LifeOS</h2></div>
          <p [class.lo-muted]="!s.notes" class="notes">{{ s.notes || 'Aucune note.' }}</p>
          <p class="lo-help">{{ s.source === 'garmin'
            ? 'Les valeurs viennent de Garmin et suivent ses modifications à chaque synchro ; tes notes, la séance type et un sport changé ici ne sont jamais écrasés. Supprimer l’activité dans Garmin Connect la supprime aussi ici.'
            : 'Si la montre envoie la même séance (même sport, horaires qui se chevauchent), les deux fusionnent : valeurs Garmin, tes notes gardées.' }}</p>
        </section>
      } @else if (!error()) { <p class="lo-status" role="status">Chargement…</p> }
    </div>
  `,
  styleUrl: './sports.css',
  styles: [`
    .head { display: flex; min-width: 0; align-items: center; gap: 16px; }
    .lo-lead-icon.big { width: 64px; height: 64px; border-radius: 16px; }
    .lo-lead-icon.big mat-icon { width: 36px; height: 36px; }
    .lo-tag mat-icon { width: 16px; height: 16px; }
    .notes { margin: 0; white-space: pre-line; }
    .zone-row { display: flex; align-items: center; gap: 12px; }
    .zone-label { flex: 0 0 64px; font: var(--mat-sys-label-large); }
    .zone-track { flex: 1 1 auto; height: 14px; overflow: hidden; border-radius: 4px; background: var(--mat-sys-surface-container-high); }
    .zone-bar { display: block; height: 100%; }
    .zone-bar.z1 { background: color-mix(in srgb, var(--mat-sys-primary) 25%, transparent); }
    .zone-bar.z2 { background: color-mix(in srgb, var(--mat-sys-primary) 50%, transparent); }
    .zone-bar.z3 { background: var(--mat-sys-primary); }
    .zone-bar.z4 { background: var(--mat-sys-tertiary); }
    .zone-bar.z5 { background: var(--mat-sys-error); }
    .zone-row strong { flex: 0 0 56px; text-align: right; }
  `],
})
export class SportSessionPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly route = inject(ActivatedRoute)
  private readonly router = inject(Router)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly session = signal<SportSessionDto | null>(null)
  readonly error = signal('')
  readonly ready = signal(false)
  private sports: SportOption[] = []
  private templates: SportTemplateDto[] = []
  private today = ''
  readonly title = computed(() => { const s = this.session(); return s ? sessionTitle(s) : '' })
  readonly pace = computed(() => { const s = this.session(); return s ? sessionPace(s) : '' })
  readonly zones = computed(() => zoneRows(this.session()?.heartRateZoneSeconds ?? null))

  readonly duration = duration
  readonly decimal = decimal
  readonly integer = integer
  readonly longDay = longDay
  readonly timeRange = timeRange

  icon(sport: string): string { return SPORT_ICONS[sport] ?? 'sports' }

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id') ?? ''
    try {
      const [session, catalog, templates] = await Promise.all([firstValueFrom(this.api.session(id)), firstValueFrom(this.api.catalog()), firstValueFrom(this.api.templates())])
      this.session.set(session); this.sports = catalog.sports; this.today = catalog.today; this.templates = templates
      this.ready.set(true)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
  }

  async edit(): Promise<void> {
    const session = this.session()
    if (!session) return
    const message = await firstValueFrom(openEditor<SessionEditorComponent, SessionEditorData, string>(this.dialog, SessionEditorComponent,
      { session, templates: this.templates, sports: this.sports, today: this.today }).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    if (message === 'Séance supprimée') { await this.router.navigateByUrl('/sports/sessions'); return }
    try { this.session.set(await firstValueFrom(this.api.session(session.id))) } catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
