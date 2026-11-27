import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { ExerciseDialogComponent, type ExerciseDialogResult } from './exercise-dialog.component'
import { ExerciseImageComponent } from './exercise-image.component'
import { ExerciseLogDialogComponent } from './exercise-log-dialog.component'
import { exerciseSummary } from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { ExerciseCompletionDto, ExerciseDto } from './sports.models'

/**
 * The household's exercise library (ADR 0014, ADR 0015); a tap opens the exercise's window, « Faire » notes it
 * done alone, outside any routine.
 */
@Component({
  selector: 'app-exercises-page',
  standalone: true,
  imports: [ExerciseImageComponent, ListPageComponent, MatButtonModule, MatIconModule],
  template: `
    <app-list-page heading="Exercices" intro="La bibliothèque du foyer, utilisée par les routines." addLabel="Ajouter un exercice"
      archivedLabel="Afficher les archivés" [(query)]="query" [(showArchived)]="showArchived" (add)="open(null)">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (shown().length) {
          <div class="lo-list">
            @for (exercise of shown(); track exercise.id) {
              <div class="lo-row">
                <button type="button" class="lo-row-main" (click)="open(exercise)">
                  <app-exercise-image [exerciseId]="exercise.id" [hasImage]="exercise.hasImage" [alt]="'Photo : ' + exercise.name" />
                  <span class="lo-row-text"><span class="lo-row-title">{{ exercise.name }}</span>
                    <span class="lo-row-sub">{{ summary(exercise) }}</span></span>
                  @if (exercise.isArchived) { <span class="lo-tag">Archivé</span> }
                </button>
                @if (!exercise.isArchived) {
                  <span class="lo-row-action">
                    <button mat-stroked-button type="button" (click)="log(exercise)" [attr.aria-label]="'Faire l’exercice ' + exercise.name + ' hors routine'">
                      <mat-icon svgIcon="fitness_center" />Faire</button>
                  </span>
                }
              </div>
            }
          </div>
        } @else {
          <p class="lo-empty">{{ exercises().length ? 'Aucun résultat.' : 'Aucun exercice. Ajoutez par exemple « Chaise contre le mur ».' }}</p>
        }
      }
    </app-list-page>
  `,
})
export class ExercisesPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly query = signal('')
  readonly showArchived = signal(false)
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly exercises = signal<ExerciseDto[]>([])
  readonly shown = computed(() => this.exercises()
    .filter(exercise => (this.showArchived() || !exercise.isArchived) && matchesQuery(exercise.name, this.query())))

  async ngOnInit(): Promise<void> { await this.load() }

  summary(exercise: ExerciseDto): string { return exerciseSummary(exercise) + (exercise.hasImage ? '' : ' · sans photo') }

  async open(exercise: ExerciseDto | null): Promise<void> {
    const result = await firstValueFrom(openEditor<ExerciseDialogComponent, ExerciseDto | null, ExerciseDialogResult>(
      this.dialog, ExerciseDialogComponent, exercise).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  async log(exercise: ExerciseDto): Promise<void> {
    const done = await firstValueFrom(openEditor<ExerciseLogDialogComponent, ExerciseDto, ExerciseCompletionDto>(
      this.dialog, ExerciseLogDialogComponent, exercise).afterClosed())
    if (done) this.snack.open(`« ${exercise.name} » noté ; il compte dans sa progression`, undefined, { duration: 4000 })
  }

  private async load(): Promise<void> {
    try { this.exercises.set(await firstValueFrom(this.api.exercises(true))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }
}
