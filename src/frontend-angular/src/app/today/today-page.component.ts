import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { hrvText, sleepText, stepsText, type TodayNight } from './today-rules'

interface TodayWellness extends TodayNight { date: string }
interface TodaySession { id: string, sportLabel: string, startedAt: string, endedAt: string, durationMinutes: number, distanceKm: number | null, source: string, garminName: string | null }
interface TodayGoal { goal: { id: string, label: string, kindLabel: string, target: number }, achieved: number, reached: boolean }
interface TodayWeek { monday: string, sunday: string, goals: TodayGoal[] }
interface TodayGarmin { state: string, stateLabel: string, waitsForCode: boolean, lastSuccessAt: string | null }

/**
 * The day at a glance (ADR 0019): last night, energy, today's sessions and steps, the week's goals.
 * A shell page that reads the Sport and Health endpoints; it becomes the planner's home (ADR 0009).
 */
@Component({
  selector: 'app-today-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page">
      <header class="lo-page-head"><h1 class="lo-bar-title">Aujourd’hui</h1></header>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (garminWarning(); as warning) {
        <p class="lo-notice warn" role="status"><mat-icon svgIcon="mark_email_unread" /><span>{{ warning }}</span>
          <a mat-flat-button routerLink="/sports/garmin">{{ garmin()?.waitsForCode ? 'Saisir le code' : 'Voir' }}</a></p>
      }
      <section class="lo-section" aria-labelledby="today-night">
        <div class="lo-section-head"><h2 id="today-night">Cette nuit</h2><a mat-button routerLink="/health/sleep"><mat-icon svgIcon="bedtime" />Sommeil</a></div>
        <div class="lo-stats">
          <div class="lo-stat"><div class="lo-stat-label">Sommeil</div><div class="lo-stat-value">{{ sleep().value }}</div><div class="lo-help">{{ sleep().sub }}</div></div>
          <div class="lo-stat"><div class="lo-stat-label">Body Battery au réveil</div><div class="lo-stat-value">{{ night()?.bodyBatteryWake ?? '—' }}</div></div>
          <div class="lo-stat"><div class="lo-stat-label">HRV</div><div class="lo-stat-value">{{ hrv().value }}</div><div class="lo-help">{{ hrv().sub }}</div></div>
          <div class="lo-stat"><div class="lo-stat-label">FC au repos</div><div class="lo-stat-value">{{ night()?.restingHeartRate ? night()!.restingHeartRate + ' bpm' : '—' }}</div></div>
        </div>
      </section>

      <section class="lo-section" aria-labelledby="today-sessions">
        <div class="lo-section-head"><h2 id="today-sessions">Séances du jour</h2><a mat-button routerLink="/sports/sessions"><mat-icon svgIcon="history" />Séances</a></div>
        @if (sessions().length) {
          <div class="lo-list">
            @for (session of sessions(); track session.id) {
              <a class="lo-row lo-row-main" [routerLink]="['/sports/session', session.id]">
                <span class="lo-row-text"><span class="lo-row-title">{{ session.sportLabel }}{{ session.garminName && session.garminName !== session.sportLabel ? ' · ' + session.garminName : '' }}</span>
                  <span class="lo-row-sub">{{ time(session.startedAt) }} – {{ time(session.endedAt) }} · {{ session.durationMinutes }} min{{ session.distanceKm ? ' · ' + km(session.distanceKm) + ' km' : '' }}</span></span>
                @if (session.source === 'garmin') { <mat-icon svgIcon="watch" aria-label="Garmin" /> }
              </a>
            }
          </div>
        } @else {
          <p class="lo-empty">Aucune séance aujourd’hui. Celles de la montre arrivent à la prochaine synchro Garmin.</p>
        }
      </section>

      @if (week(); as w) {
        <section class="lo-section" aria-labelledby="today-goals">
          <div class="lo-section-head"><h2 id="today-goals">Objectifs de la semaine</h2><a mat-button routerLink="/sports/goals"><mat-icon svgIcon="flag" />Objectifs</a></div>
          @if (w.goals.length) {
            <div class="lo-list goals">
              @for (progress of w.goals; track progress.goal.id) {
                <div class="goal">
                  <div class="goal-head"><strong>{{ progress.goal.label }}</strong><span [class.done]="progress.reached">{{ amount(progress) }}</span></div>
                  <div class="bar"><span [style.width.%]="percent(progress)"></span></div>
                </div>
              }
            </div>
          } @else { <p class="lo-empty">Aucun objectif cette semaine.</p> }
        </section>
      }

      <section class="lo-section" aria-labelledby="today-day">
        <h2 id="today-day">Journée</h2>
        <div class="lo-stats">
          <div class="lo-stat"><div class="lo-stat-label">Pas</div><div class="lo-stat-value">{{ steps() }}</div></div>
          <div class="lo-stat"><div class="lo-stat-label">Stress moyen</div><div class="lo-stat-value">{{ day()?.stressAverage ?? '—' }}</div></div>
        </div>
        @if (garmin(); as g) { <p class="lo-help">Synchro Garmin : {{ g.stateLabel.toLowerCase() }}{{ g.lastSuccessAt ? ', dernière réussie à ' + time(g.lastSuccessAt) : '' }}.</p> }
      </section>
    </div>
  `,
  styles: [`
    .lo-notice.warn { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; }
    .lo-notice.warn span { flex: 1 1 200px; }
    .lo-row-main { gap: 12px; color: inherit; text-decoration: none; }
    .goals { display: flex; flex-direction: column; padding: 4px 16px; }
    .goal { display: flex; flex-direction: column; gap: 6px; padding: 12px 0; }
    .goal + .goal { border-top: 1px solid var(--mat-sys-outline-variant); }
    .goal-head { display: flex; justify-content: space-between; gap: 8px; }
    .goal-head .done { color: var(--mat-sys-primary); font-weight: 700; }
    .bar { height: 8px; overflow: hidden; border-radius: 4px; background: var(--mat-sys-secondary-container); }
    .bar span { display: block; height: 100%; background: var(--mat-sys-primary); }
  `],
})
export class TodayPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)
  readonly wellness = signal<TodayWellness[]>([])
  readonly sessions = signal<TodaySession[]>([])
  readonly week = signal<TodayWeek | null>(null)
  readonly garmin = signal<TodayGarmin | null>(null)
  readonly error = signal('')
  private readonly today = new Intl.DateTimeFormat('fr-CA', { timeZone: 'Europe/Paris' }).format(new Date())
  readonly night = computed(() => this.wellness().find(day => day.date === this.today) ?? null)
  readonly day = computed(() => this.night() ?? this.wellness().at(-1) ?? null)
  readonly sleep = computed(() => sleepText(this.night()))
  readonly hrv = computed(() => hrvText(this.night()))
  readonly steps = computed(() => stepsText(this.night()?.steps ?? null))
  readonly garminWarning = computed(() => {
    const g = this.garmin()
    if (!g || g.state === 'ok' || g.state === 'never') return ''
    return g.waitsForCode ? 'Garmin attend un code pour reprendre la synchro.' : `Garmin : ${g.stateLabel.toLowerCase()}.`
  })

  time(instant: string): string {
    return new Intl.DateTimeFormat('fr-FR', { timeZone: 'Europe/Paris', hour: 'numeric', minute: '2-digit' }).format(new Date(instant)).replace(':', ' h ')
  }
  km(value: number): string { return value.toFixed(2).replace('.', ',') }
  amount(progress: TodayGoal): string { return `${Math.round(progress.achieved * 100) / 100} / ${progress.goal.target}` }
  percent(progress: TodayGoal): number { return Math.min(100, Math.round(progress.achieved * 100 / progress.goal.target)) }

  async ngOnInit(): Promise<void> {
    const yesterday = new Intl.DateTimeFormat('fr-CA', { timeZone: 'Europe/Paris' }).format(new Date(Date.now() - 86_400_000))
    const load = async <T>(call: () => Promise<T>, set: (value: T) => void) => {
      try { set(await call()) } catch (error) { this.error.set(apiErrorMessage(error)) }
    }
    await Promise.all([
      load(() => firstValueFrom(this.api.get<TodayWellness[]>('/health/wellness', { from: yesterday, to: this.today })), value => this.wellness.set(value)),
      load(() => firstValueFrom(this.api.get<TodaySession[]>('/sport/sessions', { from: this.today, to: this.today })), value => this.sessions.set(value)),
      load(() => firstValueFrom(this.api.get<TodayWeek[]>('/sport/weeks', { count: 1 })), value => this.week.set(value.at(-1) ?? null)),
      load(() => firstValueFrom(this.api.get<TodayGarmin>('/garmin')), value => this.garmin.set(value)),
    ])
  }
}
