import { BreakpointObserver } from '@angular/cdk/layout'
import { Component, inject, input, model, output } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { map } from 'rxjs'
import { HANDSET_QUERY } from './breakpoints'

/**
 * Library page (ADR 0016): title and add button on the computer (the phone's top bar holds the title
 * and a floating + adds), a search field, an optional "show archived" filter, then the projected list.
 * Secondary page actions (e.g. "Relever un prix") are projected with the `pageActions` attribute.
 */
@Component({
  selector: 'app-list-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="lo-page">
      <header class="lo-page-head" [class.is-phone]="handset()">
        <div class="lo-page-title">
          <h1 class="lo-bar-title">{{ heading() }}</h1>
          @if (intro()) { <p class="lo-intro">{{ intro() }}</p> }
        </div>
        <div class="lo-actions">
          <ng-content select="[pageActions]" />
          @if (!handset()) {
            <button mat-flat-button type="button" (click)="add.emit()"><mat-icon svgIcon="add" />{{ addLabel() }}</button>
          }
        </div>
      </header>
      <div class="lo-filters">
        <label class="lo-search">
          <mat-icon svgIcon="search" />
          <input type="search" placeholder="Rechercher" [attr.aria-label]="'Rechercher dans ' + heading().toLowerCase()"
            [value]="query()" (input)="query.set($any($event.target).value)" />
        </label>
        @if (archivedLabel()) {
          <button type="button" class="lo-filter-chip" [class.is-on]="showArchived()" [attr.aria-pressed]="showArchived()"
            (click)="showArchived.set(!showArchived())">
            @if (showArchived()) { <mat-icon svgIcon="check" /> }{{ archivedLabel() }}
          </button>
        }
      </div>
      <ng-content />
      @if (handset()) {
        <button mat-fab type="button" class="lo-fab" (click)="add.emit()" [attr.aria-label]="addLabel()" [attr.title]="addLabel()">
          <mat-icon svgIcon="add" />
        </button>
      }
    </div>
  `,
})
export class ListPageComponent {
  readonly heading = input.required<string>()
  readonly intro = input('')
  readonly addLabel = input.required<string>()
  /** "Afficher les archivés"; no filter when empty. */
  readonly archivedLabel = input('')
  readonly query = model('')
  readonly showArchived = model(false)
  readonly add = output<void>()
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
}

/** Search on a name, ignoring case and accents. */
export function matchesQuery(text: string, query: string): boolean {
  const normalize = (value: string) => value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase('fr')
  const wanted = normalize(query.trim())
  return !wanted || normalize(text).includes(wanted)
}
