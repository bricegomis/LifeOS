import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { ExerciseImageComponent } from './exercise-image.component'
import {
  daysLabel, doneText, kilos, measureUnit, nextDayLabel, routineCelebration, stepLoad, stepValue, targetText, valueStep,
} from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { ExerciseCompletionDto, RoutineLineDto, RoutineReportDto } from './sports.models'

interface TickDraft { exerciseId: string, value: number, loadKg: number | null }

/** Doing a routine (ADR 0014): each exercise with its photo, instructions and target; one tick per exercise. */
@Component({
  selector: 'app-sport-routine-page',
  standalone: true,
  imports: [RouterLink, ExerciseImageComponent],
  template: `
    <div class="page page-narrow">
      <a class="back-link" routerLink="/sports"><i class="pi pi-arrow-left" aria-hidden="true"></i> Sport</a>
      @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="status-text" role="status">Chargement…</p> }
      @else if (report(); as report) {
        <div class="page-header">
          <div><h1>{{ report.routine.name }}</h1>
            <p class="muted">{{ daysLabel(report.routine.days) }} · série {{ report.streak }} j</p></div>
          @if (report.plannedToday) {
            <span class="chip" [class.chip-done]="report.completedToday" [class.chip-neutral]="!report.completedToday">{{ report.doneToday }} / {{ report.totalToday }}</span>
          }
        </div>
        @if (banner()) { <div class="celebration" role="status"><i class="pi pi-check" aria-hidden="true"></i><span>{{ banner() }}</span></div> }
        @if (!report.plannedToday) {
          <p class="notice" role="status">Cette routine n'est pas prévue aujourd'hui. {{ nextDayLabel(report.today, report.nextPlannedDay) }}</p>
        }
        @for (line of report.routine.lines; track line.exerciseId; let index = $index) {
          <article class="exercise-card" [class.is-done]="!!completion(line)" [attr.aria-labelledby]="'exercise-' + line.exerciseId">
            <div class="exercise-head">
              <app-exercise-image [exerciseId]="line.exerciseId" [hasImage]="line.hasImage" [alt]="'Photo : ' + line.exerciseName" />
              <div class="exercise-text">
                <span class="muted small">Exercice {{ index + 1 }} sur {{ report.routine.lines.length }}</span>
                <strong [id]="'exercise-' + line.exerciseId">{{ line.exerciseName }}</strong>
                <span class="exercise-target">Cible : {{ targetText(line) }}</span>
              </div>
            </div>
            @if (line.instructions) {
              <button type="button" class="link-button exercise-toggle" [attr.aria-expanded]="open().has(line.exerciseId)" (click)="toggle(line)">
                {{ open().has(line.exerciseId) ? 'Masquer la consigne' : 'Voir la consigne' }}</button>
              @if (open().has(line.exerciseId)) { <p class="exercise-instructions">{{ line.instructions }}</p> }
            }
            @if (report.plannedToday) {
              @if (draft()?.exerciseId === line.exerciseId) {
                <div class="tick-editor">
                  <div class="stepper-row"><span>{{ line.measure === 'hold' ? 'Tenue' : 'Répétitions' }}</span>
                    <div class="stepper">
                      <button type="button" [attr.aria-label]="'Moins ' + valueStep(line.measure)" (click)="stepValue(line, -1)">−</button>
                      <output aria-live="polite">{{ draft()!.value }} {{ measureUnit(line.measure) }}</output>
                      <button type="button" [attr.aria-label]="'Plus ' + valueStep(line.measure)" (click)="stepValue(line, 1)">+</button>
                    </div></div>
                  @if (line.withLoad) {
                    <div class="stepper-row"><span>Charge</span>
                      <div class="stepper">
                        <button type="button" aria-label="Moins 0,5 kg" (click)="stepLoad(-1)">−</button>
                        <output aria-live="polite">{{ kilos(draft()!.loadKg ?? 0) }} kg</output>
                        <button type="button" aria-label="Plus 0,5 kg" (click)="stepLoad(1)">+</button>
                      </div></div>
                  }
                  <div class="tick-actions">
                    <button type="button" class="lifeos-button tick-main" [disabled]="pending()" (click)="saveDraft()">Valider</button>
                    <button type="button" class="lifeos-button lifeos-button-secondary" (click)="draft.set(null)">Annuler</button>
                  </div>
                </div>
              } @else if (completion(line); as done) {
                <div class="tick-done">
                  <span class="tick-done-text"><i class="pi pi-check" aria-hidden="true"></i> {{ doneText(line, done) }}</span>
                  <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="pending()" (click)="edit(line, done)"
                    [attr.aria-label]="'Modifier ' + line.exerciseName">Modifier</button>
                  <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="pending()" (click)="untick(line)"
                    [attr.aria-label]="'Décocher ' + line.exerciseName">Décocher</button>
                </div>
              } @else {
                <div class="tick-actions">
                  <button type="button" class="lifeos-button tick-main" [disabled]="pending()" (click)="tick(line)"
                    [attr.aria-label]="'Fait : ' + line.exerciseName"><i class="pi pi-check" aria-hidden="true"></i> Fait</button>
                  <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="pending()" (click)="edit(line, null)"
                    [attr.aria-label]="'Autre valeur pour ' + line.exerciseName">Autre valeur</button>
                </div>
              }
            }
          </article>
        }
        @if (report.plannedToday) {
          <p class="muted small">Cocher valide l'exercice avec la cible. Une valeur plus basse compte quand même : les chiffres servent au suivi.</p>
        }
      } @else if (!error()) {
        <p class="empty-state">Routine introuvable ou arrêtée.</p>
      }
    </div>
  `,
  styleUrls: ['./sports.css', './routines.css'],
})
export class SportRoutinePageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly route = inject(ActivatedRoute)
  readonly loading = signal(true)
  readonly error = signal('')
  readonly banner = signal('')
  readonly pending = signal(false)
  readonly report = signal<RoutineReportDto | null>(null)
  readonly open = signal(new Set<string>())
  readonly draft = signal<TickDraft | null>(null)
  private readonly completions = computed(() => new Map((this.report()?.todayCompletions ?? []).map(item => [item.exerciseId, item])))

  readonly daysLabel = daysLabel
  readonly targetText = targetText
  readonly doneText = doneText
  readonly nextDayLabel = nextDayLabel
  readonly measureUnit = measureUnit
  readonly valueStep = valueStep
  readonly kilos = kilos

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id')
    try { this.report.set((await firstValueFrom(this.api.routines(1))).find(item => item.routine.id === id) ?? null) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }

  completion(line: RoutineLineDto): ExerciseCompletionDto | null { return this.completions().get(line.exerciseId) ?? null }

  toggle(line: RoutineLineDto): void {
    this.open.update(current => {
      const next = new Set(current)
      if (!next.delete(line.exerciseId)) next.add(line.exerciseId)
      return next
    })
  }

  edit(line: RoutineLineDto, done: ExerciseCompletionDto | null): void {
    this.draft.set({
      exerciseId: line.exerciseId, value: done?.value ?? line.target,
      loadKg: line.withLoad ? done?.loadKg ?? line.loadKg ?? 0 : null,
    })
  }
  stepValue(line: RoutineLineDto, direction: 1 | -1): void {
    this.draft.update(draft => draft && { ...draft, value: stepValue(draft.value, line.measure, direction) })
  }
  stepLoad(direction: 1 | -1): void {
    this.draft.update(draft => draft && { ...draft, loadKg: stepLoad(draft.loadKg ?? 0, direction) })
  }

  async tick(line: RoutineLineDto): Promise<void> {
    await this.update(routineId => this.api.completeExercise(routineId, line.exerciseId))
  }
  async saveDraft(): Promise<void> {
    const draft = this.draft()
    if (!draft) return
    if (await this.update(routineId => this.api.completeExercise(routineId, draft.exerciseId, draft.value, draft.loadKg))) this.draft.set(null)
  }
  async untick(line: RoutineLineDto): Promise<void> {
    await this.update(routineId => this.api.uncompleteExercise(routineId, line.exerciseId))
  }

  private async update(call: (routineId: string) => ReturnType<SportsApiService['completeExercise']>): Promise<boolean> {
    const before = this.report()
    if (!before || this.pending()) return false
    this.pending.set(true); this.error.set('')
    try {
      const after = await firstValueFrom(call(before.routine.id))
      this.report.set(after)
      this.banner.set(after.completedToday ? routineCelebration(before, after) || this.banner() : '')
      return true
    } catch (error) { this.error.set(apiErrorMessage(error)); return false }
    finally { this.pending.set(false) }
  }
}
