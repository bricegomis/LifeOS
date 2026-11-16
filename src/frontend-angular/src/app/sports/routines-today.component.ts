import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import {
  nextDayLabel, openPass, passesLine, routineAction, routineChip, routineJokerLine, routinePercent, routineStreakLine,
} from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { RoutineReportDto } from './sports.models'

/** Today's routines on the sport home (ADR 0014, ADR 0015): one card per planned routine with its passes of the day. */
@Component({
  selector: 'app-routines-today',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
    @if (loaded() && (routines().length || !error())) {
      <section class="lo-section" aria-labelledby="sport-routines">
        <div class="lo-section-head"><h2 id="sport-routines">Routines du jour</h2>
          <a mat-button routerLink="/sports/routines"><mat-icon svgIcon="tune" />Gérer</a></div>
        @if (!routines().length) {
          <p class="lo-muted">Aucune routine. Créez vos exercices puis une routine quotidienne, sur les pages Exercices et Routines.</p>
        }
        <div class="cards">
          @for (report of planned(); track report.routine.id) {
            <article class="lo-card routine-card" [attr.aria-labelledby]="'routine-' + report.routine.id">
              <div class="card-head">
                <div class="lo-page-title"><strong [id]="'routine-' + report.routine.id" class="card-title">{{ report.routine.name }}</strong>
                  <span class="lo-muted">{{ routineStreakLine(report) }}</span></div>
                <span class="lo-tag" [class.is-done]="report.completedToday">{{ routineChip(report) }}</span>
              </div>
              <span>{{ passesLine(report) }}</span>
              @if (openPass(report); as pass) {
                <div class="progress-block">
                  <div class="bar" role="progressbar" [attr.aria-valuenow]="routinePercent(report)" aria-valuemin="0" aria-valuemax="100"
                    [attr.aria-label]="'Avancement du passage en cours de ' + report.routine.name"><span [style.width.%]="routinePercent(report)"></span></div>
                  <span class="lo-muted">Passage en cours · {{ pass.exercisesDone }} / {{ pass.exercisesTotal }} exercices</span>
                </div>
              }
              <div class="card-actions">
                <span class="lo-muted small">{{ routineJokerLine(report) }}</span>
                <button mat-flat-button type="button" [class.tonal]="report.completedToday && !openPass(report)" [disabled]="pending() === report.routine.id"
                  (click)="go(report)"><mat-icon svgIcon="play_arrow" />{{ routineAction(report) }}</button>
              </div>
            </article>
          }
        </div>
        @for (report of off(); track report.routine.id) {
          <div class="routine-off"><strong>{{ report.routine.name }}</strong><span class="lo-muted">{{ nextDayLabel(report.today, report.nextPlannedDay) }}</span></div>
        }
      </section>
    }
  `,
  styles: [`
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 12px; }
    .routine-card { display: flex; flex-direction: column; gap: 12px; padding: 16px; }
    .card-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; }
    .card-title { font: var(--mat-sys-title-medium); }
    .progress-block { display: flex; flex-direction: column; gap: 6px; }
    .bar { height: 4px; overflow: hidden; border-radius: 2px; background: var(--mat-sys-secondary-container); }
    .bar span { display: block; height: 100%; background: var(--mat-sys-primary); }
    .card-actions { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; }
    .small { font-size: 13px; }
    .tonal { --mat-button-filled-container-color: var(--mat-sys-secondary-container); --mat-button-filled-label-text-color: var(--mat-sys-on-secondary-container); }
    .routine-off { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px; padding: 12px 16px; border: 1px dashed var(--mat-sys-outline-variant); border-radius: 12px; }
  `],
})
export class RoutinesTodayComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly router = inject(Router)
  private readonly snack = inject(MatSnackBar)
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly pending = signal('')
  readonly routines = signal<RoutineReportDto[]>([])
  readonly planned = computed(() => this.routines().filter(report => report.plannedToday))
  readonly off = computed(() => this.routines().filter(report => !report.plannedToday))

  readonly routineStreakLine = routineStreakLine
  readonly routineChip = routineChip
  readonly routinePercent = routinePercent
  readonly routineAction = routineAction
  readonly routineJokerLine = routineJokerLine
  readonly nextDayLabel = nextDayLabel
  readonly passesLine = passesLine
  readonly openPass = openPass

  async ngOnInit(): Promise<void> {
    try { this.routines.set(await firstValueFrom(this.api.routines(1))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }

  /** Continues the open pass, or opens a new one, then goes to the routine. */
  async go(report: RoutineReportDto): Promise<void> {
    this.pending.set(report.routine.id)
    try {
      if (!openPass(report)) await firstValueFrom(this.api.startSession(report.routine.id))
      await this.router.navigate(['/sports/routine', report.routine.id])
    } catch (error) { this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 }) }
    finally { this.pending.set('') }
  }
}
