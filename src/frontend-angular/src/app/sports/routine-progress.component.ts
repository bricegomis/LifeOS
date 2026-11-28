import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { ConfirmService } from '@/app/shared/ui/confirm'
import { completionText, daysLabel, exerciseCharts, kilos, measureUnit, routineDayLevel, routineDayTitle } from './routine-format'
import { calendarWeeks, dayMonth } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { ExerciseCompletionDto, ExerciseDto, ExerciseProgressDto, RoutineDayDto, RoutineReportDto } from './sports.models'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

/** Follow-up of routines (calendar with passes, streak, record) and of each exercise (value, load), ADR 0014, ADR 0015. */
@Component({
  selector: 'app-routine-progress',
  standalone: true,
  imports: [SkeletonComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule],
  template: `
    @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
    @if (loaded() && (routines().length || exercises().length)) {
      <div class="lo-grid-2">
        <section class="lo-panel" aria-labelledby="progress-routines">
          <h2 id="progress-routines">Routines</h2>
          @if (!routines().length) { <p class="lo-muted">Aucune routine pour l'instant.</p> } @else {
            <div class="lo-chips" role="group" aria-label="Routine affichée">
              @for (report of routines(); track report.routine.id) {
                <button type="button" class="lo-filter-chip" [class.is-on]="selected()?.routine?.id === report.routine.id" [attr.aria-pressed]="selected()?.routine?.id === report.routine.id"
                  (click)="selectedId.set(report.routine.id)">{{ report.routine.name }}</button>
              }
            </div>
            @if (selected(); as report) {
              <div class="lo-stats">
                <div class="lo-stat"><div class="lo-stat-label">Série</div><div class="lo-stat-value">{{ report.streak }} j</div></div>
                <div class="lo-stat"><div class="lo-stat-label">Record</div><div class="lo-stat-value">{{ report.record }} j</div></div>
                <div class="lo-stat"><div class="lo-stat-label">Jokers restants</div>
                  <div class="lo-stat-value">{{ report.jokersPerWeek ? report.jokersLeftThisWeek + ' / ' + report.jokersPerWeek : 'Aucun' }}</div></div>
              </div>
              <p class="lo-muted lo-small">{{ daysLabel(report.routine.days) }}</p>
              <div class="calendar">
                <div class="calendar-days" aria-hidden="true"><span>L</span><span>M</span><span>M</span><span>J</span><span>V</span><span>S</span><span>D</span></div>
                <div class="calendar-weeks" role="img" [attr.aria-label]="'Calendrier des 12 dernières semaines pour ' + report.routine.name">
                  @for (week of calendar(); track $index) {
                    <div class="calendar-week">
                      @for (day of week; track $index) {
                        @if (day) {
                          <span [class]="'cell cell-' + level(day)" [class.is-today]="day.date === report.today" [title]="cellTitle(day)">{{ day.passes > 1 ? day.passes : '' }}</span>
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
              <p class="lo-muted lo-small">Un chiffre dans une case : nombre de passages terminés ce jour-là.</p>
            }
          }
        </section>

        <section class="lo-panel" aria-labelledby="progress-exercises">
          <h2 id="progress-exercises">Exercices</h2>
          @if (!exercises().length) { <p class="lo-muted">Aucun exercice pour l'instant.</p> } @else {
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Exercice</mat-label>
              <mat-select name="progressExercise" [ngModel]="exercise()?.id" (ngModelChange)="pick($event)">
                @for (item of exercises(); track item.id) { <mat-option [value]="item.id">{{ item.name }}{{ item.isArchived ? ' (archivé)' : '' }}</mat-option> }
              </mat-select>
            </mat-form-field>
            @if (exercise(); as item) {
              @if (progress(); as data) {
                @if (data.bestValue === null) { <p class="lo-muted">Pas encore réalisé.</p> } @else {
                  <div class="lo-stats">
                    <div class="lo-stat"><div class="lo-stat-label">{{ item.measure === 'hold' ? 'Record de tenue' : 'Record de répétitions' }}</div>
                      <div class="lo-stat-value">{{ data.bestValue }} {{ measureUnit(item.measure) }}</div></div>
                    @if (data.bestLoadKg !== null) {
                      <div class="lo-stat"><div class="lo-stat-label">Record de charge</div><div class="lo-stat-value">{{ kilos(data.bestLoadKg) }} kg</div></div>
                    }
                    <div class="lo-stat"><div class="lo-stat-label">Jours réalisés</div><div class="lo-stat-value">{{ data.daysDone }}</div></div>
                  </div>
                  @for (chart of charts(); track chart.title) {
                    <div class="exercise-chart">
                      <div class="goal-row-head"><strong>{{ chart.title }}</strong><span class="value is-done">{{ chart.now }}</span></div>
                      <div class="mini-chart" role="img" [attr.aria-label]="chart.summary">
                        @for (bar of chart.bars; track $index) {
                          <div class="mini-col" [title]="bar.title"><span class="lo-muted">{{ bar.label }}</span>
                            <span class="chart-bar" [class.is-current]="bar.current" [style.height.px]="bar.height"></span></div>
                        }
                      </div>
                    </div>
                  }
                  <p class="lo-muted lo-small">Meilleure valeur de chaque semaine sur 12 semaines, routines et exercices faits seuls confondus.</p>
                  @if (data.recent.length) {
                    <table class="recent">
                      <caption>Dernières fois</caption>
                      <thead><tr><th scope="col">Jour</th><th scope="col">Fait</th><th scope="col"><span class="sr-only">Actions</span></th></tr></thead>
                      <tbody>
                        @for (tick of data.recent; track tick.id) {
                          <tr><td>{{ dayMonth(tick.date) }}</td>
                            <td>{{ completionText(item.measure, tick) }}@if (!tick.sessionId) { <span class="lo-muted"> · hors routine</span> }</td>
                            <td class="actions">@if (!tick.sessionId) {
                              <button mat-icon-button type="button" [disabled]="deleting() === tick.id" (click)="remove(item, tick)"
                                [attr.aria-label]="'Supprimer ' + item.name + ' du ' + dayMonth(tick.date)" title="Supprimer"><mat-icon svgIcon="delete" /></button>
                            }</td></tr>
                        }
                      </tbody>
                    </table>
                    <p class="lo-muted lo-small">La cible se règle à la main dans la routine.</p>
                  }
                }
              } @else if (!error()) { <app-skeleton [rows]="2" /> }
            }
          }
        </section>
      </div>
    }
  `,
  styleUrl: './sports.css',
  styles: [`
    .calendar .cell { display: grid; place-items: center; color: var(--mat-sys-on-primary); font-size: 11px; font-weight: 700; }
    .exercise-chart { display: flex; flex-direction: column; gap: 8px; }
    .mini-chart { display: flex; height: 96px; align-items: flex-end; gap: 6px; }
    .mini-col { display: flex; min-width: 0; height: 100%; flex: 1 1 0; flex-direction: column; align-items: center; justify-content: flex-end; gap: 2px; font-size: 10px; }
    .recent { width: 100%; border-collapse: collapse; font: var(--mat-sys-body-medium); }
    .recent caption { padding-bottom: 6px; text-align: left; font-weight: 700; }
    .recent th { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-medium); text-align: left; }
    .recent th, .recent td { padding: 6px 8px 6px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .recent .actions { width: 48px; padding: 0; text-align: right; }
  `],
})
export class RoutineProgressComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly confirm = inject(ConfirmService)
  private readonly snack = inject(MatSnackBar)
  readonly loaded = signal(false)
  readonly deleting = signal('')
  readonly completionText = completionText
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
      const [routines, exercises] = await Promise.all([firstValueFrom(this.api.routines(12)), firstValueFrom(this.api.exercises(true))])
      this.routines.set(routines); this.exercises.set(exercises)
      const first = routines[0]?.routine.lines[0]?.exerciseId
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
  readonly dayMonth = dayMonth
  /** Deletes an exercise done outside a pass (alone, or kept from a deleted routine). */
  async remove(exercise: ExerciseDto, tick: ExerciseCompletionDto): Promise<void> {
    if (this.deleting() || !await this.confirm.ask({ message: `Supprimer « ${exercise.name} » du ${dayMonth(tick.date)} ?`, confirmLabel: 'Supprimer', danger: true })) return
    this.deleting.set(tick.id)
    try {
      await firstValueFrom(this.api.deleteExerciseLog(exercise.id, tick.id))
      this.snack.open('Supprimé de la progression', undefined, { duration: 4000 })
      await this.pick(exercise.id)
    } catch (error) { this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 }) }
    finally { this.deleting.set('') }
  }
  cellTitle(day: RoutineDayDto): string { return routineDayTitle(day, dayMonth(day.date)) }
}
