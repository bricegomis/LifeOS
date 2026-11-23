import { Component, computed, DestroyRef, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { TaskImageComponent } from './task-image.component'
import { initial, projectTree, subTreeIds } from './task-format'
import { TasksApiService } from './tasks-api.service'
import type { TaskProjectDetailDto, TaskProjectDto } from './tasks.models'

export interface ProjectDialogResult { detail: TaskProjectDetailDto, message: string }
/** `project`: the project to edit, null for a new one; `parentId`: the parent of a new sub-project. */
export interface ProjectDialogData { project: TaskProjectDto | null, parentId?: string }

/**
 * Creates or edits a project (ADR 0017): photo, name, parent project, due day and notes; abandoned from
 * here (second click). The parent is a project in progress (or the current one), never the project itself
 * nor one of its sub-projects.
 */
@Component({
  selector: 'app-project-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, TaskImageComponent],
  template: `
    <app-editor-frame [heading]="project ? 'Modifier le projet' : 'Nouveau projet'" formId="project-form" [saveLabel]="project ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="project?.state === 'active' ? 'Abandonner le projet' : ''" dangerIcon="block"
      (danger)="abandon()">
      <form id="project-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-field-group">
          <span class="lo-field-label">Photo</span>
          <div class="photo-row">
            <div class="photo">
              @if (preview(); as source) {
                <img [src]="source" alt="Nouvelle photo du projet" />
              } @else if (project && project.hasImage && !removePhoto()) {
                <app-task-image kind="projects" [todoId]="project.id" [hasImage]="true" [title]="project.title" [size]="104" [alt]="'Photo : ' + project.title" />
              } @else {
                <span class="photo-empty" role="img" [attr.aria-label]="'Pas de photo, initiale ' + letter()">{{ letter() }}</span>
              }
              <label class="photo-edit" [attr.title]="hasPhoto() ? 'Changer la photo' : 'Ajouter une photo'">
                <mat-icon svgIcon="photo_camera" /><span class="sr-only">{{ hasPhoto() ? 'Changer la photo' : 'Ajouter une photo' }}</span>
                <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" (change)="pick($event)" />
              </label>
            </div>
            <div class="photo-side">
              <span class="lo-help">Facultative. Sans photo, la carte affiche la première lettre du nom.</span>
              @if (hasPhoto()) {
                <button mat-button type="button" class="photo-delete" (click)="dropPhoto()"><mat-icon svgIcon="delete" />Retirer la photo</button>
              }
            </div>
          </div>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="title" required maxlength="200" placeholder="Ex. Chambre de Léa" [(ngModel)]="draft.title" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="parent">
          <mat-label>Dans le projet</mat-label>
          <mat-select name="parent" [(ngModel)]="draft.parentId">
            <mat-option value="">Aucun (projet principal)</mat-option>
            @for (option of parents(); track option.id) { <mat-option [value]="option.id">{{ option.label }}</mat-option> }
          </mat-select>
          <mat-hint>Pour en faire un sous-projet, par exemple « Chalet » dans « Terrain ».</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="due">
          <mat-label>Échéance</mat-label>
          <input matInput type="date" name="dueOn" [(ngModel)]="draft.dueOn" />
          <mat-hint>Vide : sans échéance.</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Notes</mat-label>
          <textarea matInput name="notes" rows="3" maxlength="4000" [(ngModel)]="draft.notes"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .photo-row { display: flex; align-items: flex-end; gap: 16px; }
    .photo { position: relative; width: 104px; height: 104px; flex: 0 0 auto; }
    .photo img, .photo-empty { display: flex; width: 104px; height: 104px; border-radius: 16px; object-fit: cover; }
    .photo-empty {
      align-items: center; justify-content: center; font-size: 44px; font-weight: 600;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container);
    }
    .photo-edit {
      position: absolute; right: -8px; bottom: -8px; display: inline-flex; width: 40px; height: 40px; align-items: center; justify-content: center;
      border-radius: 12px; background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); box-shadow: var(--mat-sys-level2); cursor: pointer;
    }
    .photo-edit:focus-within { outline: 3px solid var(--mat-sys-primary); outline-offset: 2px; }
    .photo-side { display: flex; min-width: 0; flex-direction: column; align-items: flex-start; gap: 4px; }
    .photo-delete { color: var(--mat-sys-error); }
    .due { max-width: 320px; }
    .parent { max-width: 480px; }
  `],
})
export class ProjectDialogComponent {
  private readonly api = inject(TasksApiService)
  private readonly ref = inject<MatDialogRef<ProjectDialogComponent, ProjectDialogResult>>(MatDialogRef)
  private readonly data = inject<ProjectDialogData>(MAT_DIALOG_DATA)
  readonly project = this.data.project
  readonly busy = signal(false)
  readonly error = signal('')
  readonly preview = signal<string | null>(null)
  readonly removePhoto = signal(false)
  readonly draft = {
    title: this.project?.title ?? '', notes: this.project?.notes ?? '', dueOn: this.project?.dueOn ?? '',
    parentId: this.project ? this.project.parentId ?? '' : this.data.parentId ?? '',
  }
  /** Every project of the household, to choose the parent from. */
  private readonly all = signal<TaskProjectDto[]>([])
  /** Projects in progress (or the current parent), without this project and its sub-projects, by path. */
  readonly parents = computed(() => {
    const all = this.all()
    const excluded = this.project ? subTreeIds(all, this.project.id) : new Set<string>()
    const current = this.project?.parentId ?? this.data.parentId
    return projectTree(all).filter(o => !excluded.has(o.id) && (o.id === current || all.find(p => p.id === o.id)?.state === 'active'))
  })
  private file: File | null = null
  private readonly initial = formSnapshot(this.draft)

