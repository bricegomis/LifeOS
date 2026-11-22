import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { ProjectCardComponent } from './project-card.component'
import { ProjectDialogComponent, type ProjectDialogResult } from './project-dialog.component'
import { TasksApiService } from './tasks-api.service'
import type { TaskProjectDto } from './tasks.models'

/** The household's projects (ADR 0017): in progress with their progress, closed ones on demand. */
@Component({
  selector: 'app-projects-page',
  standalone: true,
  imports: [ListPageComponent, ProjectCardComponent],
  template: `
    <app-list-page heading="Projets" intro="Des todos regroupés, avec leur avancement." addLabel="Nouveau projet"
      archivedLabel="Afficher les projets terminés et abandonnés" [(query)]="query" [(showArchived)]="showClosed" (add)="add()">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (shown().length) {
          <div class="cards">
            @for (project of shown(); track project.id) { <app-project-card [project]="project" [today]="today()" /> }
          </div>
        } @else {
          <p class="lo-empty">{{ projects().length ? 'Aucun résultat.' : 'Aucun projet. Ajoutez par exemple « Chambre de Léa ».' }}</p>
        }
      }
    </app-list-page>
  `,
  styles: [`
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class ProjectsPageComponent implements OnInit {
  private readonly api = inject(TasksApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly query = signal('')
  readonly showClosed = signal(false)
  readonly projects = signal<TaskProjectDto[]>([])
  readonly today = signal(new Date().toISOString().slice(0, 10))
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly shown = computed(() => this.projects()
    .filter(p => (this.showClosed() || p.state === 'active') && matchesQuery(p.title, this.query())))

  async ngOnInit(): Promise<void> {
    try { this.today.set((await firstValueFrom(this.api.today())).today) } catch { /* local day */ }
    await this.load()
  }

  async add(): Promise<void> {
    const result = await firstValueFrom(openEditor<ProjectDialogComponent, TaskProjectDto | null, ProjectDialogResult>(this.dialog, ProjectDialogComponent, null).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.projects.set(await firstValueFrom(this.api.projects('all'))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }
}
