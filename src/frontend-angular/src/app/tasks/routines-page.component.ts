import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { RoutineCardComponent } from './routine-card.component'
import { RoutineDialogComponent, type RoutineDialogData, type RoutineDialogResult } from './routine-dialog.component'
import { TasksApiService } from './tasks-api.service'
import type { TaskRoutineDto } from './tasks.models'
import { TodoActionsService } from './todo-actions.service'

/** The household routines (ADR 0017): active ones by due day, paused ones, and the stopped ones on demand. */
@Component({
  selector: 'app-routines-page',
  standalone: true,
  imports: [ListPageComponent, RoutineCardComponent],
  template: `
    <app-list-page heading="Routines" intro="Ce qui revient : à date fixe ou un délai après la dernière fois." addLabel="Nouvelle routine"
      archivedLabel="Afficher les routines arrêtées" [(query)]="query" [(showArchived)]="showStopped" (add)="add()">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @for (group of groups(); track group.title) {
          @if (group.routines.length) {
            <section class="lo-section">
              <div class="lo-section-head"><h2>{{ group.title }}</h2><span class="lo-muted">{{ group.routines.length }}</span></div>
              <div class="cards">
                @for (routine of group.routines; track routine.id) {
                  <app-routine-card [routine]="routine" [today]="today()" [showDue]="true" [busy]="pending() === routine.id"
                    (done)="close($event, 'done')" (skip)="close($event, 'skip')" />
                }
              </div>
            </section>
          }
        }
        @if (!shownCount()) {
          <p class="lo-empty">{{ routines().length ? 'Aucun résultat.' : 'Aucune routine. Ajoutez par exemple « Sortir les poubelles ».' }}</p>
        }
      }
    </app-list-page>
  `,
  styles: [`
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class RoutinesPageComponent implements OnInit {
  private readonly api = inject(TasksApiService)
  private readonly actions = inject(TodoActionsService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly query = signal('')
  readonly showStopped = signal(false)
  readonly routines = signal<TaskRoutineDto[]>([])
  readonly today = signal(new Date().toISOString().slice(0, 10))
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly pending = signal('')
  private readonly shown = computed(() => this.routines().filter(r => matchesQuery(r.title, this.query())))
  readonly groups = computed(() => [
    { title: 'Actives', routines: this.shown().filter(r => r.state === 'active') },
    { title: 'En pause', routines: this.shown().filter(r => r.state === 'paused') },
    { title: 'Arrêtées', routines: this.showStopped() ? this.shown().filter(r => r.state === 'stopped') : [] },
  ])
  readonly shownCount = computed(() => this.groups().reduce((sum, g) => sum + g.routines.length, 0))

  async ngOnInit(): Promise<void> {
    try { this.today.set((await firstValueFrom(this.api.today())).today) } catch { /* local day */ }
    await this.load()
  }

  async close(routine: TaskRoutineDto, how: 'done' | 'skip'): Promise<void> {
    this.pending.set(routine.id)
    await this.actions.closeRoutine(routine, how, () => void this.load())
    this.pending.set('')
    await this.load()
  }

  async add(): Promise<void> {
    let tags: string[] = []
    try { tags = await firstValueFrom(this.api.tags()) } catch { /* no suggestion */ }
    const data: RoutineDialogData = { routine: null, today: this.today(), tags }
    const result = await firstValueFrom(openEditor<RoutineDialogComponent, RoutineDialogData, RoutineDialogResult>(this.dialog, RoutineDialogComponent, data).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.routines.set(await firstValueFrom(this.api.routines('all'))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }
}
