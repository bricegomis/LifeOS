import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ExerciseImageComponent } from './exercise-image.component'
import { RoutineFeelingDialogComponent, type FeelingDialogData } from './routine-feeling-dialog.component'
import { nextDayLabel, openPass, routineCelebration, setsDone, targetText } from './routine-format'
import {
  countdownText, currentStepIndex, elapsedPercent, LEAD_SECONDS, nextLabel, nextStep, roundCount, runSteps, runSummary, setTarget, stepPosition,
  warningSecond, type RunStep,
} from './routine-run'
import { RoutineSignals } from './routine-signals'
import { SportsApiService } from './sports-api.service'
import type { RoutineReportDto, RoutineSessionDto, SportSettingsDto } from './sports.models'

type Phase = 'idle' | 'lead' | 'hold'

/**
 * Guided run of a routine (ADR 0015, « Mode guidé »): one set at a time as a circuit, a countdown with
 * beeps for a hold (ticked at the end), a « Fait » button for repetitions, then the next set. Every set is
 * ticked in the open pass as on the routine page, so leaving and coming back resumes where it stopped.
 */
@Component({
  selector: 'app-sport-routine-run-page',
  standalone: true,
  imports: [ExerciseImageComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page run-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="lo-muted" role="status">Chargement…</p> }
      @else if (report(); as report) {
        <header class="head">
          <a mat-icon-button [routerLink]="['/sports/routine', report.routine.id]" aria-label="Quitter le mode guidé"><mat-icon svgIcon="close" /></a>
          <div class="lo-page-title">
            <h1>{{ report.routine.name }}</h1>
            <span class="lo-muted">Mode guidé</span>
          </div>
        </header>

        @if (pass(); as current) {
          @if (step(); as step) {
            <div class="overall" role="progressbar" aria-valuemin="0" [attr.aria-valuemax]="steps().length" [attr.aria-valuenow]="index()"
              [attr.aria-label]="'Séries faites : ' + index() + ' sur ' + steps().length"><span [style.width.%]="index() * 100 / steps().length"></span></div>
            <span class="lo-muted overall-text">Série {{ index() + 1 }} sur {{ steps().length }}</span>

            <article class="step" [attr.aria-labelledby]="'step-name'">
              <app-exercise-image class="photo" [exerciseId]="step.line.exerciseId" [hasImage]="step.line.hasImage" [size]="photoSize"
                [alt]="'Photo : ' + step.line.exerciseName" />
              @if (position(step); as text) { <span class="lo-tag position">{{ text }}</span> }
              <h2 id="step-name" class="step-name">{{ step.line.exerciseName }}</h2>
              <span class="step-target">{{ setTarget(step.line) }}</span>
              @if (step.line.instructions) { <p class="lo-muted instructions">{{ step.line.instructions }}</p> }

              @if (step.line.measure === 'hold') {
                <div class="clock" [class.is-lead]="phase() === 'lead'" aria-live="polite">
                  @if (phase() === 'lead') { <span class="clock-label">Préparez-vous</span> }
                  <span class="clock-time" [attr.aria-label]="clockAria()">{{ clockText(step) }}</span>
                  <div class="clock-bar" aria-hidden="true"><span [style.width.%]="clockPercent()"></span></div>
                </div>
                <div class="actions">
                  @if (phase() === 'idle') {
                    <button mat-flat-button type="button" class="main" [disabled]="!!pending()" (click)="startTimer(step)">
                      <mat-icon svgIcon="play_arrow" />Démarrer le chrono</button>
                    <button mat-button type="button" [disabled]="!!pending()" (click)="tick(step)"><mat-icon svgIcon="check" />Valider sans chrono</button>
                  } @else {
                    <button mat-flat-button type="button" class="main" (click)="togglePause()">
                      <mat-icon [svgIcon]="paused() ? 'play_arrow' : 'pause'" />{{ paused() ? 'Reprendre' : 'Pause' }}</button>
                    <button mat-button type="button" (click)="stopTimer()"><mat-icon svgIcon="restart_alt" />Recommencer la série</button>
                  }
                </div>
              } @else {
                <div class="actions">
                  <button mat-flat-button type="button" class="main" [disabled]="!!pending()" (click)="tick(step)">
                    <mat-icon svgIcon="check" />Fait</button>
                </div>
              }
              <p class="next">{{ next() }}</p>
            </article>

            <div class="footer">
              <button mat-button type="button" [disabled]="!undoable() || !!pending() || phase() !== 'idle'" (click)="undo()">
                <mat-icon svgIcon="undo" />Annuler la série précédente</button>
            </div>
          } @else {
            <p class="lo-muted">Toutes les séries sont faites.</p>
          }
        } @else if (report.routine.isActive && report.plannedToday) {
          <section class="lo-section start" aria-labelledby="run-start">
            <h2 id="run-start">{{ runSummary(report.routine.lines) }}</h2>
            <p class="lo-help">Les exercices s’enchaînent dans l’ordre de la routine, une série à la fois{{ rounds() > 1 ? ', puis on recommence pour le tour suivant' : '' }}.
              Un exercice tenu se fait au chrono, avec un bip à la fin ; un exercice en répétitions se valide par « Fait ».</p>
            <div class="lo-list">
              @for (line of report.routine.lines; track line.exerciseId) {
                <div class="lo-row line">
                  <app-exercise-image [exerciseId]="line.exerciseId" [hasImage]="line.hasImage" [size]="56" [alt]="''" />
                  <span class="lo-row-text"><span class="lo-row-title">{{ line.exerciseName }}</span><span class="lo-row-sub">{{ targetText(line) }}</span></span>
                  @if (line.measure === 'hold') { <mat-icon svgIcon="timer" aria-label="Au chrono" /> }
                </div>
              }
            </div>
            <div><button mat-flat-button type="button" class="main" [disabled]="pending() === 'start'" (click)="startPass()">
              <mat-icon svgIcon="play_arrow" />{{ report.completedToday ? 'Nouveau passage' : 'Commencer' }}</button></div>
          </section>
        } @else if (report.routine.isActive) {
          <p class="lo-muted">Pas prévue aujourd’hui. {{ nextDayLabel(report.today, report.nextPlannedDay) }}</p>
        } @else {
          <p class="lo-muted">Cette routine est arrêtée.</p>
        }
      } @else if (!error()) {
        <p class="lo-empty">Routine introuvable. <a routerLink="/sports/routines">Voir les routines</a></p>
      }
    </div>
  `,
  styles: [`
    .run-page { max-width: 640px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .main { --mat-button-filled-container-color: var(--mat-sys-primary); --mat-button-filled-label-text-color: var(--mat-sys-on-primary); min-height: 56px; min-width: 220px; font: var(--mat-sys-title-medium); }
    .overall { height: 6px; overflow: hidden; border-radius: 3px; background: var(--mat-sys-secondary-container); }
    .overall > span { display: block; height: 100%; background: var(--mat-sys-primary); transition: width .3s; }
    .overall-text { margin-top: -8px; font: var(--mat-sys-label-medium); }
    .step { display: flex; flex-direction: column; align-items: center; gap: 6px; text-align: center; }
    .photo { border-radius: 16px; max-width: 100%; }
    .position { margin-top: 4px; }
    .step-name { margin: 0; font: var(--mat-sys-headline-small); }
    .step-target { color: var(--mat-sys-primary); font: var(--mat-sys-title-large); }
    .instructions { margin: 0; max-width: 48ch; }
    .clock { display: flex; width: 100%; max-width: 360px; flex-direction: column; align-items: center; gap: 4px; }
    .clock-label { color: var(--mat-sys-tertiary); font: var(--mat-sys-title-medium); }
    .clock-time { font: var(--mat-sys-display-large); font-size: 64px; line-height: 1.1; font-variant-numeric: tabular-nums; }
    .clock.is-lead .clock-time { color: var(--mat-sys-tertiary); }
    .clock-bar { width: 100%; height: 10px; overflow: hidden; border-radius: 5px; background: var(--mat-sys-surface-container-high); }
    .clock-bar > span { display: block; height: 100%; background: var(--mat-sys-primary); }
    .clock.is-lead .clock-bar > span { background: var(--mat-sys-tertiary); }
    .actions { display: flex; flex-direction: column; align-items: center; gap: 8px; margin-top: 8px; }
    .next { margin: 8px 0 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .footer { display: flex; justify-content: center; }
    .line { gap: 12px; padding: 8px 16px 8px 8px; }
    .line mat-icon { color: var(--mat-sys-on-surface-variant); }
  `],
})
export class SportRoutineRunPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(SportsApiService)
  private readonly route = inject(ActivatedRoute)
  private readonly router = inject(Router)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly signals = new RoutineSignals()
  private settings: SportSettingsDto = { memberProfileId: '', defaultEffort: 7, defaultDiscomfort: 2 }
  private timer: ReturnType<typeof setInterval> | null = null
  private endsAt = 0
  /** Exercises ticked during this run, latest last, for « Annuler la série précédente ». */
  private readonly ticked = signal<string[]>([])

  readonly loading = signal(true)
  readonly error = signal('')
  readonly pending = signal('')
  readonly report = signal<RoutineReportDto | null>(null)
  readonly pass = computed(() => { const report = this.report(); return report ? openPass(report) : null })
  readonly steps = computed(() => runSteps(this.report()?.routine.lines ?? []))
  readonly rounds = computed(() => roundCount(this.report()?.routine.lines ?? []))
  readonly index = computed(() => currentStepIndex(this.steps(), this.pass()))
  readonly step = computed<RunStep | null>(() => this.steps()[this.index()] ?? null)
  readonly next = computed(() => nextLabel(nextStep(this.steps(), this.index(), this.pass()), this.rounds(), this.step()))
  readonly undoable = computed(() => this.ticked().length > 0)
  readonly phase = signal<Phase>('idle')
  readonly paused = signal(false)
  readonly remainingMs = signal(0)
  readonly totalMs = signal(0)
  readonly photoSize = globalThis.matchMedia?.('(max-width: 760px)').matches ? 150 : 240

  readonly setTarget = setTarget
  readonly targetText = targetText
  readonly runSummary = runSummary
  readonly nextDayLabel = nextDayLabel

  async ngOnInit(): Promise<void> {
    try {
      await this.reload()
      try { this.settings = await firstValueFrom(this.api.settings()) } catch { /* 7 and 2 */ }
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
    void this.signals.keepAwake()
  }

  ngOnDestroy(): void {
    this.clearTimer()
    this.signals.release()
  }

  position(step: RunStep): string { return stepPosition(step, this.rounds(), this.report()?.routine.lines.length ?? 0) }
  clockText(step: RunStep): string { return countdownText(this.phase() === 'idle' ? step.line.target * 1000 : this.remainingMs()) }
  clockPercent(): number { return this.phase() === 'idle' ? 0 : elapsedPercent(this.remainingMs(), this.totalMs()) }
  clockAria(): string {
    const seconds = Math.max(0, Math.ceil(this.remainingMs() / 1000))
    return this.phase() === 'idle' ? `${this.step()?.line.target ?? 0} secondes` : `${seconds} secondes restantes`
  }

  async startPass(): Promise<void> {
    const report = this.report()
    this.signals.unlock()
    if (report) await this.call('start', this.api.startSession(report.routine.id))
  }

  /** « Préparez-vous » for a few seconds, then the hold; the set is ticked when it reaches 0. */
  startTimer(step: RunStep): void {
    this.signals.unlock()
    this.paused.set(false)
    this.runPhase('lead', LEAD_SECONDS * 1000, () => {
      this.signals.go()
      this.runPhase('hold', step.line.target * 1000, () => {
        this.signals.done()
        this.phase.set('idle')
        void this.tick(step)
      })
    })
  }

  togglePause(): void {
    if (this.phase() === 'idle') return
    if (this.paused()) {
      this.endsAt = Date.now() + this.remainingMs()
      this.paused.set(false)
    } else {
      this.remainingMs.set(Math.max(0, this.endsAt - Date.now()))
      this.paused.set(true)
    }
  }

  stopTimer(): void {
    this.clearTimer()
    this.paused.set(false)
    this.phase.set('idle')
  }

  /** Ticks one more set of the step's exercise; ends the run when the pass is finished. */
  async tick(step: RunStep): Promise<void> {
    const report = this.report(), pass = this.pass()
    if (!report || !pass) return
    const exerciseId = step.line.exerciseId
    const after = await this.call(exerciseId, this.api.setSets(report.routine.id, pass.id, exerciseId, setsDone(pass, exerciseId) + 1))
    if (!after) return
    this.ticked.update(list => [...list, exerciseId])
    const session = after.todaySessions.find(item => item.id === pass.id)
    if (session?.finished) await this.finish(report, after, session)
  }

  async undo(): Promise<void> {
    const report = this.report(), pass = this.pass(), exerciseId = this.ticked().at(-1)
    if (!report || !pass || !exerciseId) return
    if (await this.call(exerciseId, this.api.setSets(report.routine.id, pass.id, exerciseId, Math.max(0, setsDone(pass, exerciseId) - 1)))) {
      this.ticked.update(list => list.slice(0, -1))
    }
  }

  private async finish(before: RoutineReportDto, after: RoutineReportDto, session: RoutineSessionDto): Promise<void> {
    const message = routineCelebration(before, after)
    if (message) this.snack.open(message, undefined, { duration: 5000 })
    const saved = await firstValueFrom(openEditor<RoutineFeelingDialogComponent, FeelingDialogData, RoutineReportDto>(
      this.dialog, RoutineFeelingDialogComponent, { report: after, session, settings: this.settings }).afterClosed())
    if (saved) this.snack.open('Ressenti enregistré', undefined, { duration: 4000 })
    await this.router.navigate(['/sports/routine', after.routine.id])
  }

  private runPhase(phase: Exclude<Phase, 'idle'>, durationMs: number, onEnd: () => void): void {
    this.clearTimer()
    this.phase.set(phase)
    this.totalMs.set(durationMs)
    this.remainingMs.set(durationMs)
    this.endsAt = Date.now() + durationMs
    this.timer = setInterval(() => {
      if (this.paused()) return
      const previous = this.remainingMs(), remaining = this.endsAt - Date.now()
      this.remainingMs.set(Math.max(0, remaining))
      if (remaining <= 0) { this.clearTimer(); onEnd(); return }
      if (warningSecond(previous, remaining) !== null) this.signals.tick()
    }, 100)
  }

  private clearTimer(): void {
    if (this.timer) clearInterval(this.timer)
    this.timer = null
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
