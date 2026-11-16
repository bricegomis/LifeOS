import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { daysLabel, exerciseCharts, kilos, measureUnit, routineDayLevel, routineDayTitle } from './routine-format'
import { calendarWeeks, dayMonth } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { ExerciseDto, ExerciseProgressDto, RoutineDayDto, RoutineReportDto } from './sports.models'

/** Follow-up of routines (calendar, streak, record) and of each exercise (value and load per week), ADR 0014. */
@Component({
  selector: 'app-routine-progress',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
    @if (loaded() && (routines().length || exercises().length)) {
      <div class="week-grid">
        <section class="surface-card sport-section" aria-labelledby="progress-routines">
          <h2 id="progress-routines">Routines</h2>
          @if (!routines().length) { <p class="muted">Aucune routine pour l'instant.</p> } @else {
            <div class="choice-grid" role="group" aria-label="Routine affichée">
              @for (report of routines(); track report.routine.id) {
                <button type="button" class="pill" [attr.aria-pressed]="selected()?.routine?.id === report.routine.id"
                  (click)="selectedId.set(report.routine.id)">{{ report.routine.name }}{{ report.routine.isActive ? '' : ' (arrêtée)' }}</button>
              }
            </div>
            @if (selected(); as report) {
              <div class="stats">
                <div class="stat"><div class="stat-label">Série</div><div class="stat-value">{{ report.streak }} j</div></div>
                <div class="stat"><div class="stat-label">Record</div><div class="stat-value">{{ report.record }} j</div></div>
                <div class="stat"><div class="stat-label">Jokers restants</div>
                  <div class="stat-value">{{ report.jokersPerWeek ? report.jokersLeftThisWeek + ' / ' + report.jokersPerWeek : 'Aucun' }}</div></div>
              </div>
              <p class="muted small">{{ daysLabel(report.routine.days) }}</p>
              <div class="calendar">
                <div class="calendar-days" aria-hidden="true"><span>L</span><span>M</span><span>M</span><span>J</span><span>V</span><span>S</span><span>D</span></div>
                <div class="calendar-weeks" role="img" [attr.aria-label]="'Calendrier des 12 dernières semaines pour ' + report.routine.name">
                  @for (week of calendar(); track $index) {
                    <div class="calendar-week">
                      @for (day of week; track $index) {
                        @if (day) {
                          <span [class]="'cell cell-' + level(day)" [class.is-today]="day.date === report.today" [title]="cellTitle(day)"></span>
                        } @else { <span class="cell cell-none"></span> }
                      }
                    </div>
                  }
                </div>
              </div>
              <div class="legend">
                <span><i class="cell cell-zero"></i>0</span><span><i class="cell cell-low"></i>&lt; 50 %</span>
                <span><i class="cell cell-high"></i>&lt; 100 %</span><span><i class="cell cell-full"></i>Faite</span>
                <span><i class="cell cell-joker"></i>Joker</span><span><i class="cell cell-off"></i>Non prévu</span>
              </div>
            }
          }
        </section>

        <section class="surface-card sport-section" aria-labelledby="progress-exercises">
          <h2 id="progress-exercises">Exercices</h2>
          @if (!exercises().length) { <p class="muted">Aucun exercice pour l'instant.</p> } @else {
            <label class="field"><span>Exercice</span>
              <select class="text-input" name="progressExercise" [ngModel]="exercise()?.id" (ngModelChange)="pick($event)">
                @for (item of exercises(); track item.id) { <option [value]="item.id">{{ item.name }}{{ item.isArchived ? ' (archivé)' : '' }}</option> }
              </select></label>
            @if (exercise(); as item) {
              @if (progress(); as data) {
                @if (data.bestValue === null) { <p class="muted">Pas encore réalisé.</p> } @else {
                  <div class="stats">
                    <div class="stat"><div class="stat-label">{{ item.measure === 'hold' ? 'Record de tenue' : 'Record de répétitions' }}</div>
                      <div class="stat-value">{{ data.bestValue }} {{ measureUnit(item.measure) }}</div></div>
                    @if (data.bestLoadKg !== null) {
                      <div class="stat"><div class="stat-label">Record de charge</div><div class="stat-value">{{ kilos(data.bestLoadKg) }} kg</div></div>
                    }
                    <div class="stat"><div class="stat-label">Jours réalisés</div><div class="stat-value">{{ data.daysDone }}</div></div>
                  </div>
                  @for (chart of charts(); track chart.title) {
                    <div class="exercise-chart">
                      <div class="goal-row-head"><strong>{{ chart.title }}</strong><span class="value is-done">{{ chart.now }}</span></div>
                      <div class="mini-chart" role="img" [attr.aria-label]="chart.summary">
                        @for (bar of chart.bars; track $index) {
                          <div class="mini-col" [title]="bar.title"><span class="muted">{{ bar.label }}</span>
                            <span class="chart-bar" [class.is-current]="bar.current" [style.height.px]="bar.height"></span></div>
                        }
                      </div>
                    </div>
                  }
                  <p class="muted small">Meilleure valeur de chaque semaine sur 12 semaines, toutes routines confondues.</p>
                }
              } @else if (!error()) { <p class="status-text" role="status">Chargement…</p> }
            }
          }
        </section>
      </div>
    }
  `,
  styleUrls: ['./sports.css', './routines.css'],
})
export class RoutineProgressComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly routines = signal<RoutineReportDto[]>([])
  readonly exercises = signal<ExerciseDto[]>([])
  readonly selectedId = signal('')
  readonly exercise = signal<ExerciseDto | null>(null)
  readonly progress = signal<ExerciseProgressDto | null>(null)

  readonly selected = computed(() => this.routines().find(report => report.routine.id === this.selectedId()) ?? this.routines()[0] ?? null)
  readonly calendar = computed(() => calendarWeeks(this.selected()?.calendar ?? []))
  readonly charts = computed(() => {
    const exercise = this.exercise(); const progress = this.progress()
    return exercise && progress ? exerciseCharts(exercise, progress, dayMonth) : []
  })
  readonly daysLabel = daysLabel
  readonly measureUnit = measureUnit
  readonly kilos = kilos

  async ngOnInit(): Promise<void> {
    try {
      const [routines, exercises] = await Promise.all([firstValueFrom(this.api.routines(12, true)), firstValueFrom(this.api.exercises(true))])
      this.routines.set(routines); this.exercises.set(exercises)
      const first = routines.find(report => report.routine.isActive)?.routine.lines[0]?.exerciseId
      const exercise = exercises.find(item => item.id === first) ?? exercises[0]
      if (exercise) await this.pick(exercise.id)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }

  async pick(exerciseId: string): Promise<void> {
    const exercise = this.exercises().find(item => item.id === exerciseId) ?? null
    this.exercise.set(exercise); this.progress.set(null); this.error.set('')
    if (!exercise) return
    try {
      const progress = await firstValueFrom(this.api.exerciseProgress(exercise.id, 12))
      if (this.exercise()?.id === exercise.id) this.progress.set(progress)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
  }

  level(day: RoutineDayDto): string { return routineDayLevel(day) }
  cellTitle(day: RoutineDayDto): string { return routineDayTitle(day, dayMonth(day.date)) }
}
