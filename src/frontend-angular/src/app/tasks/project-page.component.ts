import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ProjectCardComponent } from './project-card.component'
import { ProjectDialogComponent, type ProjectDialogData, type ProjectDialogResult } from './project-dialog.component'
import { TaskImageComponent } from './task-image.component'
import { dayLabel, projectProgress } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { TaskProjectDetailDto, TaskProjectDto, TodoDto } from './tasks.models'
import { TodoActionsService } from './todo-actions.service'
import { TodoCardComponent } from './todo-card.component'

/**
 * One project (ADR 0017): the path of its parents, progress (sub-projects included), its sub-projects,
 * its open todos by due day, a quick add, the done and abandoned ones folded; finishing asks a second
 * click and says how many todos and sub-projects stay open.
 */
@Component({
  selector: 'app-project-page',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, ProjectCardComponent, RouterLink, TaskImageComponent, TodoCardComponent],
  template: `
    <div class="lo-page project-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (detail(); as d) {
        @let p = d.project;
        <header class="head">
          @if (parent(); as up) {
            <a mat-icon-button [routerLink]="['/tasks/project', up.id]" [attr.aria-label]="'Retour au projet « ' + up.title + ' »'"><mat-icon svgIcon="arrow_back" /></a>
          } @else {
            <a mat-icon-button routerLink="/tasks/projects" aria-label="Retour aux projets"><mat-icon svgIcon="arrow_back" /></a>
          }
          <h1 class="lo-bar-title head-title">Projet</h1>
          <button mat-flat-button type="button" class="lo-desktop-only" (click)="edit(p)"><mat-icon svgIcon="edit" />Modifier</button>
          <button mat-icon-button type="button" class="lo-phone-only" (click)="edit(p)" aria-label="Modifier le projet" title="Modifier le projet">
            <mat-icon svgIcon="edit" /></button>
        </header>

        <div class="hero">
          <app-task-image kind="projects" [todoId]="p.id" [hasImage]="p.hasImage" [title]="p.title" [version]="p.updatedAt" [size]="96"
            [alt]="p.hasImage ? 'Photo : ' + p.title : 'Pas de photo'" />
          <div class="hero-text">
            @if (d.ancestors.length) {
              <nav class="crumbs" aria-label="Projets parents">
                @for (a of d.ancestors; track a.id) { <a [routerLink]="['/tasks/project', a.id]">{{ a.title }}</a><span aria-hidden="true">›</span> }
              </nav>
            }
            <h2 class="hero-title">{{ p.title }}</h2>
            <span class="lo-muted">{{ p.dueOn ? 'Échéance : ' + day(p.dueOn).toLocaleLowerCase('fr') : 'Sans échéance' }}</span>
            @if (p.state !== 'active') { <span class="lo-tags"><span class="lo-tag" [class.is-ok]="p.state === 'done'">{{ p.state === 'done' ? 'Terminé' : 'Abandonné' }}</span></span> }
          </div>
        </div>

        <div class="progress">
          <div class="bar" role="progressbar" [attr.aria-valuenow]="progress().percent" aria-valuemin="0" aria-valuemax="100"
            [attr.aria-label]="'Avancement : ' + progress().label"><span [style.width.%]="progress().percent"></span></div>
          <span class="lo-muted">{{ progress().label }} ({{ d.subProjects.length ? 'sous-projets compris, ' : '' }}les todos abandonnés ne comptent pas)</span>
        </div>

        <div class="actions">
          @if (p.state === 'active') {
            <button mat-stroked-button type="button" [class.is-confirming]="confirming()" [disabled]="busy()" (click)="finish()">
              <mat-icon svgIcon="check_circle" />{{ confirming() ? 'Confirmer : terminer le projet' : 'Terminer le projet' }}</button>
          } @else {
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="reopen()"><mat-icon svgIcon="undo" />Rouvrir le projet</button>
          }
        </div>
        @if (confirming()) {
          <p class="lo-notice" role="status">{{ finishNotice() }}</p>
        }
        @if (p.notes) { <p class="notes">{{ p.notes }}</p> }

        @if (d.subProjects.length || p.state === 'active') {
          <section class="lo-section" aria-labelledby="sub-projects">
            <div class="lo-section-head">
              <h2 id="sub-projects">Sous-projets</h2><span class="lo-muted">{{ d.subProjects.length }}</span>
              @if (p.state === 'active') {
                <button mat-stroked-button type="button" class="sub-add" (click)="addSubProject(p)"><mat-icon svgIcon="add" />Nouveau sous-projet</button>
              }
            </div>
            @if (d.subProjects.length) {
              <div class="cards">
                @for (sub of d.subProjects; track sub.id) { <app-project-card [project]="sub" [today]="today()" /> }
              </div>
            } @else {
              <p class="lo-muted">Aucun sous-projet. Découpez par exemple « Chalet » en « Plafond » et « Électricité ».</p>
            }
          </section>
        }

        <section class="lo-section" aria-labelledby="open-todos">
          <div class="lo-section-head"><h2 id="open-todos">À faire</h2><span class="lo-muted">{{ open().length }}</span></div>
          @if (p.state === 'active') {
            <form class="add" (ngSubmit)="addTodo(p)">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Nouveau todo du projet</mat-label>
                <input matInput name="title" maxlength="200" placeholder="Ex. poncer les murs" [(ngModel)]="newTitle" />
              </mat-form-field>
              <button mat-stroked-button type="submit" [disabled]="busy() || !newTitle.trim()">Ajouter</button>
            </form>
          }
          @if (open().length) {
            <div class="cards">
              @for (todo of open(); track todo.id) {
                <app-todo-card [todo]="todo" [today]="today()" [showDue]="true" [showProject]="false" [busy]="pending() === todo.id" (tick)="toggle($event)" />
              }
            </div>
          } @else {
            <p class="lo-muted">Aucun todo ouvert.</p>
          }
        </section>

        @if (closed().length) {
          <section class="lo-section">
            <button type="button" class="fold" [attr.aria-expanded]="showClosed()" (click)="showClosed.set(!showClosed())">
              <mat-icon svgIcon="chevron_right" [class.is-open]="showClosed()" /><span>Faits et abandonnés</span>
              <span class="lo-muted">{{ closed().length }}</span>
            </button>
            @if (showClosed()) {
              <div class="cards">
                @for (todo of closed(); track todo.id) {
                  <app-todo-card [todo]="todo" [today]="today()" [showProject]="false" [busy]="pending() === todo.id" (tick)="toggle($event)" />
                }
              </div>
            }
          </section>
        }
      } @else if (!error()) {
        <p class="lo-muted" role="status">Chargement…</p>
      }
    </div>
  `,
  styles: [`
    .project-page { max-width: 880px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .hero { display: flex; align-items: center; gap: 16px; }
    .hero-text { display: flex; min-width: 0; flex-direction: column; gap: 6px; }
    .crumbs { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-large); }
    .crumbs a { color: var(--mat-sys-primary); text-decoration: none; }
    .crumbs a:hover { text-decoration: underline; }
    .sub-add { margin-left: auto; }
    .hero-title { margin: 0; font: var(--mat-sys-title-large); font-weight: 600; }
    .progress { display: flex; flex-direction: column; gap: 6px; }
    .bar { height: 8px; overflow: hidden; border-radius: 4px; background: var(--mat-sys-secondary-container); }
    .bar span { display: block; height: 100%; background: var(--mat-sys-primary); }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .is-confirming { background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); }
    .notes { margin: 0; white-space: pre-line; }
    .add { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .add mat-form-field { flex: 1 1 240px; }
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    .fold { display: flex; align-items: center; gap: 8px; min-height: 40px; padding: 0; border: 0; background: transparent;
      color: var(--mat-sys-on-surface); font: var(--mat-sys-title-medium); font-weight: 600; cursor: pointer; }
    .fold mat-icon { transition: transform 150ms; }
    .fold mat-icon.is-open { transform: rotate(90deg); }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class ProjectPageComponent implements OnInit {
  private readonly api = inject(TasksApiService)
  private readonly actions = inject(TodoActionsService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly route = inject(ActivatedRoute)
  private readonly destroyRef = inject(DestroyRef)
  private id = ''
  readonly detail = signal<TaskProjectDetailDto | null>(null)
  readonly today = signal(new Date().toISOString().slice(0, 10))
  readonly error = signal('')
  readonly busy = signal(false)
  readonly pending = signal('')
  readonly confirming = signal(false)
  readonly showClosed = signal(false)
  readonly open = computed(() => this.detail()?.todos.filter(t => t.state === 'open') ?? [])
  readonly closed = computed(() => this.detail()?.todos.filter(t => t.state !== 'open') ?? [])
  readonly parent = computed(() => this.detail()?.ancestors.at(-1) ?? null)
  readonly finishNotice = computed(() => {
    const d = this.detail()
    if (!d) return ''
    const open = d.todos.filter(t => t.state === 'open').length
    const subs = d.subProjects.filter(s => s.state === 'active').length
    const bits: string[] = []
    if (open) bits.push(`${open} todo${open > 1 ? 's' : ''} ouvert${open > 1 ? 's' : ''}`)
    if (subs) bits.push(`${subs} sous-projet${subs > 1 ? 's' : ''} en cours`)
    return bits.length ? `Il reste ${bits.join(' et ')}. Rien ne change pour eux : ils restent ouverts et gardent ce projet.` : 'Tous les todos sont faits.'
  })
  readonly progress = computed(() => projectProgress(this.detail()?.project.done ?? 0, this.detail()?.project.total ?? 0))
  newTitle = ''

  async ngOnInit(): Promise<void> {
    try { this.today.set((await firstValueFrom(this.api.today())).today) } catch { /* local day */ }
    // The same page shows a parent and its sub-projects: reload when the id changes.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.id = params.get('id') ?? ''
      this.confirming.set(false)
      this.showClosed.set(false)
      void this.load()
    })
  }

  day(iso: string): string { return dayLabel(iso, this.today()) }

  async toggle(todo: TodoDto): Promise<void> {
    this.pending.set(todo.id)
    await this.actions.toggle(todo, () => void this.load())
    this.pending.set('')
    await this.load()
  }

  async addTodo(project: TaskProjectDto): Promise<void> {
    const title = this.newTitle.trim()
    if (!title || this.busy()) return
    this.busy.set(true)
    try {
      await firstValueFrom(this.api.createTodo({ title, notes: null, dueOn: null, durationMinutes: null, priority: 'normal', tags: [], projectId: project.id }))
      this.newTitle = ''
      await this.load()
      this.snack.open('Todo ajouté au projet', undefined, { duration: 4000 })
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
    } finally {
      this.busy.set(false)
    }
  }

  async finish(): Promise<void> {
    if (!this.confirming()) { this.confirming.set(true); return }
    this.confirming.set(false)
    await this.run(() => this.api.closeProject(this.id, 'finish'), 'Projet terminé')
  }

  reopen(): Promise<void> { return this.run(() => this.api.reopenProject(this.id), 'Projet rouvert') }

  async addSubProject(project: TaskProjectDto): Promise<void> {
    const result = await firstValueFrom(openEditor<ProjectDialogComponent, ProjectDialogData, ProjectDialogResult>(this.dialog, ProjectDialogComponent,
      { project: null, parentId: project.id }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  async edit(project: TaskProjectDto): Promise<void> {
    const result = await firstValueFrom(openEditor<ProjectDialogComponent, ProjectDialogData, ProjectDialogResult>(this.dialog, ProjectDialogComponent, { project }).afterClosed())
    if (!result) return
    this.detail.set(result.detail)
    this.snack.open(result.message, undefined, { duration: 4000 })
  }

  private async run(request: () => ReturnType<TasksApiService['project']>, message: string): Promise<void> {
    this.busy.set(true)
    try {
      this.detail.set(await firstValueFrom(request()))
      this.snack.open(message, undefined, { duration: 4000 })
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
    } finally {
      this.busy.set(false)
    }
  }

  private async load(): Promise<void> {
    try { this.detail.set(await firstValueFrom(this.api.project(this.id))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
