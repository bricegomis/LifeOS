import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { RoutinesTodayComponent } from './routines-today.component'
import { SessionEditorComponent, type SessionEditorData } from './session-editor.component'
import {
  celebration, challengePercent, entryTime, goalPercent, goalProgressText, integer, jokerLine, longDay, quickSteps,
  sessionCalories, sessionDetails, sessionTitle, shortDay, streakLine, weekRange, decimal, duration,
} from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { ChallengeReportDto, SportOption, SportSessionDto, SportTemplateDto, WeekReportDto } from './sports.models'

/** Sport home (validated mockups): today's routines and challenges first, then the current week. */
@Component({
  selector: 'app-sport-today-page',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, RouterLink, RoutinesTodayComponent],
  template: `
    <div class="lo-page">
      <header class="lo-page-head">
        <div class="lo-page-title"><h1>Sport</h1>@if (today()) { <p class="lo-intro">{{ longDay(today()) }}</p> }</div>
        <button mat-flat-button type="button" [disabled]="!today()" (click)="openEditor(null)"><mat-icon svgIcon="add" />Ajouter une séance</button>
      </header>
      @if (banner()) {
        <div class="celebration" role="status"><mat-icon svgIcon="trophy" /><span>{{ banner() }}</span>
          <button mat-icon-button type="button" aria-label="Fermer le message" (click)="banner.set('')"><mat-icon svgIcon="close" /></button></div>
      }
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="lo-status" role="status">Chargement…</p> } @else {
        <app-routines-today />
        <section class="sport-section" aria-labelledby="sport-challenges">
          <div class="sport-section-head"><h2 id="sport-challenges">Défis du jour</h2>
            <a mat-button routerLink="/sports/challenges"><mat-icon svgIcon="tune" />Gérer</a></div>
          @if (!challenges().length) {
            <p class="lo-empty">Aucun défi. Ajoutez par exemple « 100 pompes par jour » sur la page Défis.</p>
          }
          <div class="challenge-grid">
            @for (report of challenges(); track report.challenge.id) {
              <article class="lo-card challenge-card" [attr.aria-labelledby]="'challenge-' + report.challenge.id">
                <div class="challenge-head">
                  <div><strong [id]="'challenge-' + report.challenge.id">{{ report.challenge.name }}</strong>
                    <span class="lo-muted lo-small">{{ streakLine(report) }}</span></div>
                  <span class="lo-tag" [class.is-done]="report.reachedToday">{{ report.reachedToday ? 'Atteint' : challengePercent(report) + ' %' }}</span>
                </div>
                <div class="challenge-count"><strong>{{ integer(report.todayTotal) }}</strong>
                  <span class="lo-muted">/ {{ integer(report.todayTarget) }} {{ unitNoun(report) }}</span></div>
                <div class="progress" role="progressbar" [attr.aria-valuenow]="challengePercent(report)" aria-valuemin="0" aria-valuemax="100"
                  [attr.aria-label]="'Avancement du défi ' + report.challenge.name">
                  <span [style.width.%]="challengePercent(report)" [class.is-done]="report.reachedToday"></span></div>
                <div class="quick-row">
                  @for (step of quickSteps(report.challenge.unit, report.todayTarget); track step) {
                    <button mat-stroked-button type="button" [disabled]="pending() === report.challenge.id" (click)="add(report, step)"
                      [attr.aria-label]="'Ajouter ' + step + ' à ' + report.challenge.name">+{{ step }}</button>
                  }
                </div>
                <form class="custom-row" (ngSubmit)="addCustom(report)">
                  <mat-form-field appearance="outline" subscriptSizing="dynamic">
                    <mat-label>Autre nombre</mat-label>
                    <input matInput type="number" inputmode="numeric" min="1" max="10000" [name]="'custom-' + report.challenge.id"
                      [attr.aria-label]="'Nombre libre pour ' + report.challenge.name" [(ngModel)]="custom[report.challenge.id]" />
                  </mat-form-field>
                  <button mat-stroked-button type="submit" [disabled]="pending() === report.challenge.id">Ajouter</button>
                </form>
                @if (report.todayEntries.length) {
                  <div class="entries-row lo-muted lo-small">
                    <span>{{ entriesLine(report) }}</span>
                    <button mat-button type="button" [disabled]="pending() === report.challenge.id" (click)="undo(report)"><mat-icon svgIcon="undo" />Retirer le dernier</button>
                  </div>
                }
                <span class="lo-muted lo-small">{{ jokerLine(report) }}</span>
              </article>
            }
          </div>
        </section>

        @if (week(); as week) {
          <div class="lo-grid-2">
            <section class="sport-section" aria-labelledby="sport-week">
              <div class="sport-section-head"><h2 id="sport-week">Cette semaine</h2><span class="lo-muted lo-small">{{ weekRange(week.monday, week.sunday) }}</span></div>
              <div class="lo-stats">
                <div class="lo-stat"><div class="lo-stat-label">Calories</div><div class="lo-stat-value">{{ integer(week.calories) }} kcal</div>
                  <div class="lo-stat-note" [class.is-partial]="!week.caloriesComplete">{{ caloriesNote(week) }}</div></div>
                <div class="lo-stat"><div class="lo-stat-label">Séances</div><div class="lo-stat-value">{{ week.sessionCount }}</div>
                  <div class="lo-muted lo-small">{{ duration(week.durationMinutes) }}</div></div>
                <div class="lo-stat"><div class="lo-stat-label">Distance</div><div class="lo-stat-value">{{ decimal(week.distanceKm) }} km</div></div>
              </div>
              @if (week.goals.length) {
                <div class="goal-list">
                  @for (progress of week.goals; track progress.goal.id) {
                    <div class="goal-row">
                      <div class="goal-row-head"><strong>{{ progress.goal.label }}</strong>
                        <span class="value" [class.is-done]="progress.reached">{{ goalProgressText(progress) }}</span></div>
                      <div class="progress progress-thin"><span [style.width.%]="goalPercent(progress)" [class.is-done]="progress.reached"></span></div>
                      <span class="lo-muted lo-small">{{ progress.goal.kindLabel }}{{ progress.reached ? ' · atteint' : '' }}</span>
                    </div>
                  }
                </div>
              } @else {
                <p class="lo-empty">Aucun objectif. Fixez par exemple « 2 footings » ou « 4 séances » par semaine sur la page Objectifs.</p>
              }
            </section>
            <section class="sport-section" aria-labelledby="sport-recent">
              <div class="sport-section-head"><h2 id="sport-recent">Dernières séances</h2>
                <a mat-button routerLink="/sports/history"><mat-icon svgIcon="history" />Tout l’historique</a></div>
              @if (recent().length) {
                <div class="session-list">
                  @for (session of recent(); track session.id) {
                    <button type="button" class="session-row" (click)="openEditor(session)">
                      <span class="session-day">{{ shortDay(session.date) }}</span>
                      <span class="session-text"><strong>{{ sessionTitle(session) }}</strong><span class="lo-muted lo-small">{{ sessionDetails(session) }}</span></span>
                      <span class="session-kcal" [class.is-unknown]="session.calories === null">{{ sessionCalories(session) }}</span>
                    </button>
                  }
                </div>
              } @else {
                <p class="lo-empty">Aucune séance ces deux dernières semaines.</p>
              }
            </section>
          </div>
        }
      }
    </div>
  `,
  styleUrl: './sports.css',
})
export class SportTodayPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly dialog = inject(MatDialog)
  readonly loading = signal(true)
  readonly error = signal('')
  readonly banner = signal('')
  readonly today = signal('')
  readonly sports = signal<SportOption[]>([])
  readonly templates = signal<SportTemplateDto[]>([])
  readonly challenges = signal<ChallengeReportDto[]>([])
  readonly weeks = signal<WeekReportDto[]>([])
  readonly week = computed(() => this.weeks().at(-1) ?? null)
  readonly recent = computed(() => [...this.weeks()].reverse().flatMap(week => week.sessions).slice(0, 3))
  readonly pending = signal('')
  custom: Record<string, number | null> = {}

  readonly integer = integer
  readonly decimal = decimal
  readonly duration = duration
  readonly longDay = longDay
  readonly shortDay = shortDay
  readonly weekRange = weekRange
  readonly quickSteps = quickSteps
  readonly streakLine = streakLine
  readonly jokerLine = jokerLine
  readonly challengePercent = challengePercent
  readonly goalProgressText = goalProgressText
  readonly goalPercent = goalPercent
  readonly sessionTitle = sessionTitle
  readonly sessionDetails = sessionDetails
  readonly sessionCalories = sessionCalories

  async ngOnInit(): Promise<void> {
    try {
      const [catalog, templates, challenges, weeks] = await Promise.all([
        firstValueFrom(this.api.catalog()), firstValueFrom(this.api.templates()),
        firstValueFrom(this.api.challenges(1)), firstValueFrom(this.api.weeks(2)),
      ])
      this.today.set(catalog.today); this.sports.set(catalog.sports); this.templates.set(templates)
      this.challenges.set(challenges); this.weeks.set(weeks)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }

  unitNoun(report: ChallengeReportDto): string {
    return report.challenge.unit === 'minutes' ? 'min' : report.challenge.name.toLocaleLowerCase('fr')
  }
  entriesLine(report: ChallengeReportDto): string {
    return report.todayEntries.map(entry => `${entryTime(entry.loggedAt)} +${entry.quantity}`).join(' · ')
  }
  caloriesNote(week: WeekReportDto): string {
    if (week.caloriesComplete) return week.sessionCount ? 'Complet' : ''
    const count = week.sessionsWithoutCalories
    return `Partiel : ${count} séance${count > 1 ? 's' : ''} sans calories`
  }

  add(report: ChallengeReportDto, quantity: number): Promise<void> {
    return this.update(report, () => this.api.addEntry(report.challenge.id, quantity))
  }
  addCustom(report: ChallengeReportDto): Promise<void> {
    const quantity = Math.round(Number(this.custom[report.challenge.id] ?? 0))
    if (!quantity || quantity < 1) return Promise.resolve()
    this.custom[report.challenge.id] = null
    return this.add(report, quantity)
  }
  undo(report: ChallengeReportDto): Promise<void> {
    const last = report.todayEntries.at(-1)
    return last ? this.update(report, () => this.api.removeEntry(report.challenge.id, last.id), false) : Promise.resolve()
  }

  private async update(report: ChallengeReportDto, call: () => ReturnType<SportsApiService['addEntry']>, celebrate = true): Promise<void> {
    if (this.pending()) return
    this.pending.set(report.challenge.id); this.error.set('')
    try {
      const updated = await firstValueFrom(call())
      this.challenges.update(list => list.map(item => item.challenge.id === updated.challenge.id ? updated : item))
      this.banner.set(celebrate ? celebration(report, updated) : '')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.pending.set('') }
  }

  async openEditor(session: SportSessionDto | null): Promise<void> {
    const message = await firstValueFrom(openEditor<SessionEditorComponent, SessionEditorData, string>(this.dialog, SessionEditorComponent,
      { session, templates: this.templates(), sports: this.sports(), today: this.today() }).afterClosed())
    if (!message) return
    this.banner.set(message)
    try { this.weeks.set(await firstValueFrom(this.api.weeks(2))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
