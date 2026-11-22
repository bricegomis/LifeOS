import { Component, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { RoutineDialogComponent, type RoutineDialogData, type RoutineDialogResult } from './routine-dialog.component'
import { RoutineDoneDialogComponent, type RoutineDoneDialogData } from './routine-done-dialog.component'
import { recurrenceLabel } from './routine-rules'
import { TaskImageComponent } from './task-image.component'
import { dayLabel, durationLabel, priorityLabel } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { RoutinePassDto, TaskRoutineDetailDto } from './tasks.models'
import { TodoActionsService } from './todo-actions.service'

/** One household routine (ADR 0017): next due day, streak, Fait / Sauter / Pause, details and history. */
@Component({
  selector: 'app-routine-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink, TaskImageComponent],
  template: `
    <div class="lo-page routine-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (detail(); as d) {
        @let r = d.routine;
        <header class="head">
          <a mat-icon-button routerLink="/tasks/routines" aria-label="Retour aux routines"><mat-icon svgIcon="arrow_back" /></a>
          <h1 class="lo-bar-title head-title">Routine</h1>
          @if (r.state !== 'stopped') {
            <button mat-flat-button type="button" class="lo-desktop-only" (click)="edit()"><mat-icon svgIcon="edit" />Modifier</button>
            <button mat-icon-button type="button" class="lo-phone-only" (click)="edit()" aria-label="Modifier la routine" title="Modifier la routine">
              <mat-icon svgIcon="edit" /></button>
          }
        </header>

        <div class="hero">
          <app-task-image kind="routines" [todoId]="r.id" [hasImage]="r.hasImage" [title]="r.title" [version]="r.updatedAt" [size]="96"
            [alt]="r.hasImage ? 'Photo : ' + r.title : 'Pas de photo'" />
          <div class="hero-text">
            <h2 class="hero-title">{{ r.title }}</h2>
            <span class="lo-muted">{{ recurrence(d) }}</span>
            <span class="lo-tags">
              @if (r.state === 'paused') { <span class="lo-tag">En pause</span> }
              @if (r.state === 'stopped') { <span class="lo-tag">Arrêtée</span> }
              @if (r.isLate) { <span class="lo-tag is-off">En retard</span> }
              @if (r.priority === 'high') { <span class="lo-tag is-off">Priorité haute</span> }
              @if (r.priority === 'low') { <span class="lo-tag">Priorité basse</span> }
            </span>
          </div>
        </div>

        @if (r.state === 'active') {
          <div class="actions">
            <button mat-flat-button type="button" [disabled]="busy()" (click)="done()"><mat-icon svgIcon="check" />Fait…</button>
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="skip()"><mat-icon svgIcon="redo" />Sauter</button>
          </div>
        } @else if (r.state === 'paused') {
          <div class="actions"><button mat-flat-button type="button" [disabled]="busy()" (click)="resume()"><mat-icon svgIcon="play_arrow" />Reprendre</button></div>
        }
        @if (r.isLate && r.nextDueOn) {
          <p class="lo-notice is-off" role="status">Échéance {{ day(r.nextDueOn).toLocaleLowerCase('fr') }} pas encore faite. Les échéances suivantes ne s’empilent pas : faites-la ou sautez-la.</p>
        }

        <div class="lo-stats">
          <div class="lo-stat"><span class="lo-stat-value">{{ r.streak }}</span><span class="lo-stat-label">Série en cours</span></div>
          <div class="lo-stat"><span class="lo-stat-value">{{ r.record }}</span><span class="lo-stat-label">Meilleure série</span></div>
          <div class="lo-stat"><span class="lo-stat-value">{{ r.lastDoneOn ? day(r.lastDoneOn) : '—' }}</span><span class="lo-stat-label">Dernière fois</span></div>
        </div>

        <section class="lo-card details" aria-label="Détails">
          <div class="detail"><mat-icon svgIcon="today" /><div><span class="lo-field-label">Prochaine échéance</span>
            <span [class.is-late]="r.isLate">{{ r.nextDueOn ? day(r.nextDueOn) : (r.state === 'paused' ? 'En pause' : 'Arrêtée') }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="event_repeat" /><div><span class="lo-field-label">Récurrence</span>
            <span>{{ recurrence(d) }}, à partir du {{ day(r.startOn).toLocaleLowerCase('fr') }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="timer" /><div><span class="lo-field-label">Durée estimée</span>
            <span>{{ duration(r.durationMinutes) || 'Non renseignée' }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="flag" /><div><span class="lo-field-label">Priorité</span><span>{{ priority(r.priority) }}</span></div></div>
          <div class="detail"><mat-icon svgIcon="sell" /><div><span class="lo-field-label">Étiquettes</span><span>{{ r.tags.join(', ') || 'Aucune' }}</span></div></div>
          @if (r.notes) {
            <div class="detail"><mat-icon svgIcon="notes" /><div><span class="lo-field-label">Notes</span><span class="notes">{{ r.notes }}</span></div></div>
          }
        </section>

        <section class="lo-section" aria-labelledby="history">
          <h2 id="history">Historique</h2>
          @if (d.passes.length) {
            <div class="lo-card">
              @for (pass of d.passes; track pass.id) {
                <div class="pass">
                  <mat-icon [svgIcon]="passIcon(pass)" [class.is-ok]="pass.onTime" />
                  <div class="pass-text"><span>{{ day(pass.day) }}</span><span class="lo-muted">{{ passLine(pass) }}</span></div>
                  <button mat-icon-button type="button" [disabled]="busy()" (click)="removePass(pass)" [attr.aria-label]="'Supprimer le passage du ' + day(pass.day).toLocaleLowerCase('fr')"
                    title="Supprimer le passage"><mat-icon svgIcon="delete" /></button>
                </div>
              }
            </div>
          } @else {
            <p class="lo-muted">Pas encore de passage.</p>
          }
        </section>

        @if (r.state === 'active') {
          <div><button mat-button type="button" [disabled]="busy()" (click)="pause()"><mat-icon svgIcon="pause" />Mettre en pause</button></div>
        }
      } @else if (!error()) {
        <p class="lo-muted" role="status">Chargement…</p>
      }
    </div>
  `,
  styles: [`
    .routine-page { max-width: 880px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .hero { display: flex; align-items: center; gap: 16px; }
    .hero-text { display: flex; min-width: 0; flex-direction: column; gap: 6px; }
    .hero-title { margin: 0; font: var(--mat-sys-title-large); font-weight: 600; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .lo-stat { display: flex; flex-direction: column; }
    .details { display: flex; flex-direction: column; }
    .detail { display: flex; align-items: flex-start; gap: 12px; padding: 12px 16px; }
    .detail + .detail { border-top: 1px solid var(--mat-sys-outline-variant); }
    .detail mat-icon { flex: 0 0 auto; color: var(--mat-sys-on-surface-variant); }
    .detail > div { display: flex; min-width: 0; flex-direction: column; }
    .is-late { color: var(--mat-sys-error); }
    .notes { white-space: pre-line; }
    .pass { display: flex; align-items: center; gap: 12px; padding: 8px 4px 8px 16px; }
    .pass + .pass { border-top: 1px solid var(--mat-sys-outline-variant); }
    .pass mat-icon { flex: 0 0 auto; color: var(--mat-sys-outline); }
    .pass mat-icon.is-ok { color: var(--mat-sys-primary); }
    .pass-text { display: flex; min-width: 0; flex: 1 1 auto; flex-direction: column; }
  `],
})
export class RoutinePageComponent implements OnInit {
  private readonly api = inject(TasksApiService)
  private readonly actions = inject(TodoActionsService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? ''
  readonly detail = signal<TaskRoutineDetailDto | null>(null)
  readonly today = signal(new Date().toISOString().slice(0, 10))
  readonly error = signal('')
  readonly busy = signal(false)
  readonly duration = durationLabel
  readonly priority = priorityLabel

  async ngOnInit(): Promise<void> {
    try { this.today.set((await firstValueFrom(this.api.today())).today) } catch { /* local day */ }
    await this.load()
  }

  day(iso: string): string { return dayLabel(iso, this.today()) }
  recurrence(d: TaskRoutineDetailDto): string { return recurrenceLabel(d.routine.recurrence) }
  passIcon(pass: RoutinePassDto): string { return pass.kind === 'skipped' ? 'redo' : pass.onTime ? 'check_circle' : 'history' }
  passLine(pass: RoutinePassDto): string {
    const what = pass.kind === 'skipped' ? 'Sauté, série remise à zéro' : pass.onTime ? 'Fait' : 'Fait en retard, série remise à zéro'
    return `${what} · échéance ${this.day(pass.dueOn).toLocaleLowerCase('fr')}${pass.note ? ' · ' + pass.note : ''}`
  }

  async done(): Promise<void> {
    const d = this.detail()
    if (!d) return
    const data: RoutineDoneDialogData = { routine: d.routine, today: this.today() }
    const result = await firstValueFrom(openEditor<RoutineDoneDialogComponent, RoutineDoneDialogData, RoutineDialogResult>(this.dialog, RoutineDoneDialogComponent, data).afterClosed())
    if (!result) return
    this.detail.set(result.detail)
    this.snack.open(result.message, undefined, { duration: 4000 })
  }

  async skip(): Promise<void> {
    const d = this.detail()
    if (!d) return
    this.busy.set(true)
    const changed = await this.actions.closeRoutine(d.routine, 'skip', () => void this.load())
    if (changed) this.detail.set(changed)
    this.busy.set(false)
  }

  pause(): Promise<void> { return this.run(this.api.pauseRoutine(this.id), 'Routine en pause : plus d’échéance, la série est gardée') }
  resume(): Promise<void> { return this.run(this.api.resumeRoutine(this.id), 'Routine reprise') }
  removePass(pass: RoutinePassDto): Promise<void> { return this.run(this.api.removePass(this.id, pass.id), 'Passage supprimé, échéance recalculée') }

  async edit(): Promise<void> {
    const d = this.detail()
    if (!d) return
    let tags: string[] = []
    try { tags = await firstValueFrom(this.api.tags()) } catch { tags = d.routine.tags }
    const data: RoutineDialogData = { routine: d.routine, today: this.today(), tags }
    const result = await firstValueFrom(openEditor<RoutineDialogComponent, RoutineDialogData, RoutineDialogResult>(this.dialog, RoutineDialogComponent, data).afterClosed())
    if (!result) return
    this.detail.set(result.detail)
    this.snack.open(result.message, undefined, { duration: 4000 })
  }

  private async run(request: Observable<TaskRoutineDetailDto>, message: string): Promise<void> {
    this.busy.set(true)
    try {
      this.detail.set(await firstValueFrom(request))
      this.snack.open(message, undefined, { duration: 4000 })
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
    } finally {
      this.busy.set(false)
    }
  }

  private async load(): Promise<void> {
    try { this.detail.set(await firstValueFrom(this.api.routine(this.id))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
