import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { ExerciseImageComponent } from './exercise-image.component'
import { exerciseLogRequest, logValueLabel, optionalLoad } from './routine-format'
import {
  countdownText, elapsedPercent, LEAD_SECONDS, runSteps, setCount, setKey, setNumber, setsCompleted, setTarget, sideLabel, soloLine,
  warningSecond, type RunStep,
} from './routine-run'
import { RoutineSignals } from './routine-signals'
import { SportsApiService } from './sports-api.service'
import type { ExerciseDto } from './sports.models'

type Phase = 'idle' | 'lead' | 'hold'

/** The Paris day, "2026-11-27". */
function parisToday(): string { return new Intl.DateTimeFormat('fr-CA', { timeZone: 'Europe/Paris' }).format(new Date()) }

/**
 * A held exercise done alone, outside any routine, on the chrono (ADR 0014, « Exercice fait seul »): the
 * sets one after the other like in the guided run of a routine (5 s to get ready, beeps, left then right
 * side), then the sets done are noted in the exercise's progress. Nothing is saved before the end, or
 * before « Terminer » for the sets already done.
 */
@Component({
  selector: 'app-sport-exercise-run-page',
  standalone: true,
  imports: [ExerciseImageComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, RouterLink],
  template: `
    <div class="lo-page run-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="lo-muted" role="status">Chargement…</p> }
      @else if (exercise(); as exercise) {
        <header class="head">
          <a mat-icon-button routerLink="/sports/exercises" aria-label="Quitter le chrono"><mat-icon svgIcon="close" /></a>
          <div class="lo-page-title">
            <h1>{{ exercise.name }}</h1>
            <span class="lo-muted">Hors routine, au chrono</span>
          </div>
        </header>

        @if (exercise.measure !== 'hold') {
          <p class="lo-muted">Cet exercice se compte en répétitions : notez-le depuis la liste des exercices.</p>
        } @else if (!line()) {
          <section class="lo-section setup" aria-labelledby="setup-title">
            <app-exercise-image class="photo" [exerciseId]="exercise.id" [hasImage]="exercise.hasImage" [size]="photoSize" [alt]="'Photo : ' + exercise.name" />
            <h2 id="setup-title" class="sr-only">Réglages</h2>
            @if (exercise.instructions) { <p class="lo-muted instructions">{{ exercise.instructions }}</p> }
            <form class="lo-form" (ngSubmit)="begin(exercise)">
              <div class="row">
                <mat-form-field appearance="outline">
                  <mat-label>Séries</mat-label>
                  <input matInput type="number" name="sets" required min="1" max="20" step="1" inputmode="numeric" [(ngModel)]="sets" />
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>{{ valueLabel(exercise) }}</mat-label>
                  <input matInput type="number" name="value" required min="1" max="10000" step="1" inputmode="numeric" [(ngModel)]="seconds" />
                </mat-form-field>
              </div>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Charge (kg)</mat-label>
                <input matInput type="number" name="load" min="0" max="500" step="0.5" inputmode="decimal" placeholder="Poids du corps" [(ngModel)]="load" />
                <mat-hint>Vide ou 0 = au poids du corps.</mat-hint>
              </mat-form-field>
              <p class="lo-help">{{ leadSeconds }} s pour se mettre en place, un bip sur les 3 dernières secondes et à la fin de chaque série
                @if (exercise.perSide) { ; le côté droit démarre seul après le gauche, avec {{ leadSeconds }} s pour changer de côté }.
                Les séries faites sont notées dans la progression de l’exercice.</p>
              <div><button mat-flat-button type="submit" class="main"><mat-icon svgIcon="play_arrow" />Démarrer le chrono</button></div>
            </form>
          </section>
        } @else if (step(); as step) {
          <div class="overall" role="progressbar" aria-valuemin="0" [attr.aria-valuemax]="steps().length" [attr.aria-valuenow]="index()"
            [attr.aria-label]="'Étapes faites : ' + index() + ' sur ' + steps().length"><span [style.width.%]="index() * 100 / steps().length"></span></div>
          <span class="lo-muted overall-text">Série {{ setNumber(steps(), index()) }} sur {{ setCount(steps()) }}</span>

          <article class="step" aria-labelledby="step-name">
            <app-exercise-image class="photo" [exerciseId]="exercise.id" [hasImage]="exercise.hasImage" [size]="photoSize" [alt]="'Photo : ' + exercise.name" />
            <h2 id="step-name" class="step-name">{{ exercise.name }}</h2>
            <span class="step-target">{{ setTarget(step.line) }}</span>
            @if (step.side) { <span class="side" [class.is-right]="step.side === 'right'"><mat-icon [svgIcon]="step.side === 'left' ? 'arrow_back' : 'arrow_forward'" />{{ sideLabel(step.side) }}</span> }
            <div class="clock" [class.is-lead]="phase() === 'lead'" aria-live="polite">
              @if (phase() === 'lead') { <span class="clock-label">{{ leadLabel() }}</span> }
              <span class="clock-time">{{ clockText(step) }}</span>
              <div class="clock-bar" aria-hidden="true"><span [style.width.%]="clockPercent()"></span></div>
            </div>
            <div class="actions">
              @if (phase() === 'idle') {
                <button mat-flat-button type="button" class="main" (click)="startTimer(step)"><mat-icon svgIcon="play_arrow" />Démarrer le chrono</button>
              } @else {
                <button mat-flat-button type="button" class="main" (click)="togglePause()">
                  <mat-icon [svgIcon]="paused() ? 'play_arrow' : 'pause'" />{{ paused() ? 'Reprendre' : 'Pause' }}</button>
                <button mat-button type="button" (click)="stopTimer()"><mat-icon svgIcon="restart_alt" />Recommencer la série</button>
              }
            </div>
          </article>

          <div class="footer">
            <button mat-button type="button" [disabled]="saving() || phase() !== 'idle' || !done()" (click)="save()">
              <mat-icon svgIcon="check" />Terminer : noter {{ done() }} série{{ done() > 1 ? 's' : '' }}</button>
          </div>
        }
      } @else if (!error()) {
        <p class="lo-empty">Exercice introuvable. <a routerLink="/sports/exercises">Voir les exercices</a></p>
      }
    </div>
  `,
  styles: [`
    .run-page { max-width: 640px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .main { --mat-button-filled-container-color: var(--mat-sys-primary); --mat-button-filled-label-text-color: var(--mat-sys-on-primary); min-height: 56px; min-width: 220px; font: var(--mat-sys-title-medium); }
    .setup { align-items: stretch; }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .overall { height: 6px; overflow: hidden; border-radius: 3px; background: var(--mat-sys-secondary-container); }
    .overall > span { display: block; height: 100%; background: var(--mat-sys-primary); transition: width .3s; }
    .overall-text { margin-top: -8px; font: var(--mat-sys-label-medium); }
    .step { display: flex; flex-direction: column; align-items: center; gap: 6px; text-align: center; }
    .photo { border-radius: 16px; max-width: 100%; }
    .step-name { margin: 0; font: var(--mat-sys-headline-small); }
    .step-target { color: var(--mat-sys-primary); font: var(--mat-sys-title-large); }
    .instructions { margin: 0; max-width: 48ch; }
    .side {
      display: inline-flex; align-items: center; gap: 6px; padding: 4px 14px; border-radius: 16px;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); font: var(--mat-sys-title-medium);
    }
    .side.is-right { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .clock { display: flex; width: 100%; max-width: 360px; flex-direction: column; align-items: center; gap: 4px; }
    .clock-label { color: var(--mat-sys-tertiary); font: var(--mat-sys-title-medium); }
    .clock-time { font: var(--mat-sys-display-large); font-size: 64px; line-height: 1.1; font-variant-numeric: tabular-nums; }
    .clock.is-lead .clock-time { color: var(--mat-sys-tertiary); }
    .clock-bar { width: 100%; height: 10px; overflow: hidden; border-radius: 5px; background: var(--mat-sys-surface-container-high); }
    .clock-bar > span { display: block; height: 100%; background: var(--mat-sys-primary); }
    .clock.is-lead .clock-bar > span { background: var(--mat-sys-tertiary); }
    .actions { display: flex; flex-direction: column; align-items: center; gap: 8px; margin-top: 8px; }
    .footer { display: flex; justify-content: center; }
  `],
})
export class SportExerciseRunPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(SportsApiService)
  private readonly route = inject(ActivatedRoute)
  private readonly router = inject(Router)
  private readonly snack = inject(MatSnackBar)
  private readonly signals = new RoutineSignals()
  private timer: ReturnType<typeof setInterval> | null = null
  private endsAt = 0
  private readonly today = parisToday()

  readonly loading = signal(true)
  readonly error = signal('')
  readonly saving = signal(false)
  readonly exercise = signal<ExerciseDto | null>(null)
  /** Set once « Démarrer » is pressed with valid settings. */
  readonly line = signal<ReturnType<typeof soloLine> | null>(null)
  readonly steps = computed(() => { const line = this.line(); return line ? runSteps([line]) : [] })
  readonly index = signal(0)
  readonly step = computed<RunStep | null>(() => this.steps()[this.index()] ?? null)
  readonly done = computed(() => setsCompleted(this.steps(), this.index()))
  readonly phase = signal<Phase>('idle')
  readonly paused = signal(false)
  readonly remainingMs = signal(0)
  readonly totalMs = signal(0)
  readonly leadLabel = signal('Préparez-vous')
  readonly photoSize = globalThis.matchMedia?.('(max-width: 760px)').matches ? 150 : 240
  sets: number | string | null = 1
  seconds: number | string | null = null
  load: number | string | null = null

  readonly setTarget = setTarget
  readonly setCount = setCount
  readonly setNumber = setNumber
  readonly sideLabel = sideLabel
  readonly valueLabel = logValueLabel
  readonly leadSeconds = LEAD_SECONDS

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id'), query = this.route.snapshot.queryParamMap
    try {
      const exercise = (await firstValueFrom(this.api.exercises())).find(item => item.id === id) ?? null
      this.exercise.set(exercise)
      this.sets = query.get('sets') ?? 1
      this.seconds = query.get('value')
      this.load = query.has('load') ? query.get('load') : exercise?.defaultLoadKg ?? null
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }

  ngOnDestroy(): void {
    this.clearTimer()
    this.signals.release()
  }

  /** Checks the settings, then starts the first set at once (the tap also unlocks the sound). */
  begin(exercise: ExerciseDto): void {
    const request = exerciseLogRequest({ date: this.today, setsDone: this.sets, value: this.seconds, loadKg: this.load }, this.today)
    if (typeof request === 'string') { this.error.set(request); return }
    this.error.set('')
    this.line.set(soloLine(exercise, request.setsDone, request.value, optionalLoad(this.load) ?? null))
    this.index.set(0)
    const first = this.step()
    if (first) this.startTimer(first)
  }

  clockText(step: RunStep): string { return countdownText(this.phase() === 'idle' ? step.line.target * 1000 : this.remainingMs()) }
  clockPercent(): number { return this.phase() === 'idle' ? 0 : elapsedPercent(this.remainingMs(), this.totalMs()) }

  /** Lead, hold, then the next step; after a left side the right side follows by itself. */
  startTimer(step: RunStep, lead = 'Préparez-vous'): void {
    this.signals.unlock()
    this.paused.set(false)
    this.leadLabel.set(lead)
    this.runPhase('lead', LEAD_SECONDS * 1000, () => {
      this.signals.go()
      this.runPhase('hold', step.line.target * 1000, () => {
        this.signals.done()
        this.phase.set('idle')
        this.index.update(index => index + 1)
        const next = this.step()
        if (!next) { void this.save(); return }
        if (step.side === 'left' && next.side === 'right' && setKey(next) === setKey(step)) this.startTimer(next, 'Changez de côté')
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

  /** Notes the sets done so far in the exercise's progress, then goes back to the exercises. */
  async save(): Promise<void> {
    const exercise = this.exercise(), line = this.line(), sets = this.done()
    if (!exercise || !line || !sets || this.saving()) return
    this.saving.set(true)
    try {
      // 0 says bodyweight explicitly: null would take the exercise's usual load.
      await firstValueFrom(this.api.logExercise(exercise.id, { date: this.today, setsDone: sets, value: line.target, loadKg: line.loadKg ?? 0 }))
      this.snack.open(`« ${exercise.name} » noté : ${sets} série${sets > 1 ? 's' : ''}`, undefined, { duration: 5000 })
      await this.router.navigate(['/sports/exercises'])
    } catch (error) {
      this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 })
      this.saving.set(false)
    }
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
}
