import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ExerciseImageComponent } from './exercise-image.component'
import { RoutineDialogComponent, type RoutineDialogData, type RoutineDialogResult } from './routine-dialog.component'
import {
  clockTime, daysLabel, nextDayLabel, openPass, routineCelebration, setButton, setsDone, targetText,
} from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { RoutineLineDto, RoutineReportDto, RoutineSessionDto } from './sports.models'
import { ConfirmService } from '@/app/shared/ui/confirm'

/**
 * Doing a routine (ADR 0014, ADR 0015): today's open pass with one "Série faite" button per exercise
 * that fills set after set, the passes of the day.
 */
@Component({
  selector: 'app-sport-routine-page',
  standalone: true,
  imports: [ExerciseImageComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page routine-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="lo-muted" role="status">Chargement…</p> }
      @else if (report(); as report) {
        <header class="head">
          <a mat-icon-button routerLink="/sports" aria-label="Retour à l’accueil du sport"><mat-icon svgIcon="arrow_back" /></a>
          <div class="lo-page-title">
            <h1>{{ report.routine.name }}</h1>
            <span class="lo-muted">{{ daysLabel(report.routine.days) }} · série de {{ report.streak }} jour{{ report.streak > 1 ? 's' : '' }} · record {{ report.record }}</span>
          </div>
          @if (report.routine.isActive) {
            <button mat-flat-button type="button" class="tonal lo-desktop-only" (click)="editRoutine(report)"><mat-icon svgIcon="edit" />Modifier la routine</button>
            <button mat-icon-button type="button" class="lo-phone-only" (click)="editRoutine(report)" aria-label="Modifier la routine"
              title="Modifier la routine"><mat-icon svgIcon="edit" /></button>
          }
        </header>

        @if (pass(); as current) {
          <section class="lo-section" aria-labelledby="open-pass">
            <div class="lo-section-head">
              <h2 id="open-pass">Passage de {{ clockTime(current.startedAt) }}</h2>
              <span class="lo-muted">{{ current.exercisesDone }} / {{ current.exercisesTotal }} exercices</span>
            </div>
            <div><a mat-flat-button class="start" [routerLink]="['/sports/routine', report.routine.id, 'run']"><mat-icon svgIcon="timer" />Continuer en mode guidé</a></div>
            @for (line of report.routine.lines; track line.exerciseId) {
              @let button = buttonOf(line);
              <article class="lo-card lo-media-card exercise" [class.is-done]="button.full">
                <app-exercise-image [exerciseId]="line.exerciseId" [hasImage]="line.hasImage" [size]="photoSize" [fill]="true" [alt]="'Photo : ' + line.exerciseName" />
                <div class="exercise-body">
                  <div class="exercise-text">
                    <strong class="exercise-name">{{ line.exerciseName }}</strong>
                    <span class="exercise-target">{{ targetText(line) }}</span>
                    @if (line.instructions) { <span class="lo-muted">{{ line.instructions }}</span> }
                  </div>
                  <div class="exercise-actions">
                    <button type="button" class="set-button" [class.is-full]="button.full" [disabled]="button.full || pending() === line.exerciseId"
                      [style.--fill.%]="button.percent" [attr.aria-label]="button.aria" (click)="setSets(line, 1)">
                      <span>{{ button.label }}</span><span class="set-counter">{{ button.counter }}</span>
                    </button>
                    <button mat-icon-button type="button" class="undo" [disabled]="!done(line) || pending() === line.exerciseId"
                      [attr.aria-label]="'Retirer la dernière série de ' + line.exerciseName" [attr.title]="'Retirer la dernière série'" (click)="setSets(line, -1)">
                      <mat-icon svgIcon="undo" />
                    </button>
                  </div>
                </div>
              </article>
            }
            <p class="lo-help">Un appui par série faite, avec la cible ; la flèche retire la dernière. Le passage se termine à la dernière série.</p>
          </section>
        } @else if (report.plannedToday && report.routine.isActive) {
          <div class="start-row">
            <a mat-flat-button class="start" [routerLink]="['/sports/routine', report.routine.id, 'run']"><mat-icon svgIcon="timer" />Mode guidé</a>
            <button mat-stroked-button type="button" class="free" [disabled]="pending() === 'start'" (click)="start()">
              <mat-icon svgIcon="play_arrow" />{{ report.completedToday ? 'Nouveau passage' : 'Commencer un passage' }}</button>
          </div>
          <p class="lo-help">Le mode guidé enchaîne les exercices un par un, avec un chrono et un bip pour les exercices tenus.</p>
        } @else if (report.routine.isActive) {
          <p class="lo-muted">Pas prévue aujourd’hui. {{ nextDayLabel(report.today, report.nextPlannedDay) }}</p>
        }

        <section class="lo-section" aria-labelledby="passes">
          <h2 id="passes">Passages du jour</h2>
          @if (report.todaySessions.length) {
            <div class="lo-list">
              @for (session of report.todaySessions; track session.id) {
                <div class="lo-row pass">
                  <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="schedule" /></span>
                  <span class="lo-row-text">
                    <span class="lo-row-title">{{ passTime(session) }} · {{ session.finished ? 'Terminé' : 'En cours, ' + session.exercisesDone + ' / ' + session.exercisesTotal }}</span>
                  </span>
                  <button mat-icon-button type="button" class="delete" (click)="deletePass(session)" aria-label="Supprimer le passage" title="Supprimer le passage">
                    <mat-icon svgIcon="delete" />
                  </button>
                </div>
              }
            </div>
          } @else { <p class="lo-muted">Aucun passage aujourd’hui.</p> }
        </section>
      } @else if (!error()) {
        <p class="lo-empty">Routine introuvable. <a routerLink="/sports/routines">Voir les routines</a></p>
      }
    </div>
  `,
  styles: [`
    .routine-page { max-width: 960px; }
    .head { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .head .lo-page-title { flex: 1 1 200px; }
    .tonal, .start { --mat-button-filled-container-color: var(--mat-sys-secondary-container); --mat-button-filled-label-text-color: var(--mat-sys-on-secondary-container); }
    .start { --mat-button-filled-container-color: var(--mat-sys-primary); --mat-button-filled-label-text-color: var(--mat-sys-on-primary); min-height: 48px; }
    .start-row { display: flex; flex-wrap: wrap; gap: 8px; }
    .free { min-height: 48px; }
    .exercise { min-height: 112px; }
    .exercise.is-done { border-color: var(--mat-sys-primary); }
    .exercise-body { display: flex; min-width: 0; flex: 1 1 auto; flex-wrap: wrap; align-items: center; gap: 12px; padding: 12px 16px; }
    .exercise-text { display: flex; min-width: 0; flex: 1 1 220px; flex-direction: column; gap: 2px; }
    .exercise-name { font: var(--mat-sys-title-medium); }
    .exercise-target { color: var(--mat-sys-primary); font: var(--mat-sys-label-large); }
    .exercise-actions { display: flex; flex: 1 1 260px; align-items: center; justify-content: flex-end; gap: 4px; }
    .set-button {
      display: flex; min-width: 0; min-height: 48px; flex: 1 1 auto; max-width: 260px; align-items: center; justify-content: space-between; gap: 8px;
      padding: 0 20px; border: 1px solid var(--mat-sys-primary); border-radius: 24px; color: var(--mat-sys-primary); font: var(--mat-sys-label-large);
      background: linear-gradient(to right, var(--mat-sys-primary-container) var(--fill, 0%), var(--mat-sys-surface) var(--fill, 0%));
    }
    .set-button.is-full { background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); opacity: 1; }
    .set-counter { font: var(--mat-sys-title-medium); }
    .undo { border: 1px solid var(--mat-sys-outline); }
    .pass { gap: 12px; padding: 8px 8px 8px 16px; min-height: 72px; }
    .delete { color: var(--mat-sys-error); }
    @media (max-width: 760px) {
      .exercise-actions { flex-basis: 100%; }
      .set-button { max-width: none; }
    }
  `],
})
export class SportRoutinePageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly confirm = inject(ConfirmService)
  private readonly route = inject(ActivatedRoute)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly loading = signal(true)
  readonly error = signal('')
  readonly pending = signal('')
  readonly report = signal<RoutineReportDto | null>(null)
  readonly pass = computed(() => { const report = this.report(); return report ? openPass(report) : null })
  readonly photoSize = globalThis.matchMedia?.('(max-width: 760px)').matches ? 96 : 128

  readonly daysLabel = daysLabel
  readonly targetText = targetText
  readonly clockTime = clockTime
  readonly nextDayLabel = nextDayLabel

  async ngOnInit(): Promise<void> {
    try {
      await this.reload()
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }

  done(line: RoutineLineDto): number { return setsDone(this.pass(), line.exerciseId) }
  buttonOf(line: RoutineLineDto) { return setButton(line, this.done(line)) }
  passTime(session: RoutineSessionDto): string {
    return session.finishedAt ? `${clockTime(session.startedAt)} – ${clockTime(session.finishedAt)}` : clockTime(session.startedAt)
  }

  async start(): Promise<void> {
    const report = this.report()
    if (report) await this.call('start', this.api.startSession(report.routine.id))
  }

  /** One more set, or one less (0 unticks the exercise). */
  async setSets(line: RoutineLineDto, direction: 1 | -1): Promise<void> {
    const report = this.report(), pass = this.pass()
    if (!report || !pass) return
    const before = report
    const after = await this.call(line.exerciseId, this.api.setSets(report.routine.id, pass.id, line.exerciseId, Math.max(0, this.done(line) + direction)))
    if (!after) return
    const finished = after.todaySessions.find(session => session.id === pass.id)
    if (finished?.finished) {
      const message = routineCelebration(before, after)
      this.snack.open(message || 'Passage terminé', undefined, { duration: 5000 })
    }
  }

  async deletePass(session: RoutineSessionDto): Promise<void> {
    const report = this.report()
    if (!report
      || !await this.confirm.ask({ message: `Supprimer le passage de ${clockTime(session.startedAt)} et ses coches ?`, confirmLabel: 'Supprimer', danger: true })) return
    if (await this.call(session.id, this.api.deletePass(report.routine.id, session.id))) this.snack.open('Passage supprimé', undefined, { duration: 4000 })
  }

  async editRoutine(report: RoutineReportDto): Promise<void> {
    let exercises
    try { exercises = await firstValueFrom(this.api.exercises(true)) }
    catch (error) { this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 }); return }
    const result = await firstValueFrom(openEditor<RoutineDialogComponent, RoutineDialogData, RoutineDialogResult>(
      this.dialog, RoutineDialogComponent, { routine: report.routine, exercises }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.reload()
  }

  private async reload(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id')
    this.report.set((await firstValueFrom(this.api.routines(1, true))).find(item => item.routine.id === id) ?? null)
  }

  private async call(key: string, request: Observable<RoutineReportDto>): Promise<RoutineReportDto | null> {
    if (this.pending()) return null
    this.pending.set(key); this.error.set('')
    try {
      const report = await firstValueFrom(request)
      this.report.set(report)
      return report
    } catch (error) {
      this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 })
      return null
    } finally { this.pending.set('') }
  }
}
