import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { SessionEditorComponent, type SessionEditorData } from './session-editor.component'
import { dayMonth, decimal, duration, integer, sessionCalories, sessionDetails, sessionTitle, shortDay } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { SportOption, SportSessionDto, SportTemplateDto, WeekReportDto } from './sports.models'

const STEP = 6

/** Sessions grouped by week, each week headed by its report. */
@Component({
  selector: 'app-sport-history-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="lo-page">
      <header class="lo-page-head">
        <h1 class="lo-bar-title">Historique</h1>
        <button mat-flat-button type="button" class="lo-desktop-only" [disabled]="!today()" (click)="openEditor(null)"><mat-icon svgIcon="add" />Ajouter une séance</button>
        <button mat-fab type="button" class="lo-fab lo-phone-only" [disabled]="!today()" (click)="openEditor(null)"
          aria-label="Ajouter une séance" title="Ajouter une séance"><mat-icon svgIcon="add" /></button>
      </header>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (loading() && !weeks().length) { <p class="lo-status" role="status">Chargement…</p> }
      @for (week of newestFirst(); track week.monday; let first = $first) {
        <section class="sport-section" [attr.aria-label]="weekTitle(week, first)">
          <div class="sport-section-head">
            <h2>{{ weekTitle(week, first) }}</h2>
            <span class="lo-muted lo-small">{{ summary(week) }}</span>
          </div>
          @if (week.goals.length) {
            <p class="lo-small" [class.lo-muted]="week.goalsReached < week.goals.length"><strong>Objectifs : {{ week.goalsReached }} sur {{ week.goals.length }} atteints</strong></p>
          }
          @if (week.sessions.length) {
            <div class="session-list">
              @for (session of week.sessions; track session.id) {
                <button type="button" class="session-row" (click)="openEditor(session)">
                  <span class="session-day">{{ shortDay(session.date) }}</span>
                  <span class="session-text"><strong>{{ sessionTitle(session) }}</strong><span class="lo-muted lo-small">{{ sessionDetails(session) }}</span>
                    @if (session.notes) { <span class="notes">{{ session.notes }}</span> }</span>
                  <span class="session-kcal" [class.is-unknown]="session.calories === null">{{ sessionCalories(session) }}</span>
                </button>
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

  more(): Promise<void> { this.count += STEP; return this.load() }

  async openEditor(session: SportSessionDto | null): Promise<void> {
    const message = await firstValueFrom(openEditor<SessionEditorComponent, SessionEditorData, string>(this.dialog, SessionEditorComponent,
      { session, templates: this.templates(), sports: this.sports(), today: this.today() }).afterClosed())
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
