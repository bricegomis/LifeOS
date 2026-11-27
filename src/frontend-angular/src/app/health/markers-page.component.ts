import { Component, computed, effect, inject, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent } from '@/app/shared/ui/list-page.component'
import { HealthApiService } from './health-api.service'
import type { MarkerListDto } from './health.models'
import { MarkerCardComponent } from './marker-card.component'
import { MarkerDialogComponent, type MarkerDialogData } from './marker-dialog.component'
import { filterMarkers, FOLLOW_UP_FILTERS, type MarkerFilter, type MarkerFollowUpFilter, type MarkerPositionFilter, POSITION_FILTERS } from './marker-rules'

/**
 * Every marker on one page (ADR 0018): search, then filters by position of the last value (target, lab range),
 * by follow-up (to do, late, never measured, up to date) and by panel. Each chip shows how many markers it keeps.
 */
@Component({
  selector: 'app-markers-page',
  standalone: true,
  imports: [ListPageComponent, MarkerCardComponent, MatIconModule],
  template: `
    <app-list-page heading="Marqueurs" intro="Tous tes marqueurs, filtrés par position et par suivi." addLabel="Nouveau marqueur"
      archivedLabel="Afficher les archivés" [(query)]="query" [(showArchived)]="showArchived" (add)="add()">
      <div class="lo-chips" role="group" aria-label="Position de la dernière valeur">
        @for (p of positions; track p.value) {
          <button type="button" class="lo-filter-chip" [class.is-on]="position() === p.value" [attr.aria-pressed]="position() === p.value" (click)="position.set(p.value)">
            @if (position() === p.value) { <mat-icon svgIcon="check" /> }{{ p.label }} · {{ count({ position: p.value }) }}
          </button>
        }
      </div>
      <div class="lo-chips" role="group" aria-label="Suivi">
        @for (s of followUps; track s.value) {
          <button type="button" class="lo-filter-chip" [class.is-on]="followUp() === s.value" [attr.aria-pressed]="followUp() === s.value" (click)="followUp.set(s.value)">
            @if (followUp() === s.value) { <mat-icon svgIcon="check" /> }{{ s.label }} · {{ count({ followUp: s.value }) }}
          </button>
        }
      </div>
      @if (list()?.panels?.length) {
        <div class="lo-chips" role="group" aria-label="Bilan">
          @for (p of ['', ...list()!.panels]; track p) {
            <button type="button" class="lo-filter-chip" [class.is-on]="panel() === p" [attr.aria-pressed]="panel() === p" (click)="panel.set(p)">
              @if (panel() === p) { <mat-icon svgIcon="check" /> }{{ p || 'Tous les bilans' }} · {{ count({ panel: p }) }}
            </button>
          }
        </div>
      }
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (list(); as l) {
        <p class="lo-muted" role="status">{{ markers().length }} marqueur{{ markers().length > 1 ? 's' : '' }}</p>
        @if (markers().length) {
          <div class="cards">@for (m of markers(); track m.id) { <app-marker-card [marker]="m" /> }</div>
        } @else if (!l.markers.length) {
          <p class="lo-empty">Aucun marqueur. Ajoute par exemple « Ferritine », en µg/L.</p>
        } @else {
          <p class="lo-empty">{{ query().trim() ? 'Aucun marqueur ne correspond à « ' + query().trim() + ' ».' : 'Aucun marqueur avec ces filtres.' }}</p>
        }
      }
    </app-list-page>
  `,
  styles: [`
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class MarkersPageComponent {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly positions = POSITION_FILTERS
  readonly followUps = FOLLOW_UP_FILTERS
  readonly query = signal('')
  readonly showArchived = signal(false)
  readonly position = signal<MarkerPositionFilter>('all')
  readonly followUp = signal<MarkerFollowUpFilter>('all')
  readonly panel = signal('')
  readonly list = signal<MarkerListDto | null>(null)
  readonly error = signal('')
  private readonly filter = computed<MarkerFilter>(() => ({ query: this.query(), position: this.position(), followUp: this.followUp(), panel: this.panel() }))
  readonly markers = computed(() => filterMarkers(this.list()?.markers ?? [], this.filter()))

  constructor() {
    effect(() => { this.showArchived(); void this.load() })
  }

  /** Markers a chip would keep, the other filters staying as they are. */
  count(change: Partial<MarkerFilter>): number { return filterMarkers(this.list()?.markers ?? [], { ...this.filter(), ...change }).length }

  async add(): Promise<void> {
    const data: MarkerDialogData = { marker: null, panels: this.list()?.panels ?? [] }
    const message = await firstValueFrom(openEditor<MarkerDialogComponent, MarkerDialogData, string>(this.dialog, MarkerDialogComponent, data).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try {
      this.list.set(await firstValueFrom(this.api.markers(this.showArchived())))
      this.error.set('')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
