import { Location } from '@angular/common'
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { BookCoverComponent } from './book-cover.component'
import { BookDialogComponent, type BookDialogData, type BookDialogResult } from './book-dialog.component'
import { QuoteDialogComponent, type QuoteDialogData, type QuoteDialogResult } from './quote-dialog.component'
import { ReadingApiService } from './reading-api.service'
import { ReadingDialogComponent, type ReadingDialogData, type ReadingDialogMode, type ReadingDialogResult } from './reading-dialog.component'
import type { BookDto, BookQuoteDto, BookReadingDto } from './reading.models'
import { formatInfo, localToday, readingDates, readingTitle, seriesLabel, stateDetail, STATES } from './reading-rules'

interface Action { label: string, icon: string, main: boolean, run: () => void }

/**
 * One book (ADR 0022): cover, authors, format, series, genres; its state with the actions it allows (Commencer,
 * Terminé, Arrêter, Relire, Reprendre, Pas envie); its readings, most recent first, each with its rating and
 * review; quotes; notes. Everything is changed through windows.
 */
@Component({
  selector: 'app-book-page',
  standalone: true,
  imports: [BookCoverComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page book-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (book(); as b) {
        <header class="head">
          <button mat-icon-button type="button" (click)="back()" aria-label="Retour"><mat-icon svgIcon="arrow_back" /></button>
          <h1 class="lo-bar-title head-title">Livre</h1>
          <button mat-flat-button type="button" class="lo-desktop-only" (click)="edit(b)"><mat-icon svgIcon="edit" />Modifier</button>
          <button mat-icon-button type="button" class="lo-phone-only" (click)="edit(b)" aria-label="Modifier le livre" title="Modifier le livre">
            <mat-icon svgIcon="edit" /></button>
        </header>

        <div class="layout">
          <div class="side">
            <app-book-cover class="cover" [bookId]="b.id" [hasImage]="b.hasImage" [title]="b.title" [version]="b.updatedAt"
              [alt]="b.hasImage ? 'Couverture : ' + b.title : ''" />
            <div class="facts">
              <h2 class="title">{{ b.title }}</h2>
              @if (b.authors) { <span class="lo-muted">{{ b.authors }}</span> }
              <span class="format"><mat-icon [svgIcon]="format().icon" />{{ format().label }}</span>
              @if (b.seriesId) { <a [routerLink]="['/reading/series', b.seriesId]">{{ series() }}</a> }
              @if (b.genres.length) {
                <span class="lo-chips">@for (g of b.genres; track g) { <span class="lo-chip">{{ g }}</span> }</span>
              }
            </div>
          </div>

          <div class="main">
            <section class="lo-panel" aria-label="État">
              <div class="state">
                <span class="lo-tag" [class]="'lo-tag ' + state().tag"><mat-icon [svgIcon]="state().icon" />{{ state().label }}</span>
                <span class="lo-muted">{{ detail() }}</span>
              </div>
              <div class="actions">
                @for (a of actions(); track a.label) {
                  @if (a.main) {
                    <button mat-flat-button type="button" [disabled]="busy()" (click)="a.run()"><mat-icon [svgIcon]="a.icon" />{{ a.label }}</button>
                  } @else {
                    <button mat-stroked-button type="button" [disabled]="busy()" (click)="a.run()"><mat-icon [svgIcon]="a.icon" />{{ a.label }}</button>
                  }
                }
              </div>
            </section>

            <section class="lo-section" aria-labelledby="book-readings">
              <div class="lo-section-head">
                <h2 id="book-readings">Lectures</h2><span class="lo-muted">{{ b.readings.length }}</span>
                @if (b.state !== 'reading') {
                  <button mat-button type="button" class="head-add" (click)="openReading('past')"><mat-icon svgIcon="add" />Lecture passée</button>
                }
              </div>
              @if (readings().length) {
                <div class="lo-list">
                  @for (r of readings(); track r.reading.id) {
                    <button type="button" class="lo-row reading" (click)="openReading('edit', r.reading)" [attr.aria-label]="'Modifier : ' + r.title">
                      <span class="lo-row-main">
                        <mat-icon class="lo-lead-icon" [svgIcon]="r.icon" />
                        <span class="lo-row-text">
                          <span class="lo-row-title">{{ r.title }}</span>
                          <span class="lo-row-sub">{{ r.dates }}</span>
                          @if (r.text) { <span class="text">{{ r.text }}</span> }
                        </span>
                      </span>
                      @if (r.reading.rating !== null) { <span class="rating">{{ r.reading.rating }}<span class="lo-muted">/10</span></span> }
                    </button>
                  }
                </div>
              } @else {
                <p class="lo-muted">Pas encore lu.</p>
              }
            </section>

            <section class="lo-section" aria-labelledby="book-quotes">
              <div class="lo-section-head">
                <h2 id="book-quotes">Citations</h2><span class="lo-muted">{{ b.quotes.length }}</span>
                <button mat-button type="button" class="head-add" (click)="openQuote(null)"><mat-icon svgIcon="add" />Ajouter</button>
              </div>
              @for (q of b.quotes; track q.id) {
                <button type="button" class="quote" (click)="openQuote(q)" aria-label="Modifier la citation">
                  <span class="quote-text">« {{ q.text }} »</span>
                  @if (q.place) { <span class="lo-muted">{{ q.place }}</span> }
                </button>
              } @empty {
                <p class="lo-muted">Aucune citation.</p>
              }
            </section>

            <section class="lo-section" aria-labelledby="book-notes">
              <div class="lo-section-head"><h2 id="book-notes">Notes</h2></div>
              <p class="notes" [class.lo-muted]="!b.notes">{{ b.notes || 'Aucune note.' }}</p>
            </section>
          </div>
        </div>
      } @else if (!error()) {
        <p class="lo-muted" role="status">Chargement…</p>
      }
    </div>
  `,
  styles: [`
    .book-page { max-width: 1040px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .layout { display: flex; flex-wrap: wrap; align-items: flex-start; gap: 24px; }
    .side { display: flex; flex: 0 1 220px; min-width: 0; flex-direction: column; gap: 16px; }
    .main { display: flex; flex: 999 1 420px; min-width: 0; flex-direction: column; gap: 20px; }
    .cover { width: 100%; }
    .facts { display: flex; min-width: 0; flex-direction: column; gap: 6px; }
    .title { margin: 0; font: var(--mat-sys-headline-small); }
    .format { display: inline-flex; align-items: center; gap: 6px; color: var(--mat-sys-on-surface-variant); }
    .format mat-icon { width: 18px; height: 18px; }
    .facts a { color: var(--mat-sys-primary); font: var(--mat-sys-title-small); }
    .state { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .state .lo-tag mat-icon { width: 16px; height: 16px; margin-right: 4px; vertical-align: -3px; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .head-add { margin-left: auto; }
    .reading { width: 100%; border: 0; background: transparent; color: inherit; font: inherit; text-align: left; cursor: pointer; }
    .text { white-space: pre-line; }
    .rating { flex: 0 0 auto; font: var(--mat-sys-title-large); font-weight: 700; }
    .rating .lo-muted { font: var(--mat-sys-label-medium); }
    .quote {
      display: flex; width: 100%; flex-direction: column; gap: 4px; padding: 12px 16px; border: 0; border-radius: 16px;
      background: var(--mat-sys-surface-container); color: inherit; font: inherit; text-align: left; cursor: pointer;
    }
    .quote:focus-visible, .reading:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: -3px; }
    .quote-text { font-style: italic; white-space: pre-line; }
    .notes { margin: 0; white-space: pre-line; }
    @media (max-width: 600px) {
      .side { flex: 1 1 100%; flex-direction: row; }
      .cover { width: 112px; }
      .title { font: var(--mat-sys-title-large); }
    }
  `],
})
export class BookPageComponent implements OnInit {
  private readonly api = inject(ReadingApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly route = inject(ActivatedRoute)
  private readonly router = inject(Router)
  private readonly location = inject(Location)
  private readonly destroyRef = inject(DestroyRef)
  private id = ''
  readonly book = signal<BookDto | null>(null)
  readonly error = signal('')
  readonly busy = signal(false)
  readonly state = computed(() => STATES[this.book()?.state ?? 'toread'])
  readonly format = computed(() => formatInfo(this.book()?.format ?? 'paper'))
  readonly detail = computed(() => { const b = this.book(); return b ? stateDetail(b) : '' })
  readonly series = computed(() => { const b = this.book(); return b ? seriesLabel(b) : '' })
  /** Most recent first. */
  readonly readings = computed(() => {
    const list = this.book()?.readings ?? []
    return list.map((reading, index) => ({
      reading,
      title: readingTitle(reading, index, list.length),
      dates: readingDates(reading),
      text: reading.outcome === 'stopped' ? reading.stopReason : reading.review,
      icon: STATES[reading.outcome === 'reading' ? 'reading' : reading.outcome].icon,
    })).reverse()
  })
  readonly actions = computed<Action[]>(() => {
    const b = this.book()
    if (!b) return []
    const start = (label: string, icon: string, main = true): Action => ({ label, icon, main, run: () => void this.start(label) })
    switch (b.state) {
      case 'toread': return [start('Commencer', 'play_arrow'), { label: 'Pas envie', icon: 'do_not_disturb_on', main: false, run: () => void this.openReading('notwanted') }]
      case 'reading': return [
        { label: 'Terminé', icon: 'check', main: true, run: () => void this.openReading('finish') },
        { label: 'Arrêter', icon: 'pause', main: false, run: () => void this.openReading('stop') },
      ]
      case 'read': return [start('Relire', 'replay')]
      case 'stopped': return [start('Reprendre', 'play_arrow')]
      case 'notwanted': return [
        { label: 'Remettre à lire', icon: 'bookmark_add', main: true, run: () => void this.run(this.api.clearNotWanted(b.id), 'Remis dans « À lire »') },
        start('Commencer', 'play_arrow', false),
      ]
    }
  })

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.id = params.get('id') ?? ''
      void this.load()
    })
  }

  back(): void {
    if (globalThis.history?.length > 1) this.location.back()
    else void this.router.navigate(['/reading'])
  }

  start(label: string): Promise<void> {
    return this.run(this.api.start(this.id), label === 'Relire' ? 'Nouvelle lecture commencée' : 'Lecture commencée')
  }

  async edit(book: BookDto): Promise<void> {
    const result = await firstValueFrom(openEditor<BookDialogComponent, BookDialogData, BookDialogResult>(this.dialog, BookDialogComponent, { book }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    if (result.book) this.book.set(result.book)
    else await this.router.navigate(['/reading'])
  }

  async openReading(mode: ReadingDialogMode, reading?: BookReadingDto): Promise<void> {
    const book = this.book()
    if (!book) return
    const result = await firstValueFrom(openEditor<ReadingDialogComponent, ReadingDialogData, ReadingDialogResult>(this.dialog, ReadingDialogComponent,
      { mode, book, reading, today: localToday() }).afterClosed())
    if (!result) return
    this.book.set(result.book)
    this.snack.open(result.message, undefined, { duration: 4000 })
  }

  async openQuote(quote: BookQuoteDto | null): Promise<void> {
    const book = this.book()
    if (!book) return
    const result = await firstValueFrom(openEditor<QuoteDialogComponent, QuoteDialogData, QuoteDialogResult>(this.dialog, QuoteDialogComponent, { book, quote }).afterClosed())
    if (!result) return
    this.book.set(result.book)
    this.snack.open(result.message, undefined, { duration: 4000 })
  }

  private async run(request: Observable<BookDto>, message: string): Promise<void> {
    this.busy.set(true)
    try {
      this.book.set(await firstValueFrom(request))
      this.snack.open(message, undefined, { duration: 4000 })
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
    } finally {
      this.busy.set(false)
    }
  }

  private async load(): Promise<void> {
    try { this.book.set(await firstValueFrom(this.api.book(this.id))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
