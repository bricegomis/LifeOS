import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { BookCoverComponent } from './book-cover.component'
import { BookDialogComponent, type BookDialogData, type BookDialogResult } from './book-dialog.component'
import { ReadingApiService } from './reading-api.service'
import type { BookSeriesDetailDto } from './reading.models'
import { formatInfo, STATES, tomeLabel } from './reading-rules'
import { SeriesDialogComponent, type SeriesDialogData, type SeriesDialogResult } from './series-dialog.component'
import { seriesSummary } from './series-list-page.component'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

/** One series (ADR 0022): its tomes in order, read, to read and missing; a missing tome is added from its row. */
@Component({
  selector: 'app-series-page',
  standalone: true,
  imports: [SkeletonComponent, BookCoverComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page series-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (detail(); as d) {
        <header class="head">
          <a mat-icon-button class="lo-back-link" routerLink="/reading/series" aria-label="Retour aux séries"><mat-icon svgIcon="arrow_back" /></a>
          <h1 class="lo-bar-title head-title">Série · {{ d.series.name }}</h1>
          <button mat-flat-button type="button" class="lo-desktop-only" (click)="edit()"><mat-icon svgIcon="edit" />Modifier</button>
          <button mat-icon-button type="button" class="lo-phone-only" (click)="edit()" aria-label="Modifier la série" title="Modifier la série">
            <mat-icon svgIcon="edit" /></button>
        </header>
        <p class="lo-muted summary">{{ summary() }}</p>
        @if (d.series.totalTomes) {
          <div class="bar" role="progressbar" aria-label="Tomes lus" aria-valuemin="0" [attr.aria-valuemax]="d.series.totalTomes"
            [attr.aria-valuenow]="d.series.readCount"><span [style.width.%]="percent()"></span></div>
        }
        @if (d.tomes.length) {
          <div class="lo-list">
            @for (t of d.tomes; track $index) {
              @if (t.book; as b) {
                <a class="lo-row row" [routerLink]="['/reading/book', b.id]">
                  <span class="lo-row-main">
                    <span class="num">{{ t.number !== null ? tome(t.number) : '–' }}</span>
                    <app-book-cover width="36px" [showTitle]="false" [bookId]="b.id" [hasImage]="b.hasImage" [title]="b.title" [version]="b.updatedAt" />
                    <span class="lo-row-text">
                      <span class="lo-row-title">{{ b.title }}</span>
                      <span class="lo-row-sub">{{ format(b.format) }}{{ b.lastRating !== null ? ' · ' + b.lastRating + '/10' : '' }}</span>
                    </span>
                  </span>
                  <span class="lo-tag" [class]="'lo-tag ' + state(b.state).tag">{{ state(b.state).label }}</span>
                </a>
              } @else {
                <div class="lo-row missing">
                  <span class="lo-row-main">
                    <span class="num">{{ tome(t.number!) }}</span>
                    <span class="hole" aria-hidden="true"></span>
                    <span class="lo-row-text"><span class="lo-row-title is-missing">Tome {{ tome(t.number!) }} manquant</span></span>
                  </span>
                  <button mat-button type="button" (click)="addTome(t.number!)" [attr.aria-label]="'Ajouter le tome ' + tome(t.number!)">
                    <mat-icon svgIcon="add" />Ajouter</button>
                </div>
              }
            }
          </div>
        } @else {
          <p class="lo-muted">Aucun livre dans cette série. Range un livre ici depuis sa fiche, ou donne le nombre de tomes.</p>
        }
        <p class="lo-help">Sans nombre de tomes, LifeOS ne montre pas de tomes manquants.</p>
      } @else if (!error()) {
        <app-skeleton variant="page" />
      }
    </div>
  `,
  styles: [`
    .series-page { max-width: 720px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .summary { margin: 0; }
    .bar { height: 8px; overflow: hidden; border-radius: 4px; background: var(--mat-sys-secondary-container); }
    .bar span { display: block; height: 100%; background: var(--mat-sys-primary); }
    .row { color: inherit; text-decoration: none; }
    .num { flex: 0 0 32px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-title-medium); font-weight: 700; text-align: center; }
    .hole { flex: 0 0 auto; box-sizing: border-box; width: 36px; aspect-ratio: 2 / 3; border: 1px dashed var(--mat-sys-outline); border-radius: 8px; }
    .is-missing { color: var(--mat-sys-on-surface-variant); font-style: italic; }
  `],
})
export class SeriesPageComponent implements OnInit {
  private readonly api = inject(ReadingApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly route = inject(ActivatedRoute)
  private readonly router = inject(Router)
  private readonly destroyRef = inject(DestroyRef)
  private id = ''
  readonly detail = signal<BookSeriesDetailDto | null>(null)
  readonly error = signal('')
  readonly summary = computed(() => { const d = this.detail(); return d ? seriesSummary(d.series) : '' })
  readonly percent = computed(() => {
    const s = this.detail()?.series
    return s?.totalTomes ? Math.min(100, Math.round(s.readCount * 100 / s.totalTomes)) : 0
  })

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.id = params.get('id') ?? ''
      void this.load()
    })
  }

  tome(n: number): string { return tomeLabel(n) }
  format(format: Parameters<typeof formatInfo>[0]): string { return formatInfo(format).label }
  state(state: keyof typeof STATES) { return STATES[state] }

  async edit(): Promise<void> {
    const series = this.detail()?.series
    if (!series) return
    const result = await firstValueFrom(openEditor<SeriesDialogComponent, SeriesDialogData, SeriesDialogResult>(this.dialog, SeriesDialogComponent, { series }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    if (result.detail) this.detail.set(result.detail)
    else await this.router.navigate(['/reading/series'])
  }

  async addTome(number: number): Promise<void> {
    const series = this.detail()?.series
    if (!series) return
    const result = await firstValueFrom(openEditor<BookDialogComponent, BookDialogData, BookDialogResult>(this.dialog, BookDialogComponent,
      { book: null, seriesId: series.id, tome: number, title: '' }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.detail.set(await firstValueFrom(this.api.series(this.id))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
