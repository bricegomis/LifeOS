import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import {
  doneText, nextDayLabel, routineAction, routineChip, routineJokerLine, routinePercent, routineStreakLine, targetText,
} from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { RoutineLineDto, RoutineReportDto } from './sports.models'

/** Today's routines on the sport home (ADR 0014): one card per planned routine, above the challenges. */
@Component({
  selector: 'app-routines-today',
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
    @if (loaded() && (routines().length || !error())) {
      <section class="sport-section" aria-labelledby="sport-routines">
        <div class="sport-section-head"><h2 id="sport-routines">Routines du jour</h2>
          <a class="link-button" routerLink="/sports/settings">Gérer les routines</a></div>
        @if (!routines().length) {
          <p class="empty-state">Aucune routine. Créez vos exercices puis une routine quotidienne dans les réglages du sport.</p>
        }
        <div class="challenge-grid">
          @for (report of planned(); track report.routine.id) {
            <article class="challenge-card" [attr.aria-labelledby]="'routine-' + report.routine.id">
              <div class="challenge-head">
                <div><strong [id]="'routine-' + report.routine.id">{{ report.routine.name }}</strong>
                  <span class="muted small">{{ routineStreakLine(report) }}</span></div>
                <span class="chip" [class.chip-done]="report.completedToday" [class.chip-neutral]="!report.completedToday">{{ routineChip(report) }}</span>
              </div>
              <div class="challenge-count"><strong>{{ report.doneToday }}</strong><span class="muted">/ {{ report.totalToday }} exercices</span></div>
              <div class="progress" role="progressbar" [attr.aria-valuenow]="routinePercent(report)" aria-valuemin="0" aria-valuemax="100"
                [attr.aria-label]="'Avancement de la routine ' + report.routine.name">
                <span [style.width.%]="routinePercent(report)" [class.is-done]="report.completedToday"></span></div>
              <ul class="routine-rows">
                @for (line of report.routine.lines; track line.exerciseId) {
                  <li [class.is-done]="isDone(report, line)">
                    <i class="routine-dot" aria-hidden="true"></i>
                    <span class="routine-row-name">{{ line.exerciseName }}</span>
                    <span class="small">{{ lineValue(report, line) }}</span>
                    <span class="sr-only">{{ isDone(report, line) ? '(fait)' : '(à faire)' }}</span>
                  </li>
                }
              </ul>
              <a class="lifeos-button routine-open" [routerLink]="['/sports/routine', report.routine.id]">{{ routineAction(report) }}</a>
              <span class="muted small">{{ routineJokerLine(report) }}</span>
            </article>
          }
        </div>
        @for (report of off(); track report.routine.id) {
          <div class="routine-off"><strong>{{ report.routine.name }}</strong><span class="muted">{{ nextDayLabel(report.today, report.nextPlannedDay) }}</span></div>
        }
      </section>
    }
  `,
  styleUrls: ['./sports.css', './routines.css'],
})
export class RoutinesTodayComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly routines = signal<RoutineReportDto[]>([])
  readonly planned = computed(() => this.routines().filter(report => report.plannedToday))
  readonly off = computed(() => this.routines().filter(report => !report.plannedToday))

  readonly routineStreakLine = routineStreakLine
  readonly routineChip = routineChip
  readonly routinePercent = routinePercent
  readonly routineAction = routineAction
  readonly routineJokerLine = routineJokerLine
  readonly nextDayLabel = nextDayLabel

  async ngOnInit(): Promise<void> {
    try { this.routines.set(await firstValueFrom(this.api.routines(1))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }

  isDone(report: RoutineReportDto, line: RoutineLineDto): boolean {
    return report.todayCompletions.some(completion => completion.exerciseId === line.exerciseId)
  }
  lineValue(report: RoutineReportDto, line: RoutineLineDto): string {
    const completion = report.todayCompletions.find(item => item.exerciseId === line.exerciseId)
    return completion ? doneText(line, completion) : targetText(line)
  }
}
