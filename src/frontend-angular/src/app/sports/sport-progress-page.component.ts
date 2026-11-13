import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { SportNavComponent } from './sport-nav.component'
import { calendarWeeks, challengeAmount, dayLevel, dayMonth, decimal, duration, integer } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { ChallengeDayDto, ChallengeReportDto, WeekReportDto } from './sports.models'

type Metric = 'calories' | 'sessions' | 'distance' | 'duration'

const METRICS: { id: Metric, label: string, value: (week: WeekReportDto) => number, format: (value: number) => string }[] = [
  { id: 'calories', label: 'Calories', value: week => week.calories, format: integer },
  { id: 'sessions', label: 'Séances', value: week => week.sessionCount, format: value => String(value) },
  { id: 'distance', label: 'Distance', value: week => week.distanceKm, format: value => decimal(value) },
  { id: 'duration', label: 'Durée', value: week => week.durationMinutes, format: duration },
]

const STATUS_LABELS: Record<string, string> = { reached: 'atteint', joker: 'joker', missed: 'manqué', in_progress: 'en cours', not_counted: 'hors défi' }

/** Weekly volume over 8 weeks and, per challenge, its calendar, streak, totals and milestones. */
@Component({
  selector: 'app-sport-progress-page',
  standalone: true,
  imports: [SportNavComponent],
  template: `
    <div class="page">
      <app-sport-nav />
      <div class="page-header"><h1>Progression</h1></div>
      @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="status-text" role="status">Chargement…</p> } @else {
        <div class="week-grid">
          <section class="surface-card sport-section" aria-labelledby="progress-weeks">
            <h2 id="progress-weeks">8 dernières semaines</h2>
            <div class="choice-grid" role="group" aria-label="Mesure affichée">
              @for (item of metrics; track item.id) {
                <button type="button" class="pill" [attr.aria-pressed]="metric() === item.id" (click)="metric.set(item.id)">{{ item.label }}</button>
              }
            </div>
            <div class="chart" role="img" [attr.aria-label]="chartLabel()">
              @for (bar of bars(); track bar.label) {
                <div class="chart-col">
                  <strong>{{ bar.value }}</strong>
                  <span class="chart-bar" [class.is-current]="bar.current" [style.height.px]="bar.height"></span>
                  <span class="muted">{{ bar.label }}</span>
                  <span class="goals" [class.is-done]="bar.allGoals">{{ bar.goals }}</span>
                </div>
              }
            </div>
            <p class="muted small">Sous chaque barre : objectifs atteints. Barre hachurée : semaine en cours. * : calories partielles.</p>
            @for (record of records(); track record.label) {
              <div class="settings-row"><span>{{ record.label }}</span><strong>{{ record.value }}</strong></div>
            }
          </section>

          <section class="surface-card sport-section" aria-labelledby="progress-challenges">
            <h2 id="progress-challenges">Défis du jour</h2>
            @if (!challenges().length) { <p class="muted">Aucun défi pour l'instant.</p> } @else {
              <div class="choice-grid" role="group" aria-label="Défi affiché">
                @for (report of challenges(); track report.challenge.id) {
                  <button type="button" class="pill" [attr.aria-pressed]="selected()?.challenge?.id === report.challenge.id"
                    (click)="selectedId.set(report.challenge.id)">{{ report.challenge.name }}{{ report.challenge.isActive ? '' : ' (arrêté)' }}</button>
                }
              </div>
              @if (selected(); as report) {
                <div class="stats">
                  <div class="stat"><div class="stat-label">Série</div><div class="stat-value">{{ report.streak }} j</div></div>
                  <div class="stat"><div class="stat-label">Record</div><div class="stat-value">{{ report.record }} j</div></div>
                  <div class="stat"><div class="stat-label">Jokers restants</div><div class="stat-value">{{ report.jokersLeftThisWeek }} / {{ report.jokersPerWeek }}</div></div>
                </div>
                <div class="calendar">
                  <div class="calendar-days" aria-hidden="true"><span>L</span><span>M</span><span>M</span><span>J</span><span>V</span><span>S</span><span>D</span></div>
                  <div class="calendar-weeks" role="img" [attr.aria-label]="'Calendrier des 12 dernières semaines pour ' + report.challenge.name">
                    @for (week of calendar(); track $index) {
                      <div class="calendar-week">
                        @for (day of week; track $index) {
                          @if (day) {
                            <span class="cell" [class]="'cell cell-' + level(day)" [class.is-today]="day.date === report.today" [title]="cellTitle(day)"></span>
                          } @else { <span class="cell cell-none"></span> }
                        }
                      </div>
                    }
                  </div>
                </div>
                <div class="legend">
                  <span><i class="cell cell-zero"></i>0</span><span><i class="cell cell-low"></i>&lt; 50 %</span>
                  <span><i class="cell cell-high"></i>&lt; 100 %</span><span><i class="cell cell-full"></i>Atteint</span><span><i class="cell cell-joker"></i>Joker</span>
                </div>
                <div class="stats">
                  <div class="stat"><div class="stat-label">Depuis le {{ dayMonth(report.challenge.startedOn) }}</div><div class="stat-value">{{ amount(report, report.total) }}</div></div>
                  <div class="stat"><div class="stat-label">Cette année</div><div class="stat-value">{{ amount(report, report.totalThisYear) }}</div></div>
                </div>
                @if (report.nextMilestone; as next) {
                  <div class="goal-row">
                    <div class="goal-row-head"><span class="muted">Prochain palier</span>
                      <strong>{{ amount(report, next) }} (encore {{ amount(report, next - report.total) }})</strong></div>
                    <div class="progress"><span class="is-done" [style.width.%]="milestonePercent(report)"></span></div>
                  </div>
                }
                <div class="tags">
                  @for (milestone of report.milestones; track milestone) {
                    <span class="chip" [class.chip-ok]="report.total >= milestone" [class.chip-neutral]="report.total < milestone">
                      {{ report.total >= milestone ? '✓ ' : '' }}{{ amount(report, milestone) }}</span>
                  }
                </div>
              }
            }
          </section>
        </div>
      }
    </div>
  `,
  styleUrl: './sports.css',
})
export class SportProgressPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  readonly loading = signal(true)
  readonly error = signal('')
  readonly weeks = signal<WeekReportDto[]>([])
  readonly challenges = signal<ChallengeReportDto[]>([])
  readonly metric = signal<Metric>('calories')
  readonly selectedId = signal('')
  readonly metrics = METRICS
  readonly dayMonth = dayMonth

  readonly selected = computed(() => this.challenges().find(report => report.challenge.id === this.selectedId()) ?? this.challenges()[0] ?? null)
  readonly calendar = computed(() => calendarWeeks(this.selected()?.calendar ?? []))
  readonly bars = computed(() => {
    const metric = METRICS.find(item => item.id === this.metric())!
    const weeks = this.weeks()
    const max = Math.max(1, ...weeks.map(metric.value))
    return weeks.map((week, index) => ({
      label: dayMonth(week.monday),
      value: metric.format(metric.value(week)) + (metric.id === 'calories' && !week.caloriesComplete ? '*' : ''),
      height: Math.max(4, Math.round(metric.value(week) * 120 / max)),
      current: index === weeks.length - 1,
      goals: week.goals.length ? `${week.goalsReached}/${week.goals.length}` : '',
      allGoals: week.goals.length > 0 && week.goalsReached === week.goals.length,
    }))
  })
  readonly chartLabel = computed(() => `${METRICS.find(item => item.id === this.metric())!.label} par semaine : ` +
    this.bars().map(bar => `${bar.label} ${bar.value}`).join(', '))
  readonly records = computed(() => {
    const weeks = this.weeks()
    const sessions = weeks.flatMap(week => week.sessions)
    const longest = sessions.filter(session => session.distanceKm !== null).sort((a, b) => b.distanceKm! - a.distanceKm!)[0]
    const biggest = [...weeks].sort((a, b) => b.calories - a.calories)[0]
    const records = [{ label: 'Séances sur 8 semaines', value: String(sessions.length) }]
    if (longest) records.push({ label: 'Plus longue sortie', value: `${decimal(longest.distanceKm!)} km · ${dayMonth(longest.date)}` })
    if (biggest?.calories) records.push({ label: 'Plus grosse semaine', value: `${integer(biggest.calories)} kcal · semaine du ${dayMonth(biggest.monday)}` })
    return records
  })

  async ngOnInit(): Promise<void> {
    try {
      const [weeks, challenges] = await Promise.all([firstValueFrom(this.api.weeks(8)), firstValueFrom(this.api.challenges(12, true))])
      this.weeks.set(weeks); this.challenges.set(challenges)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }

  level(day: ChallengeDayDto): string { return dayLevel(day.status, day.percent) }
  cellTitle(day: ChallengeDayDto): string { return `${dayMonth(day.date)} : ${day.total} / ${day.target} (${STATUS_LABELS[day.status]})` }
  amount(report: ChallengeReportDto, value: number): string {
    const amount = challengeAmount(report.challenge.unit, value)
    return report.challenge.unit === 'reps' ? `${amount} ${report.challenge.name.toLocaleLowerCase('fr')}` : amount
  }
  milestonePercent(report: ChallengeReportDto): number {
    const next = report.nextMilestone
    if (!next) return 100
    const previous = [...report.milestones].reverse().find(milestone => milestone <= report.total) ?? 0
    return Math.round((report.total - previous) * 100 / (next - previous))
  }
}
