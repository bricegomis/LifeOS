import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { SessionEditorComponent } from './session-editor.component'
import { SportNavComponent } from './sport-nav.component'
import { dayMonth, decimal, duration, integer, sessionCalories, sessionDetails, sessionTitle, shortDay } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { SportOption, SportSessionDto, SportTemplateDto, WeekReportDto } from './sports.models'

const STEP = 6

/** Sessions grouped by week, each week headed by its report. */
@Component({
  selector: 'app-sport-history-page',
  standalone: true,
  imports: [SessionEditorComponent, SportNavComponent],
  template: `
    <div class="page">
      <app-sport-nav />
      <div class="page-header">
        <h1>Historique</h1>
        <button type="button" class="lifeos-button" [disabled]="!today()" (click)="openEditor(null)"><i class="pi pi-plus" aria-hidden="true"></i> Ajouter une séance</button>
      </div>
      @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
      @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
      @if (loading() && !weeks().length) { <p class="status-text" role="status">Chargement…</p> }
      @for (week of newestFirst(); track week.monday; let first = $first) {
        <section class="sport-section" [attr.aria-label]="weekTitle(week, first)">
          <div class="sport-section-head">
            <h2>{{ weekTitle(week, first) }}</h2>
            <span class="muted small">{{ summary(week) }}</span>
          </div>
          @if (week.goals.length) {
            <p class="small" [class.muted]="week.goalsReached < week.goals.length"><strong>Objectifs : {{ week.goalsReached }} sur {{ week.goals.length }} atteints</strong></p>
          }
          @if (week.sessions.length) {
            <div class="session-list">
              @for (session of week.sessions; track session.id) {
                <button type="button" class="session-row" (click)="openEditor(session)">
                  <span class="session-day">{{ shortDay(session.date) }}</span>
                  <span class="session-text"><strong>{{ sessionTitle(session) }}</strong><span class="muted small">{{ sessionDetails(session) }}</span>
                    @if (session.notes) { <span class="notes">{{ session.notes }}</span> }</span>
                  <span class="session-kcal" [class.is-unknown]="session.calories === null">{{ sessionCalories(session) }}</span>
                </button>
              }
            </div>
          } @else {
            <p class="muted small">Aucune séance.</p>
          }
        </section>
      }
      @if (weeks().length) {
        <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="loading()" (click)="more()">Semaines précédentes</button>
      }
    </div>
    @if (today()) {
      <app-session-editor [open]="editorOpen()" [session]="editing()" [templates]="templates()" [sports]="sports()" [today]="today()"
        (saved)="onSaved($event)" (dismissed)="editorOpen.set(false)" />
    }
  `,
  styleUrl: './sports.css',
  styles: [`.page > .lifeos-button { align-self: flex-start; }`],
})
export class SportHistoryPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  readonly loading = signal(true)
  readonly error = signal('')
  readonly notice = signal('')
  readonly today = signal('')
  readonly sports = signal<SportOption[]>([])
  readonly templates = signal<SportTemplateDto[]>([])
  readonly weeks = signal<WeekReportDto[]>([])
  readonly newestFirst = computed(() => [...this.weeks()].reverse())
  readonly editorOpen = signal(false)
  readonly editing = signal<SportSessionDto | null>(null)
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

  openEditor(session: SportSessionDto | null): void { this.editing.set(session); this.editorOpen.set(true) }

  async onSaved(message: string): Promise<void> {
    this.editorOpen.set(false)
    this.notice.set(message)
    await this.load()
  }

  private async load(): Promise<void> {
    this.loading.set(true)
    try { this.weeks.set(await firstValueFrom(this.api.weeks(this.count))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }
}
