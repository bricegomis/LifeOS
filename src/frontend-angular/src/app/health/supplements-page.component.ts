import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { HealthApiService } from './health-api.service'
import type { SupplementDto, SupplementListDto } from './health.models'
import { SupplementCardComponent } from './supplement-card.component'
import { SupplementDialogComponent, type SupplementDialogData, type SupplementDialogResult } from './supplement-dialog.component'
import { euro } from './supplement-rules'

/** The supplements (ADR 0018) with their monthly cost; the stopped ones on demand. */
@Component({
  selector: 'app-supplements-page',
  standalone: true,
  imports: [ListPageComponent, SupplementCardComponent],
  template: `
    <app-list-page heading="Compléments" addLabel="Nouveau complément" archivedLabel="Afficher les arrêtés"
      [(query)]="query" [(showArchived)]="showStopped" (add)="add()">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (list(); as l) {
        <div class="lo-stats">
          <div class="lo-stat"><span class="lo-stat-value">{{ euro(l.monthlyTotal) }}</span><span class="lo-stat-label">par mois (cures lissées)</span></div>
          <div class="lo-stat"><span class="lo-stat-value">{{ euro(l.monthlyTotal * 12) }}</span><span class="lo-stat-label">par an</span></div>
          <div class="lo-stat"><span class="lo-stat-value">{{ activeCount() }}</span><span class="lo-stat-label">compléments suivis</span></div>
        </div>
        <p class="lo-help">Coût par mois = prix × dose par jour × 30 ÷ contenu du conditionnement ; une cure qui se répète compte pour ses jours de cure.</p>
        @if (shown().length) {
          <div class="cards">@for (s of shown(); track s.id) { <app-supplement-card [supplement]="s" /> }</div>
        } @else {
          <p class="lo-empty">{{ l.supplements.length ? 'Aucun résultat.' : 'Aucun complément. Ajoute par exemple « Magnésium ».' }}</p>
        }
      }
    </app-list-page>
  `,
  styles: [`
    .lo-stat { display: flex; flex-direction: column; }
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class SupplementsPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly euro = euro
  readonly query = signal('')
  readonly showStopped = signal(false)
  readonly list = signal<SupplementListDto | null>(null)
  readonly error = signal('')
  readonly activeCount = computed(() => this.list()?.supplements.filter(s => !s.isStopped).length ?? 0)
  readonly shown = computed(() => (this.list()?.supplements ?? [])
    .filter((s: SupplementDto) => (this.showStopped() || !s.isStopped) && matchesQuery(s.name, this.query())))

  async ngOnInit(): Promise<void> { await this.load() }

  async add(): Promise<void> {
    const data: SupplementDialogData = { supplement: null, today: this.list()?.today ?? new Date().toISOString().slice(0, 10) }
    const result = await firstValueFrom(openEditor<SupplementDialogComponent, SupplementDialogData, SupplementDialogResult>(this.dialog, SupplementDialogComponent, data).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.list.set(await firstValueFrom(this.api.supplements(true))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
