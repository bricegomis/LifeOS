import { Component, computed, input, output } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { RouterLink } from '@angular/router'
import { TaskImageComponent } from './task-image.component'
import { todoMeta } from './task-format'
import type { TodoDto } from './tasks.models'

/** A todo as a card (ADR 0017): image or initial, title, details, priority and tags; the round button ticks it. */
@Component({
  selector: 'app-todo-card',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink, TaskImageComponent],
  template: `
    @let t = todo();
    <article class="lo-card card" [class.is-closed]="t.state !== 'open'">
      <a class="card-main" [routerLink]="['/tasks/todo', t.id]" [attr.aria-label]="'Ouvrir « ' + t.title + ' »'">
        <app-task-image [todoId]="t.id" [hasImage]="t.hasImage" [title]="t.title" [version]="t.updatedAt" />
        <span class="card-text">
          <span class="card-title">{{ t.title }}</span>
          @if (meta()) { <span class="card-meta" [class.is-late]="t.isLate && showDue()">{{ meta() }}</span> }
          @if (t.priority !== 'normal' || t.tags.length) {
            <span class="lo-tags">
              @if (t.priority === 'high') { <span class="lo-tag is-off">Priorité haute</span> }
              @if (t.priority === 'low') { <span class="lo-tag">Priorité basse</span> }
              @for (tag of t.tags; track tag) { <span class="lo-tag tag">{{ tag }}</span> }
            </span>
          }
        </span>
      </a>
      @if (t.state !== 'abandoned') {
        <button mat-icon-button type="button" class="check" [class.is-done]="t.state === 'done'" [disabled]="busy()"
          [attr.aria-label]="t.state === 'done' ? 'Rouvrir « ' + t.title + ' »' : 'Marquer « ' + t.title + ' » comme fait'"
          [attr.title]="t.state === 'done' ? 'Rouvrir' : 'Marquer comme fait'" (click)="tick.emit(t)">
          <mat-icon [svgIcon]="t.state === 'done' ? 'check_circle' : 'radio_button_unchecked'" />
        </button>
      }
    </article>
  `,
  styles: [`
    .card { display: flex; align-items: center; gap: 4px; padding-right: 4px; }
    .card-main { display: flex; min-width: 0; flex: 1 1 auto; align-items: center; gap: 12px; padding: 8px; color: inherit; text-decoration: none; }
    .card-main:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .card-main:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: -3px; }
    .card-text { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
    .card-title { font: var(--mat-sys-title-small); font-size: 15px; }
    .card-meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .card-meta.is-late { color: var(--mat-sys-error); }
    .lo-tags { margin-top: 2px; }
    .tag { background: transparent; border: 1px solid var(--mat-sys-outline-variant); }
    .is-closed .card-title { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .check { flex: 0 0 auto; color: var(--mat-sys-on-surface-variant); }
    .check.is-done { color: var(--mat-sys-primary); }
  `],
})
export class TodoCardComponent {
  readonly todo = input.required<TodoDto>()
  readonly today = input.required<string>()
  readonly showDue = input(false)
  readonly busy = input(false)
  readonly tick = output<TodoDto>()
  readonly meta = computed(() => todoMeta(this.todo(), this.today(), this.showDue()))
}
