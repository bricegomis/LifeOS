import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { ExerciseImageComponent } from './exercise-image.component'
import {
  DAY_LETTERS, DAY_NAMES, daysLabel, draftFromRoutine, emptyExerciseDraft, emptyRoutineDraft, exerciseRequest, exerciseSummary, measureLabel,
  moveLine, newLine, routineRequest, routineSummary, type RoutineDraft,
} from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { ExerciseDto, ExerciseRequest, RoutineReportDto } from './sports.models'

/** Routines and the exercise library in the sport settings (ADR 0014); usable on the phone, photo included. */
@Component({
  selector: 'app-routine-settings',
  standalone: true,
  imports: [FormsModule, ExerciseImageComponent],
  template: `
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
    @if (loaded()) {
      <div class="week-grid">
        <section class="sport-section" aria-labelledby="settings-routines">
          <h2 id="settings-routines">Routines</h2>
          @if (routines().length) {
            <div class="settings-list">
              @for (report of routines(); track report.routine.id) {
                <div class="settings-row">
                  <span><strong>{{ report.routine.name }}</strong><span class="muted small">{{ routineSummary(report.routine) }}</span></span>
                  <span class="page-actions">
                    <button type="button" class="lifeos-button lifeos-button-text" [disabled]="busy()" (click)="editRoutine(report)">Modifier</button>
                    <button type="button" class="lifeos-button lifeos-button-text danger-text" [disabled]="busy()" (click)="stopRoutine(report)">Arrêter</button>
                  </span>
                </div>
              }
            </div>
          } @else { <p class="empty-state">Aucune routine.</p> }
          @if (routineDraft; as draft) {
            <form class="inline-form" (ngSubmit)="saveRoutine()">
              <label class="field"><span>Nom</span>
                <input class="text-input" name="routineName" required maxlength="100" placeholder="Ex. Tendons" [(ngModel)]="draft.name" /></label>
              <div class="field"><span>Jours · {{ daysLabel(selectedDays(draft)) || 'aucun' }}</span>
                <div class="day-grid" role="group" aria-label="Jours de la routine">
                  @for (letter of dayLetters; track $index) {
                    <button type="button" class="pill" [attr.aria-pressed]="draft.days[$index]" [attr.aria-label]="dayNames[$index]"
                      (click)="draft.days[$index] = !draft.days[$index]">{{ letter }}</button>
                  }
                </div>
              </div>
              <div class="field"><span>Jokers par semaine (jours prévus manqués sans casser la série)</span>
                <div class="choice-grid" role="group" aria-label="Jokers par semaine">
                  @for (count of [0, 1, 2]; track count) {
                    <button type="button" class="pill" [attr.aria-pressed]="draft.jokersPerWeek === count" (click)="draft.jokersPerWeek = count">{{ count }}</button>
                  }
                </div>
              </div>
              <div class="field"><span>Exercices et cibles</span>
                @for (line of draft.lines; track line.exerciseId; let index = $index) {
                  @if (exerciseOf(line.exerciseId); as exercise) {
                    <div class="line-editor">
                      <div class="line-editor-head"><strong>{{ exercise.name }}</strong>
                        <button type="button" class="icon-button" [disabled]="index === 0" [attr.aria-label]="'Monter ' + exercise.name" (click)="move(index, -1)"><i class="pi pi-chevron-up" aria-hidden="true"></i></button>
                        <button type="button" class="icon-button" [disabled]="index === draft.lines.length - 1" [attr.aria-label]="'Descendre ' + exercise.name" (click)="move(index, 1)"><i class="pi pi-chevron-down" aria-hidden="true"></i></button>
                        <button type="button" class="icon-button danger-text" [attr.aria-label]="'Retirer ' + exercise.name" (click)="draft.lines.splice(index, 1)"><i class="pi pi-times" aria-hidden="true"></i></button>
                      </div>
                      <div class="line-editor-fields">
                        <label class="field"><span>Séries</span>
                          <input class="text-input" type="number" inputmode="numeric" min="1" max="20" step="1" required [name]="'sets-' + line.exerciseId" [(ngModel)]="line.sets" /></label>
                        <label class="field"><span>{{ exercise.measure === 'hold' ? 'Secondes' : 'Répétitions' }}</span>
                          <input class="text-input" type="number" inputmode="numeric" min="1" max="10000" step="1" required [name]="'target-' + line.exerciseId" [(ngModel)]="line.target" /></label>
                        @if (exercise.withLoad) {
                          <label class="field"><span>Charge (kg)</span>
                            <input class="text-input" type="number" inputmode="decimal" min="0" max="500" step="0.5" [name]="'load-' + line.exerciseId" [(ngModel)]="line.loadKg" /></label>
                        }
                      </div>
                    </div>
                  }
                }
                @if (addable(draft).length) {
                  <div class="custom-row">
                    <select class="text-input" name="routineAdd" aria-label="Exercice à ajouter" [(ngModel)]="addId">
                      @for (exercise of addable(draft); track exercise.id) { <option [value]="exercise.id">{{ exercise.name }}</option> }
                    </select>
                    <button type="button" class="lifeos-button lifeos-button-secondary" (click)="addLine(draft)">Ajouter</button>
                  </div>
                } @else if (!exercises().length) { <p class="muted small">Créez d'abord un exercice, plus bas.</p> }
              </div>
              @if (editingRoutineId) { <p class="muted small">Les changements valent à partir d'aujourd'hui ; les jours passés restent jugés avec la routine d'alors.</p> }
              @if (formError()) { <p class="api-error" role="alert">{{ formError() }}</p> }
              <div class="page-actions">
                <button type="submit" class="lifeos-button" [disabled]="busy()">{{ editingRoutineId ? 'Enregistrer' : 'Ajouter' }}</button>
                <button type="button" class="lifeos-button lifeos-button-secondary" (click)="routineDraft = null">Annuler</button>
              </div>
            </form>
          } @else {
            <button type="button" class="lifeos-button lifeos-button-secondary align-start" (click)="newRoutine()">+ Ajouter une routine</button>
          }
        </section>

        <section class="sport-section" aria-labelledby="settings-exercises">
          <h2 id="settings-exercises">Exercices</h2>
          @if (exercises().length) {
            <div class="settings-list">
              @for (exercise of exercises(); track exercise.id) {
                <div class="settings-row">
                  <span class="exercise-row">
                    <app-exercise-image [exerciseId]="exercise.id" [hasImage]="exercise.hasImage" [version]="imageVersion()" [size]="44" />
                    <span><strong>{{ exercise.name }}</strong><span class="muted small">{{ exerciseSummary(exercise) }}</span></span>
                  </span>
                  <button type="button" class="lifeos-button lifeos-button-text" [disabled]="busy()" (click)="editExercise(exercise)">Modifier</button>
                </div>
              }
            </div>
          } @else { <p class="empty-state">Aucun exercice. Ajoutez par exemple « Chaise contre le mur ».</p> }
          @if (exerciseDraft; as draft) {
            <form class="inline-form" (ngSubmit)="saveExercise()">
              <label class="field"><span>Nom</span>
                <input class="text-input" name="exerciseName" required maxlength="100" placeholder="Ex. Chaise contre le mur" [(ngModel)]="draft.name" /></label>
              @if (editingExercise(); as exercise) {
                <div class="exercise-photo">
                  <app-exercise-image [exerciseId]="exercise.id" [hasImage]="exercise.hasImage" [version]="imageVersion()" [alt]="'Photo : ' + exercise.name" [size]="96" />
                  <div class="exercise-photo-actions">
                    <label class="lifeos-button lifeos-button-secondary">{{ exercise.hasImage ? 'Changer la photo' : 'Ajouter une photo' }}
                      <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" [disabled]="busy()" (change)="uploadImage(exercise, $event)" /></label>
                    @if (exercise.hasImage) {
                      <button type="button" class="lifeos-button lifeos-button-text danger-text" [disabled]="busy()" (click)="removeImage(exercise)">Retirer la photo</button>
                    }
                  </div>
                </div>
              } @else { <p class="muted small">La photo s'ajoute une fois l'exercice enregistré.</p> }
              <label class="field"><span>Consigne (position, points d'attention)</span>
                <textarea class="text-input" name="exerciseInstructions" rows="4" maxlength="2000" [(ngModel)]="draft.instructions"></textarea></label>
              <div class="field"><span>Mesure</span>
                @if (editingExercise()?.measureLocked) {
                  <p class="muted small"><strong>{{ measureLabel(draft.measure) }}</strong> · ne change plus, l'exercice a déjà un historique.</p>
                } @else {
                  <div class="choice-grid" role="group" aria-label="Mesure de l'exercice">
                    <button type="button" class="pill" [attr.aria-pressed]="draft.measure === 'hold'" (click)="draft.measure = 'hold'">Tenue (secondes)</button>
                    <button type="button" class="pill" [attr.aria-pressed]="draft.measure === 'reps'" (click)="draft.measure = 'reps'">Répétitions</button>
                  </div>
                }
              </div>
              <label class="check-row"><input type="checkbox" name="exerciseLoad" [(ngModel)]="draft.withLoad" /> Avec charge (en kg)</label>
              @if (formError()) { <p class="api-error" role="alert">{{ formError() }}</p> }
              <div class="page-actions">
                <button type="submit" class="lifeos-button" [disabled]="busy()">{{ editingExerciseId ? 'Enregistrer' : 'Ajouter' }}</button>
                <button type="button" class="lifeos-button lifeos-button-secondary" (click)="exerciseDraft = null">{{ editingExerciseId ? 'Fermer' : 'Annuler' }}</button>
                @if (editingExercise(); as exercise) {
                  <button type="button" class="lifeos-button lifeos-button-text danger-text" [disabled]="busy()" (click)="archiveExercise(exercise)">Archiver</button>
                }
              </div>
            </form>
          } @else {
            <button type="button" class="lifeos-button lifeos-button-secondary align-start" (click)="newExercise()">+ Ajouter un exercice</button>
          }
        </section>
      </div>
    }
  `,
  styleUrls: ['./sports.css', './routines.css'],
  styles: [`.align-start { align-self: flex-start; }`],
})
export class RoutineSettingsComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  readonly loaded = signal(false)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly formError = signal('')
  readonly notice = signal('')
  readonly routines = signal<RoutineReportDto[]>([])
  readonly exercises = signal<ExerciseDto[]>([])
  readonly imageVersion = signal(0)
  readonly editingExerciseIdSignal = signal('')
  readonly editingExercise = computed(() => this.exercises().find(exercise => exercise.id === this.editingExerciseIdSignal()) ?? null)

  routineDraft: RoutineDraft | null = null
  editingRoutineId = ''
  exerciseDraft: ExerciseRequest | null = null
  addId = ''
  get editingExerciseId(): string { return this.editingExerciseIdSignal() }

  readonly dayLetters = DAY_LETTERS
  readonly dayNames = DAY_NAMES
  readonly daysLabel = daysLabel
  readonly routineSummary = routineSummary
  readonly exerciseSummary = exerciseSummary
  readonly measureLabel = measureLabel

  async ngOnInit(): Promise<void> {
    try {
      const [routines, exercises] = await Promise.all([firstValueFrom(this.api.routines(0)), firstValueFrom(this.api.exercises())])
      this.routines.set(routines); this.exercises.set(exercises)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }

  exerciseOf(exerciseId: string): ExerciseDto | null { return this.exercises().find(exercise => exercise.id === exerciseId) ?? null }
  selectedDays(draft: RoutineDraft): number[] { return draft.days.flatMap((on, index) => on ? [index + 1] : []) }
  addable(draft: RoutineDraft): ExerciseDto[] {
    return this.exercises().filter(exercise => !draft.lines.some(line => line.exerciseId === exercise.id))
  }
  addLine(draft: RoutineDraft): void {
    const options = this.addable(draft)
    const exercise = options.find(item => item.id === this.addId) ?? options[0]
    if (exercise) draft.lines.push(newLine(exercise))
    this.addId = ''
  }
  move(index: number, direction: 1 | -1): void {
    if (this.routineDraft) this.routineDraft.lines = moveLine(this.routineDraft.lines, index, direction)
  }

  newRoutine(): void { this.editingRoutineId = ''; this.routineDraft = emptyRoutineDraft(); this.formError.set('') }
  editRoutine(report: RoutineReportDto): void {
    this.editingRoutineId = report.routine.id; this.routineDraft = draftFromRoutine(report.routine); this.formError.set('')
  }
  async saveRoutine(): Promise<void> {
    if (!this.routineDraft) return
    const request = routineRequest(this.routineDraft, this.exercises())
    if (typeof request === 'string') { this.formError.set(request); return }
    await this.run(async () => {
      const editing = this.editingRoutineId
      if (editing) await firstValueFrom(this.api.updateRoutine(editing, request))
      else await firstValueFrom(this.api.createRoutine(request))
      this.routines.set(await firstValueFrom(this.api.routines(0)))
      this.routineDraft = null; this.editingRoutineId = ''
      return editing ? 'Routine modifiée à partir d’aujourd’hui.' : 'Routine ajoutée : elle démarre aujourd’hui.'
    })
  }
  stopRoutine(report: RoutineReportDto): Promise<void> {
    if (!window.confirm(`Arrêter la routine « ${report.routine.name} » ? Son historique est conservé.`)) return Promise.resolve()
    return this.run(async () => {
      await firstValueFrom(this.api.stopRoutine(report.routine.id))
      this.routines.update(list => list.filter(item => item.routine.id !== report.routine.id))
      if (this.editingRoutineId === report.routine.id) this.routineDraft = null
      return 'Routine arrêtée.'
    })
  }

  newExercise(): void { this.editingExerciseIdSignal.set(''); this.exerciseDraft = emptyExerciseDraft(); this.formError.set('') }
  editExercise(exercise: ExerciseDto): void {
    const { name, instructions, measure, withLoad } = exercise
    this.editingExerciseIdSignal.set(exercise.id); this.exerciseDraft = { name, instructions: instructions ?? '', measure, withLoad }; this.formError.set('')
  }
  async saveExercise(): Promise<void> {
    if (!this.exerciseDraft) return
    const request = exerciseRequest(this.exerciseDraft)
    if (typeof request === 'string') { this.formError.set(request); return }
    await this.run(async () => {
      const editing = this.editingExerciseId
      const saved = editing ? await firstValueFrom(this.api.updateExercise(editing, request)) : await firstValueFrom(this.api.createExercise(request))
      this.exercises.update(list => [...list.filter(item => item.id !== saved.id), saved].sort((a, b) => a.name.localeCompare(b.name, 'fr')))
      if (editing) {
        this.exerciseDraft = null; this.editingExerciseIdSignal.set('')
        // Routine cards show the exercise's name and load: reload them.
        this.routines.set(await firstValueFrom(this.api.routines(0)))
        return 'Exercice modifié.'
      }
      // Stay on the new exercise so a photo can be added right away.
      this.editExercise(saved)
      return 'Exercice ajouté. Vous pouvez lui ajouter une photo.'
    })
  }
  archiveExercise(exercise: ExerciseDto): Promise<void> {
    if (!window.confirm(`Archiver l'exercice « ${exercise.name} » ? Son historique est conservé.`)) return Promise.resolve()
    return this.run(async () => {
      await firstValueFrom(this.api.archiveExercise(exercise.id))
      this.exercises.update(list => list.filter(item => item.id !== exercise.id))
      this.exerciseDraft = null; this.editingExerciseIdSignal.set('')
      return 'Exercice archivé.'
    })
  }
  uploadImage(exercise: ExerciseDto, event: Event): Promise<void> {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return Promise.resolve()
    return this.run(async () => {
      this.replace(await firstValueFrom(this.api.uploadExerciseImage(exercise.id, file)))
      return 'Photo enregistrée.'
    })
  }
  removeImage(exercise: ExerciseDto): Promise<void> {
    return this.run(async () => {
      await firstValueFrom(this.api.removeExerciseImage(exercise.id))
      this.replace({ ...exercise, hasImage: false })
      return 'Photo retirée.'
    })
  }

  private replace(exercise: ExerciseDto): void {
    this.exercises.update(list => list.map(item => item.id === exercise.id ? exercise : item))
    this.imageVersion.update(version => version + 1)
  }

  private async run(action: () => Promise<string>): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set(''); this.formError.set(''); this.notice.set('')
    try { this.notice.set(await action()) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
