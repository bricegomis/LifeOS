import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { ProjectCardComponent } from './project-card.component'
import { ProjectDialogComponent, type ProjectDialogData, type ProjectDialogResult } from './project-dialog.component'
import { projectTree } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { TaskProjectDto } from './tasks.models'

/**
 * The household's projects (ADR 0017): the main projects in progress with their progress (sub-projects
 * included), closed ones on demand. A sub-project is reached from its parent's page, unless its parent
 * is hidden; a search looks into every level and shows each project with its path.
 */
@Component({
  selector: 'app-projects-page',
  standalone: true,
  imports: [ListPageComponent, ProjectCardComponent],
  template: `
    <app-list-page heading="Projets" intro="Des todos regroupés, avec leur avancement. Un projet peut contenir des sous-projets." addLabel="Nouveau projet"
      archivedLabel="Afficher les projets terminés et abandonnés" [(query)]="query" [(showArchived)]="showClosed" (add)="add()">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (shown().length) {
          <div class="cards">
            @for (project of shown(); track project.id) { <app-project-card [project]="project" [today]="today()" [path]="query().trim() ? parentPath(project) : ''" /> }
          </div>
        } @else {
          <p class="lo-empty">{{ projects().length ? 'Aucun résultat.' : 'Aucun projet. Ajoute par exemple « Chambre de Léa ».' }}</p>
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
  /** Path of each project's parent ("Terrain › Chalet"), from every project. */
  private readonly paths = computed(() => new Map(projectTree(this.projects()).map(o => [o.id, o.label])))
  readonly shown = computed(() => {
    const visible = this.projects().filter(p => this.showClosed() || p.state === 'active')
    if (this.query().trim()) return visible.filter(p => matchesQuery(p.title, this.query()))
    const ids = new Set(visible.map(p => p.id))
    return visible.filter(p => !p.parentId || !ids.has(p.parentId))
  })

  async ngOnInit(): Promise<void> {
    try { this.today.set((await firstValueFrom(this.api.today())).today) } catch { /* local day */ }
    await this.load()
  }

  parentPath(project: TaskProjectDto): string {
    return project.parentId ? this.paths().get(project.parentId) ?? '' : ''
  }

  async add(): Promise<void> {
    const result = await firstValueFrom(openEditor<ProjectDialogComponent, ProjectDialogData, ProjectDialogResult>(this.dialog, ProjectDialogComponent, { project: null }).afterClosed())
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
