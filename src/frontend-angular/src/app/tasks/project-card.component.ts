import { Component, computed, input } from '@angular/core'
import { RouterLink } from '@angular/router'
import { TaskImageComponent } from './task-image.component'
import { dayLabel, projectProgress } from './task-format'
import type { TaskProjectDto } from './tasks.models'

/**
 * A project as a card (ADR 0017): image or initial, name, its parent's path when asked, due day, open
 * todos and sub-projects, progress bar (sub-projects included).
 */
@Component({
  selector: 'app-project-card',
  standalone: true,
  imports: [RouterLink, TaskImageComponent],
  template: `
    @let p = project();
    <a class="lo-card card" [routerLink]="['/tasks/project', p.id]" [attr.aria-label]="'Ouvrir le projet « ' + p.title + ' »'">
      <div class="card-main">
        <app-task-image kind="projects" [todoId]="p.id" [hasImage]="p.hasImage" [title]="p.title" [version]="p.updatedAt" />
        <span class="card-text">
          @if (path()) { <span class="card-path">{{ path() }} ›</span> }
          <span class="card-title">{{ p.title }}</span>
          <span class="card-meta">{{ meta() }}</span>
          @if (p.state !== 'active') { <span class="lo-tags"><span class="lo-tag" [class.is-ok]="p.state === 'done'">{{ p.state === 'done' ? 'Terminé' : 'Abandonné' }}</span></span> }
        </span>
      </div>
      @if (p.total) {
        <div class="progress">
          <div class="bar" role="progressbar" [attr.aria-valuenow]="progress().percent" aria-valuemin="0" aria-valuemax="100"
            [attr.aria-label]="'Avancement : ' + progress().label"><span [style.width.%]="progress().percent"></span></div>
          <span class="lo-muted">{{ progress().label }}</span>
        </div>
      }
    </a>
  `,
  styles: [`
    .card { display: flex; flex-direction: column; color: inherit; text-decoration: none; }
    .card:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .card:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: -3px; }
    .card-main { display: flex; align-items: center; gap: 12px; padding: 8px; }
    .card-text { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
    .card-title { font: var(--mat-sys-title-small); font-size: 15px; }
    .card-path { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-medium); }
    .card-meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .progress { display: flex; align-items: center; gap: 8px; padding: 0 12px 10px; }
    .bar { flex: 1 1 auto; height: 6px; overflow: hidden; border-radius: 3px; background: var(--mat-sys-secondary-container); }
    .bar span { display: block; height: 100%; background: var(--mat-sys-primary); }
  `],
})
export class ProjectCardComponent {
  readonly project = input.required<TaskProjectDto>()
  readonly today = input.required<string>()
  /** Path of its parent, shown above the name ("Terrain › Chalet"); empty to hide it. */
  readonly path = input('')
  readonly progress = computed(() => projectProgress(this.project().done, this.project().total))
  readonly meta = computed(() => {
    const p = this.project()
    const bits: string[] = []
    if (p.dueOn && p.state === 'active') bits.push(`Échéance ${dayLabel(p.dueOn, this.today()).toLocaleLowerCase('fr')}`)
    bits.push(`${p.open} todo${p.open > 1 ? 's' : ''} ouvert${p.open > 1 ? 's' : ''}`)
    if (p.subProjects) bits.push(`${p.subProjects} sous-projet${p.subProjects > 1 ? 's' : ''}`)
    return bits.join(' · ')
  })
}
