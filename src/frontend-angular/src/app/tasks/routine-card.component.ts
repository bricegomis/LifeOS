import { Component, computed, input, output } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { RouterLink } from '@angular/router'
import { routineMeta } from './routine-rules'
import { TaskImageComponent } from './task-image.component'
import type { TaskRoutineDto } from './tasks.models'

/**
 * A household routine as a card (ADR 0017): image or initial, title, due day and recurrence, streak,
 * priority and tags; "Sauter" and the round button close the open due day.
 */
@Component({
  selector: 'app-routine-card',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink, TaskImageComponent],
  template: `
    @let r = routine();
    <article class="lo-card card" [class.is-closed]="struck()">
      <a class="card-main" [routerLink]="['/tasks/routine', r.id]" [attr.aria-label]="'Ouvrir la routine « ' + r.title + ' »'">
        <app-task-image kind="routines" [todoId]="r.id" [hasImage]="r.hasImage" [title]="r.title" [version]="r.updatedAt" />
        <span class="card-text">
          <span class="card-title">{{ r.title }}</span>
          <span class="card-meta" [class.is-late]="r.isLate && !struck()">{{ struck() ? 'Fait aujourd’hui' : meta() }}</span>
          <span class="lo-tags">
            @if (r.streak > 0 && r.state !== 'stopped') { <span class="lo-tag is-ok">Série {{ r.streak }}</span> }
            @if (r.priority === 'high') { <span class="lo-tag is-off">Priorité haute</span> }
            @if (r.priority === 'low') { <span class="lo-tag">Priorité basse</span> }
            @for (tag of r.tags; track tag) { <span class="lo-tag tag">{{ tag }}</span> }
          </span>
        </span>
      </a>
      @if (canAct()) {
        <button mat-icon-button type="button" class="action" [disabled]="busy()" [attr.aria-label]="'Sauter « ' + r.title + ' » cette fois'"
          title="Sauter cette fois" (click)="skip.emit(r)"><mat-icon svgIcon="redo" /></button>
        <button mat-icon-button type="button" class="action" [disabled]="busy()" [attr.aria-label]="'Marquer « ' + r.title + ' » comme fait aujourd’hui'"
          title="Marquer comme fait" (click)="done.emit(r)"><mat-icon svgIcon="radio_button_unchecked" /></button>
      } @else if (struck()) {
        <mat-icon class="done-mark" svgIcon="check_circle" aria-hidden="true" />
      }
    </article>
  `,
  styles: [`
    .card { display: flex; align-items: center; gap: 0; padding-right: 4px; }
    .card-main { display: flex; min-width: 0; flex: 1 1 auto; align-items: center; gap: 12px; padding: 8px; color: inherit; text-decoration: none; }
    .card-main:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .card-main:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: -3px; }
    .card-text { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
    .card-title { font: var(--mat-sys-title-small); font-size: 15px; }
    .card-meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }
    .card-meta.is-late { color: var(--mat-sys-error); }
    .lo-tags { margin-top: 2px; }
    .lo-tags:empty { display: none; }
    .tag { background: transparent; border: 1px solid var(--mat-sys-outline-variant); }
    .is-closed .card-title { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .action { flex: 0 0 auto; color: var(--mat-sys-on-surface-variant); }
    .done-mark { flex: 0 0 auto; margin: 0 12px; color: var(--mat-sys-primary); }
  `],
})
export class RoutineCardComponent {
  readonly routine = input.required<TaskRoutineDto>()
  readonly today = input.required<string>()
  readonly showDue = input(false)
  /** The "done today" copy of a routine on the today page: struck through, no action. */
  readonly doneCopy = input(false)
  readonly busy = input(false)
  readonly done = output<TaskRoutineDto>()
  readonly skip = output<TaskRoutineDto>()
  readonly struck = computed(() => this.doneCopy())
  readonly canAct = computed(() => !this.doneCopy() && this.routine().state === 'active' && !!this.routine().nextDueOn)
  readonly meta = computed(() => routineMeta(this.routine(), this.today(), this.showDue()))
}