  constructor() {
    inject(DestroyRef).onDestroy(() => this.releasePreview())
    firstValueFrom(this.api.projects('all')).then(all => this.all.set(all), () => { /* top project only */ })
  }

  letter(): string { return initial(this.draft.title) }
  hasPhoto(): boolean { return !!this.preview() || (!!this.project?.hasImage && !this.removePhoto()) }
  dirty(): boolean { return hasChanges(this.initial, this.draft) || !!this.file || this.removePhoto() }

  pick(event: Event): void {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return
    this.releasePreview()
    this.file = file
    this.removePhoto.set(false)
    this.preview.set(URL.createObjectURL(file))
  }

  dropPhoto(): void {
    this.releasePreview()
    this.file = null
    this.removePhoto.set(!!this.project?.hasImage)
  }

  async save(): Promise<void> {
    if (this.busy()) return
    const title = this.draft.title.trim()
    if (!title) { this.error.set('Le nom est obligatoire.'); return }
    const request = { title, notes: this.draft.notes.trim() || null, dueOn: this.draft.dueOn || null, parentId: this.draft.parentId || null }
    this.busy.set(true); this.error.set('')
    try {
      let detail = await firstValueFrom(this.project ? this.api.updateProject(this.project.id, request) : this.api.createProject(request))
      if (this.file) detail = await firstValueFrom(this.api.uploadProjectImage(detail.project.id, this.file))
      else if (this.removePhoto() && detail.project.hasImage) {
        await firstValueFrom(this.api.removeProjectImage(detail.project.id))
        detail = { ...detail, project: { ...detail.project, hasImage: false } }
      }
      this.ref.close({ detail, message: this.project ? 'Projet enregistré' : 'Projet ajouté' })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async abandon(): Promise<void> {
    if (!this.project || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      const detail = await firstValueFrom(this.api.closeProject(this.project.id, 'abandon'))
      this.ref.close({ detail, message: `« ${this.project.title} » abandonné ; ses todos ne changent pas` })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  private releasePreview(): void {
    const url = this.preview()
    if (url) URL.revokeObjectURL(url)
    this.preview.set(null)
  }
}
