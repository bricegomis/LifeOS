import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { RouterLink } from '@angular/router'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { ChallengeSetDialogComponent, type ChallengeSetData } from './challenge-set-dialog.component'
import {
  challengeAmount, challengeItems, daySummary, doneItems, openSections, sportRoutineItems, supplementItems, taskItems, withMarks,
  type TodayChallenge, type TodayItem, type TodaySportRoutine, type TodaySupplement, type TodayTasks,
} from './today-items'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

interface RoutinePasses { passes: { id: string, kind: 'done' | 'skipped', day: string }[] }
const enc = encodeURIComponent

/**
 * What is left to tick today (ADR 0021): household todos and routines, sport routines and
 * challenges, supplements. Read from the Tasks, Sport and Health endpoints; ticks call the same
 * endpoints as the module pages. Ticked items fold into « Fait aujourd'hui ».
 */
@Component({
  selector: 'app-today-agenda',
  standalone: true,
  imports: [SkeletonComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="agenda">
      <div class="day">
        <p class="lo-muted">{{ dateLabel() }} · {{ summary().text }}</p>
        <div class="bar" role="progressbar" aria-label="Avancement de la journée" aria-valuemin="0"
          [attr.aria-valuemax]="items().length" [attr.aria-valuenow]="summary().done"><span [style.width.%]="summary().percent"></span></div>
      </div>
      @if (!loaded()) { <app-skeleton [rows]="5" /> }
      @if (loaded() && items().length && !summary().open) {
        <p class="lo-notice all-done"><mat-icon svgIcon="task_alt" />Tout est fait pour aujourd’hui.</p>
      }
      @for (section of sections(); track section.key) {
        <section class="lo-section" [attr.aria-labelledby]="'agenda-' + section.key">
          <div class="head" [class.is-late]="section.key === 'late'">
            <mat-icon [svgIcon]="section.icon" /><h2 [id]="'agenda-' + section.key">{{ section.label }}</h2><span class="count">{{ section.count }}</span>
          </div>
          <div class="lo-list">
            @for (group of section.groups; track $index) {
              @if (group.label) { <div class="moment">{{ group.label }}</div> }
              @for (item of group.items; track item.key) {
                <div class="lo-row item">
                  @if (item.kind === 'sport-routine' || item.kind === 'challenge') {
                    <span class="lead"><span class="lo-lead-icon"><mat-icon [svgIcon]="item.kind === 'challenge' ? 'local_fire_department' : 'self_improvement'" /></span></span>
                  } @else {
                    <button mat-icon-button type="button" class="check" [disabled]="pending().has(item.key)" (click)="check(item)"
                      [attr.aria-label]="'Marquer « ' + item.title + ' » comme fait'" title="Marquer comme fait"><mat-icon svgIcon="radio_button_unchecked" /></button>
                  }
                  <a class="text" [routerLink]="item.link">
                    <span class="lo-row-title">{{ item.title }}</span>
                    <span class="lo-row-sub" [class.is-late]="item.section === 'late'">{{ item.sub }}</span>
                    @if (item.percent !== undefined) { <span class="mini-bar" aria-hidden="true"><span [style.width.%]="item.percent"></span></span> }
                  </a>
                  @if (item.kind === 'sport-routine') {
                    <a mat-flat-button class="tonal" [routerLink]="item.link" [attr.aria-label]="(item.inProgress ? 'Continuer' : 'Démarrer') + ' la routine « ' + item.title + ' »'">
                      <mat-icon svgIcon="play_arrow" />{{ item.inProgress ? 'Continuer' : 'Démarrer' }}</a>
                  }
                  @if (item.kind === 'challenge') {
                    <button mat-flat-button type="button" class="tonal" [disabled]="pending().has(item.key)" (click)="addSet(item)"
                      [attr.aria-label]="'Ajouter une série au défi « ' + item.title + ' »'"><mat-icon svgIcon="add" />Série</button>
                  }
                </div>
              }
            }
          </div>
        </section>
      }
      @if (done().length) {
        <section class="done-section" aria-labelledby="agenda-done">
          <button mat-button type="button" class="done-toggle" [attr.aria-expanded]="doneOpen()" (click)="doneOpen.set(!doneOpen())">
            <mat-icon svgIcon="chevron_right" [class.open]="doneOpen()" /><span id="agenda-done">Fait aujourd’hui ({{ done().length }})</span></button>
          @if (doneOpen()) {
            <div class="lo-list">
              @for (item of done(); track item.key) {
                <div class="lo-row item is-done">
                  @if (item.kind === 'sport-routine') {
                    <span class="lead"><span class="lo-lead-icon"><mat-icon svgIcon="check" /></span></span>
                  } @else {
                    <button mat-icon-button type="button" class="check is-done" [disabled]="pending().has(item.key)" (click)="uncheck(item)"
                      [attr.aria-label]="uncheckLabel(item)" [attr.title]="uncheckLabel(item)"><mat-icon svgIcon="check_circle" /></button>
                  }
                  <a class="text" [routerLink]="item.link"><span class="lo-row-title">{{ item.title }}</span><span class="lo-row-sub">{{ item.sub }}</span></a>
                </div>
              }
            </div>
          }
        </section>
      }
    </div>
  `,
  styles: [`
    .agenda { display: flex; flex-direction: column; gap: 16px; }
    .day { display: flex; flex-direction: column; gap: 8px; }
    .bar, .mini-bar { display: block; overflow: hidden; border-radius: 4px; background: var(--mat-sys-secondary-container); }
    .bar { height: 8px; } .mini-bar { height: 6px; margin-top: 4px; }
    .bar span, .mini-bar span { display: block; height: 100%; background: var(--mat-sys-primary); }
    .all-done { display: flex; align-items: center; gap: 8px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head h2 { margin: 0; font: var(--mat-sys-title-medium); font-weight: 600; }
    .head .count { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-large); }
    .head.is-late { color: var(--mat-sys-error); }
    .moment { padding: 10px 16px 2px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-medium); text-transform: uppercase; letter-spacing: .04em; }
    .item { min-height: 64px; gap: 4px; padding: 4px 12px 4px 4px; }
    .moment + .item { border-top: 0; }
    .check, .lead { flex: 0 0 auto; color: var(--mat-sys-on-surface-variant); }
    .lead { display: inline-flex; width: 48px; justify-content: center; }
    .check.is-done { color: var(--mat-sys-primary); }
    .text { display: flex; min-width: 0; flex: 1 1 auto; flex-direction: column; padding: 6px 4px; color: inherit; text-decoration: none; }
    .text:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: 2px; border-radius: 4px; }
    .lo-row-sub.is-late { color: var(--mat-sys-error); }
    .is-done .lo-row-title { color: var(--mat-sys-on-surface-variant); text-decoration: line-through; }
    .tonal { flex: 0 0 auto; --mat-button-filled-container-color: var(--mat-sys-secondary-container); --mat-button-filled-label-text-color: var(--mat-sys-on-secondary-container); }
    .done-section { display: flex; flex-direction: column; gap: 8px; }
    .done-toggle { align-self: flex-start; }
    .done-toggle mat-icon { transition: transform .15s; } .done-toggle mat-icon.open { transform: rotate(90deg); }
  `],
})
export class TodayAgendaComponent implements OnInit {
  private readonly api = inject(LifeosApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)

  readonly tasks = signal<TodayTasks | null>(null)
  readonly sportRoutines = signal<TodaySportRoutine[]>([])
  readonly challenges = signal<TodayChallenge[]>([])
  readonly supplements = signal<TodaySupplement[]>([])
  readonly loaded = signal(false)
  /** Items waiting for the server: only their own button is disabled. */
  readonly pending = signal(new Set<string>())
  /** Ticks shown before the server answers (ADR 0021 « Cocher depuis l'accueil »). */
  private readonly marks = signal(new Map<string, boolean>())
  readonly doneOpen = signal(false)

  readonly items = computed<TodayItem[]>(() => withMarks([
    ...(this.tasks() ? taskItems(this.tasks()!) : []),
    ...sportRoutineItems(this.sportRoutines()),
    ...challengeItems(this.challenges()),
    ...supplementItems(this.supplements()),
  ], this.marks()))
  readonly sections = computed(() => openSections(this.items()))
  readonly done = computed(() => doneItems(this.items()))
  readonly summary = computed(() => daySummary(this.items()))
  readonly dateLabel = signal(this.formatDate(new Date()))

  async ngOnInit(): Promise<void> { await this.load() }

  uncheckLabel(item: TodayItem): string {
    return item.kind === 'challenge' ? `Retirer la dernière série de « ${item.title} »` : `Remettre « ${item.title} » à faire`
  }

  async check(item: TodayItem): Promise<void> {
    await this.tick(item, true, async () => {
      if (item.kind === 'todo') {
        await this.call(this.api.post(`/tasks/todos/${enc(item.id)}/complete`, { doneOn: null }))
        return () => this.api.post(`/tasks/todos/${enc(item.id)}/reopen`, {})
      }
      if (item.kind === 'task-routine') {
        const detail = await this.call(this.api.post<RoutinePasses>(`/tasks/routines/${enc(item.id)}/done`, { day: null, note: null }))
        const pass = detail.passes.find(p => p.kind === 'done')
        return pass ? () => this.api.delete(`/tasks/routines/${enc(item.id)}/passes/${enc(pass.id)}`) : null
      }
      await this.call(this.api.post(`/health/supplements/${enc(item.id)}/intakes`, { day: null, taken: true }))
      return () => this.api.post(`/health/supplements/${enc(item.id)}/intakes`, { day: null, taken: false })
    })
  }

  async uncheck(item: TodayItem): Promise<void> {
    const ok = await this.tick(item, item.kind === 'challenge' ? null : false, async () => {
      if (item.kind === 'todo') await this.call(this.api.post(`/tasks/todos/${enc(item.id)}/reopen`, {}))
      else if (item.kind === 'supplement') await this.call(this.api.post(`/health/supplements/${enc(item.id)}/intakes`, { day: null, taken: false }))
      else if (item.kind === 'task-routine') {
        const today = this.tasks()?.today
        const detail = await this.call(this.api.get<RoutinePasses>(`/tasks/routines/${enc(item.id)}`))
        const pass = detail.passes.find(p => p.kind === 'done' && p.day === today)
        if (pass) await this.call(this.api.delete(`/tasks/routines/${enc(item.id)}/passes/${enc(pass.id)}`))
      } else if (item.kind === 'challenge') {
        const entry = this.lastEntry(this.challenges().find(c => c.challenge.id === item.id))
        if (entry) await this.call(this.api.delete(`/sport/challenges/${enc(item.id)}/entries/${enc(entry.id)}`))
      }
      return null
    }, false)
    if (ok) this.snack.open(item.kind === 'challenge' ? `Dernière série de « ${item.title} » retirée` : `« ${item.title} » remis à faire`, undefined, { duration: 4000 })
  }

  async addSet(item: TodayItem): Promise<void> {
    const data: ChallengeSetData = { title: item.title, sub: item.sub, unit: item.unit ?? 'reps', steps: item.steps ?? [] }
    const ref = this.dialog.open<ChallengeSetDialogComponent, ChallengeSetData, number>(ChallengeSetDialogComponent, {
      data, width: 'min(400px, calc(100vw - 32px))', autoFocus: 'first-tabbable',
    })
    const quantity = await firstValueFrom(ref.afterClosed())
    if (!quantity) return
    this.setPending(item.key, true)
    try {
      // The answer is the challenge's fresh report: it replaces the line without reloading the page.
      const report = await this.call(this.api.post<TodayChallenge>(`/sport/challenges/${enc(item.id)}/entries`, { quantity }))
      this.challenges.update(list => list.map(c => c.challenge.id === item.id ? { ...c, ...report } : c))
      const entry = this.lastEntry(report)
      const message = report.reachedToday && !item.done
        ? `Défi « ${item.title} » atteint pour aujourd’hui`
        : `+${challengeAmount(data.unit, quantity)} au défi « ${item.title} »`
      this.offerUndo(item, message, entry ? () => this.api.delete(`/sport/challenges/${enc(item.id)}/entries/${enc(entry.id)}`) : null, null)
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
    } finally {
      this.setPending(item.key, false)
    }
  }

  /**
   * Ticks an item at once (`mark`, or no visible change for a challenge), calls the module, then
   * reloads only that module. A failed request drops the mark and says why.
   */
  private async tick(item: TodayItem, mark: boolean | null, work: () => Promise<(() => Observable<unknown>) | null>, undoable = true): Promise<boolean> {
    if (this.pending().has(item.key)) return false
    this.setPending(item.key, true)
    if (mark !== null) this.setMark(item.key, mark)
    try {
      const undo = await work()
      await this.reload(item.kind)
      if (undoable) this.offerUndo(item, `« ${item.title} » fait`, undo, mark === null ? null : !mark)
      return true
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
      return false
    } finally {
      this.setMark(item.key, null)
      this.setPending(item.key, false)
    }
  }

  private lastEntry(report: TodayChallenge | undefined): TodayChallenge['todayEntries'][number] | null {
    return report?.todayEntries.reduce<TodayChallenge['todayEntries'][number] | null>((last, entry) => !last || entry.loggedAt >= last.loggedAt ? entry : last, null) ?? null
  }

  private offerUndo(item: TodayItem, message: string, undo: (() => Observable<unknown>) | null, undoMark: boolean | null): void {
    const ref = this.snack.open(message, undo ? 'Annuler' : undefined, { duration: 6000 })
    if (!undo) return
    ref.onAction().subscribe(() => {
      void this.tick(item, undoMark, async () => { await this.call(undo()); return null }, false)
    })
  }

  private setPending(key: string, on: boolean): void {
    this.pending.update(set => { const next = new Set(set); if (on) next.add(key); else next.delete(key); return next })
  }

  private setMark(key: string, done: boolean | null): void {
    this.marks.update(map => { const next = new Map(map); if (done === null) next.delete(key); else next.set(key, done); return next })
  }

  private call<T>(request: Observable<T>): Promise<T> { return firstValueFrom(request) }

  /** Reloads the module of a ticked item only; errors go to the caller. */
  private async reload(kind: TodayItem['kind']): Promise<void> {
    if (kind === 'todo' || kind === 'task-routine') this.tasks.set(await this.call(this.api.get<TodayTasks>('/tasks/today')))
    else if (kind === 'supplement') this.supplements.set((await this.call(this.api.get<{ supplements: TodaySupplement[] }>('/health/supplements', { includeStopped: false }))).supplements)
    else if (kind === 'challenge') this.challenges.set(await this.call(this.api.get<TodayChallenge[]>('/sport/challenges', { calendarWeeks: 1, includeStopped: false })))
  }

  private async load(): Promise<void> {
    const errors: string[] = []
    const load = async <T>(request: Observable<T>, set: (value: T) => void) => {
      try { set(await firstValueFrom(request)) } catch (error) { errors.push(apiErrorMessage(error)) }
    }
    await Promise.all([
      load(this.api.get<TodayTasks>('/tasks/today'), value => this.tasks.set(value)),
      load(this.api.get<TodaySportRoutine[]>('/sport/routines', { calendarWeeks: 1 }), value => this.sportRoutines.set(value)),
      load(this.api.get<TodayChallenge[]>('/sport/challenges', { calendarWeeks: 1, includeStopped: false }), value => this.challenges.set(value)),
      load(this.api.get<{ supplements: TodaySupplement[] }>('/health/supplements', { includeStopped: false }), value => this.supplements.set(value.supplements)),
    ])
    this.dateLabel.set(this.formatDate(this.tasks() ? new Date(`${this.tasks()!.today}T12:00:00Z`) : new Date()))
    this.loaded.set(true)
    if (errors.length) this.snack.open(errors[0]!, undefined, { duration: 6000 })
  }

  private formatDate(date: Date): string {
    const text = new Intl.DateTimeFormat('fr-FR', { timeZone: 'Europe/Paris', weekday: 'long', day: 'numeric', month: 'long' }).format(date)
    return text.charAt(0).toUpperCase() + text.slice(1)
  }
}
