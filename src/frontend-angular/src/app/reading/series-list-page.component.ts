import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { ReadingApiService } from './reading-api.service'
import type { BookSeriesDto } from './reading.models'
import { SeriesDialogComponent, type SeriesDialogData, type SeriesDialogResult } from './series-dialog.component'

/** The household's book series (ADR 0022): books, read, to read and missing tomes of each. */
@Component({
  selector: 'app-series-list-page',
  standalone: true,
  imports: [ListPageComponent, MatIconModule, RouterLink],
  template: `
    <app-list-page heading="Séries" intro="Les tomes lus, à lire et manquants de chaque série." addLabel="Nouvelle série" [(query)]="query" (add)="add()">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (shown().length) {
          <div class="lo-list">
            @for (s of shown(); track s.id) {
              <a class="lo-row row" [routerLink]="['/reading/series', s.id]">
                <span class="lo-row-main">
                  <mat-icon class="lo-lead-icon" svgIcon="collections_bookmark" />
                  <span class="lo-row-text">
                    <span class="lo-row-title">{{ s.name }}</span>
                    <span class="lo-row-sub">{{ summary(s) }}</span>
                  </span>
                </span>
              </a>
            }
          </div>
        } @else {
          <p class="lo-empty">{{ series().length ? 'Aucun résultat.' : 'Aucune série. Ajoute par exemple « Dune » avec ses 6 tomes.' }}</p>
        }
      }
    </app-list-page>
  `,
  styles: [`.row { color: inherit; text-decoration: none; }`],
})
export class SeriesListPageComponent implements OnInit {
  private readonly api = inject(ReadingApiService)
  private readonly dialog = inject(MatDialog)
  private readonly router = inject(Router)
  readonly query = signal('')
  readonly series = signal<BookSeriesDto[]>([])
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly shown = computed(() => this.series().filter(s => matchesQuery(s.name, this.query())))

  async ngOnInit(): Promise<void> {
    try { this.series.set(await firstValueFrom(this.api.seriesList())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }

  summary(s: BookSeriesDto): string { return seriesSummary(s) }

  async add(): Promise<void> {
    const result = await firstValueFrom(openEditor<SeriesDialogComponent, SeriesDialogData, SeriesDialogResult>(this.dialog, SeriesDialogComponent, { series: null }).afterClosed())
    if (result?.detail) await this.router.navigate(['/reading/series', result.detail.series.id])
  }
}

/** "6 tomes · 2 lus · 1 à lire · 3 manquants". */
export function seriesSummary(s: BookSeriesDto): string {
  const plural = (n: number, word: string) => `${n} ${word}${n > 1 ? 's' : ''}`
  const bits = [s.totalTomes ? plural(s.totalTomes, 'tome') : plural(s.bookCount, 'livre'), plural(s.readCount, 'lu'), `${s.toReadCount} à lire`]
  if (s.totalTomes) bits.push(plural(s.missingTomes.length, 'manquant'))
  return bits.join(' · ')
}
