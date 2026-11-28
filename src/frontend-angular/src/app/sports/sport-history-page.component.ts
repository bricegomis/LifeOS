import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { SessionEditorComponent, type SessionEditorData } from './session-editor.component'
import { dayMonth, decimal, duration, integer, sessionCalories, sessionDetails, sessionTitle, shortDay } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { SportOption, SportSessionDto, SportTemplateDto, WeekReportDto } from './sports.models'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

const STEP = 6

/** Sessions grouped by week, each week headed by its report, filtered by sport; a session opens its page (ADR 0019). */
@Component({
  selector: 'app-sport-history-page',
  standalone: true,
  imports: [SkeletonComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page">
      <header class="lo-page-head">
        <h1 class="lo-bar-title">Séances</h1>
        <button mat-flat-button type="button" class="lo-desktop-only" [disabled]="!today()" (click)="openEditor(null)"><mat-icon svgIcon="add" />Ajouter une séance</button>
        <button mat-fab type="button" class="lo-fab lo-phone-only" [disabled]="!today()" (click)="openEditor(null)"
          aria-label="Ajouter une séance" title="Ajouter une séance"><mat-icon svgIcon="add" /></button>
      </header>
      <div class="lo-chips" role="group" aria-label="Sport">
        @for (option of sportFilters(); track option.code) {
          <button type="button" class="lo-filter-chip" [class.is-on]="sport() === option.code" [attr.aria-pressed]="sport() === option.code"
            (click)="sport.set(option.code)">@if (sport() === option.code) { <mat-icon svgIcon="check" /> }{{ option.label }}</button>
        }
      </div>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (loading() && !weeks().length) { <app-skeleton /> }
      @for (week of newestFirst(); track week.monday; let first = $first) {
        <section class="sport-section" [attr.aria-label]="weekTitle(week, first)">
          <div class="sport-section-head">
            <h2>{{ weekTitle(week, first) }}</h2>
            <span class="lo-muted lo-small">{{ summary(week) }}</span>
          </div>
          @if (week.goals.length) {
            <p class="lo-small" [class.lo-muted]="week.goalsReached < week.goals.length"><strong>Objectifs : {{ week.goalsReached }} sur {{ week.goals.length }} atteints</strong></p>
          }
          @if (shown(week).length) {
            <div class="session-list">
              @for (session of shown(week); track session.id) {
                <a class="session-row" [routerLink]="['/sports/session', session.id]">
                  <span class="session-day">{{ shortDay(session.date) }}</span>
                  <span class="session-text"><strong>{{ sessionTitle(session) }}</strong><span class="lo-muted lo-small">{{ sessionDetails(session) }}</span>
                    @if (session.notes) { <span class="notes">{{ session.notes }}</span> }</span>
                  <span class="session-side">
                    <span class="session-kcal" [class.is-unknown]="session.calories === null">{{ sessionCalories(session) }}</span>
                    @if (session.source === 'garmin') { <span class="lo-muted lo-small source"><mat-icon svgIcon="watch" />Garmin</span> }
                  </span>
                </a>
              }
            </div>
          } @else {
            <p class="lo-muted lo-small">Aucune séance.</p>
          }
        </section>
      }
      @if (weeks().length) {
        <div><button mat-stroked-button type="button" [disabled]="loading()" (click)="more()"><mat-icon svgIcon="history" />Semaines précédentes</button></div>
      }
    </div>
  `,
  styleUrl: './sports.css',
})
export class SportHistoryPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly loading = signal(true)
  readonly error = signal('')
  readonly today = signal('')
  readonly sports = signal<SportOption[]>([])
  readonly templates = signal<SportTemplateDto[]>([])
  readonly weeks = signal<WeekReportDto[]>([])
  readonly newestFirst = computed(() => [...this.weeks()].reverse())
  readonly sport = signal('all')
  readonly sportFilters = computed(() => [{ code: 'all', label: 'Tous' },
    ...this.sports().filter(sport => this.weeks().some(week => week.sessions.some(session => session.sport === sport.code)))])
  private count = STEP

  readonly shortDay = shortDay
  readonly sessionTitle = sessionTitle
  readonly sessionDetails = sessionDetails
  readonly sessionCalories = sessionCalories

  async ngOnInit(): Promise<void> {
    try {
      const [catalog, templates] = await Promise.all([firstValueFrom(this.api.catalog()), firstValueFrom(this.api.templates())])
      this.today.set(catalog.today); this.sports.set(catalog.sports); this.templates.set(templates)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    await this.load()
  }

  weekTitle(week: WeekReportDto, first: boolean): string { return first ? 'Cette semaine' : `Semaine du ${dayMonth(week.monday)}` }
  summary(week: WeekReportDto): string {
    const sessions = `${week.sessionCount} séance${week.sessionCount > 1 ? 's' : ''}`
    const calories = `${integer(week.calories)} kcal${week.caloriesComplete ? '' : ' (partiel)'}`
    return [sessions, calories, duration(week.durationMinutes), `${decimal(week.distanceKm)} km`].join(' · ')
  }

  shown(week: WeekReportDto): SportSessionDto[] {
    const sport = this.sport()
    return sport === 'all' ? week.sessions : week.sessions.filter(session => session.sport === sport)
  }

  more(): Promise<void> { this.count += STEP; return this.load() }

  async openEditor(session: SportSessionDto | null): Promise<void> {
    const message = await firstValueFrom(openEditor<SessionEditorComponent, SessionEditorData, string>(this.dialog, SessionEditorComponent,
      { session, templates: this.templates(), sports: this.sports(), today: this.today() }).afterClosed())
    // Adding happens here; a session is edited from its own page.
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    this.loading.set(true)
    try { this.weeks.set(await firstValueFrom(this.api.weeks(this.count))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }
}
