import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent } from '@/app/shared/ui/list-page.component'
import { BookCoverComponent } from './book-cover.component'
import { BookDialogComponent, type BookDialogData, type BookDialogResult } from './book-dialog.component'
import { ReadingApiService } from './reading-api.service'
import type { BookDto, BookFormat, BookState } from './reading.models'
import { cardMeta, filterBooks, FORMATS, formatInfo, genresOf, STATES, TABS, tabCounts, tomeLabel } from './reading-rules'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

const VIEW_KEY = 'lifeos.reading.view'

/**
 * The reading library (ADR 0022): tabs by state (« Tous » leaves « Pas envie » out), a search on title, authors,
 * series and genres, chips by format and genre, then covers in a grid or a list. Filled by hand or by Claude.
 */
@Component({
  selector: 'app-library-page',
  standalone: true,
  imports: [SkeletonComponent, BookCoverComponent, ListPageComponent, MatButtonToggleModule, MatIconModule, RouterLink],
  template: `
    <app-list-page heading="Lecture" intro="Les livres lus, en cours, à lire ou écartés." addLabel="Ajouter un livre"
      [(query)]="query" (add)="add()">
      <a pageActions class="lo-filter-chip series-link" routerLink="/reading/series"><mat-icon svgIcon="collections_bookmark" />Séries</a>

      <div class="tabs" role="group" aria-label="État">
        @for (t of tabs; track t.value) {
          <button type="button" class="tab" [class.is-on]="tab() === t.value" [attr.aria-pressed]="tab() === t.value" (click)="tab.set(t.value)">
            {{ t.label }} <span class="tab-count">{{ counts()[t.value] }}</span>
          </button>
        }
      </div>

      <div class="chips">
        <div class="lo-chips" role="group" aria-label="Format">
          @for (f of formats; track f.value) {
            <button type="button" class="lo-filter-chip" [class.is-on]="format() === f.value" [attr.aria-pressed]="format() === f.value"
              (click)="format.set(format() === f.value ? null : f.value)"><mat-icon [svgIcon]="f.icon" />{{ f.label }}</button>
          }
        </div>
        @if (genres().length) {
          <div class="lo-chips" role="group" aria-label="Genre">
            @for (g of genres(); track g) {
              <button type="button" class="lo-filter-chip" [class.is-on]="genre() === g" [attr.aria-pressed]="genre() === g"
                (click)="genre.set(genre() === g ? null : g)">{{ g }}</button>
            }
          </div>
        }
        <mat-button-toggle-group class="view" [value]="view()" (change)="setView($event.value)" aria-label="Affichage" hideSingleSelectionIndicator>
          <mat-button-toggle value="grid" aria-label="Grille de couvertures" title="Grille de couvertures"><mat-icon svgIcon="grid_view" /></mat-button-toggle>
          <mat-button-toggle value="list" aria-label="Liste" title="Liste"><mat-icon svgIcon="view_list" /></mat-button-toggle>
        </mat-button-toggle-group>
      </div>

      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (!shown().length) {
          <p class="lo-empty">{{ books().length ? 'Aucun livre ici. Change de filtre.' : 'Aucun livre. Ajoutes-en un, ou demande à Claude d’importer ta liste.' }}</p>
        } @else if (view() === 'grid') {
          <div class="grid">
            @for (b of shown(); track b.id) {
              <a class="tile" [routerLink]="['/reading/book', b.id]" [attr.aria-label]="b.title + ', ' + state(b.state).label">
                <span class="tile-cover">
                  <app-book-cover [bookId]="b.id" [hasImage]="b.hasImage" [title]="b.title" [version]="b.updatedAt" />
                  @if (b.state !== 'toread') { <span class="lo-tag badge" [class]="'lo-tag badge ' + state(b.state).tag">{{ state(b.state).label }}</span> }
                </span>
                <span class="tile-title">{{ b.title }}</span>
                @if (b.authors) { <span class="tile-sub">{{ b.authors }}</span> }
                <span class="tile-sub">{{ meta(b) }}</span>
              </a>
            }
          </div>
        } @else {
          <div class="lo-list">
            @for (b of shown(); track b.id) {
              <a class="lo-row row" [routerLink]="['/reading/book', b.id]">
                <span class="lo-row-main">
                  <app-book-cover width="40px" [showTitle]="false" [bookId]="b.id" [hasImage]="b.hasImage" [title]="b.title" [version]="b.updatedAt" />
                  <span class="lo-row-text">
                    <span class="lo-row-title">{{ b.title }}</span>
                    <span class="lo-row-sub">{{ listSub(b) }}</span>
                  </span>
                </span>
                <span class="row-side">
                  <span class="lo-tag" [class]="'lo-tag ' + state(b.state).tag">{{ state(b.state).label }}</span>
                  @if (b.lastRating !== null) { <span class="lo-muted">{{ b.lastRating }}/10</span> }
                </span>
              </a>
            }
          </div>
        }
      } @else if (!error()) {
        <app-skeleton />
      }
    </app-list-page>
  `,
  styles: [`
    .series-link { text-decoration: none; }
    .tabs { display: flex; gap: 4px; overflow-x: auto; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .tab {
      flex: 0 0 auto; min-height: 48px; padding: 0 14px; border: 0; border-bottom: 3px solid transparent; background: transparent;
      color: var(--mat-sys-on-surface); font: var(--mat-sys-title-small); cursor: pointer; white-space: nowrap;
    }
    .tab.is-on { border-bottom-color: var(--mat-sys-primary); color: var(--mat-sys-primary); }
    .tab:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: -3px; }
    .tab-count { color: var(--mat-sys-on-surface-variant); font-weight: 400; }
    .chips { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; }
    .view { margin-left: auto; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 20px 16px; }
    .tile { display: flex; min-width: 0; flex-direction: column; gap: 4px; color: inherit; text-decoration: none; border-radius: 8px; }
    .tile:focus-visible { outline: 3px solid var(--mat-sys-primary); outline-offset: 3px; }
    .tile:hover .tile-title { text-decoration: underline; }
    .tile-cover { position: relative; display: block; }
    .badge { position: absolute; top: 6px; left: 6px; }
    .tile-title { overflow: hidden; font: var(--mat-sys-title-small); text-overflow: ellipsis; white-space: nowrap; }
    .tile-sub { overflow: hidden; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); text-overflow: ellipsis; white-space: nowrap; }
    .row { color: inherit; text-decoration: none; }
    .row-side { display: flex; flex: 0 0 auto; flex-direction: column; align-items: flex-end; gap: 4px; }
    @media (max-width: 600px) { .grid { grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px 10px; } .view { margin-left: 0; } }
  `],
})
export class LibraryPageComponent implements OnInit {
  private readonly api = inject(ReadingApiService)
  private readonly dialog = inject(MatDialog)
  private readonly router = inject(Router)
  readonly tabs = TABS
  readonly formats = FORMATS
  readonly query = signal('')
  readonly tab = signal<'all' | BookState>('all')
  readonly format = signal<BookFormat | null>(null)
  readonly genre = signal<string | null>(null)
  readonly view = signal<'grid' | 'list'>(readView())
  readonly books = signal<BookDto[]>([])
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly genres = computed(() => genresOf(this.books()))
  private readonly filter = computed(() => ({ query: this.query(), format: this.format(), genre: this.genre() }))
  readonly counts = computed(() => tabCounts(this.books(), this.filter()))
  readonly shown = computed(() => filterBooks(this.books(), { ...this.filter(), tab: this.tab() }))

  async ngOnInit(): Promise<void> {
    try { this.books.set(await firstValueFrom(this.api.books('any'))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }

  state(state: BookState) { return STATES[state] }
  meta(book: BookDto): string { return cardMeta(book) }
  listSub(book: BookDto): string {
    const bits = [book.authors, formatInfo(book.format).label]
    if (book.seriesName) bits.push(book.tome !== null ? `${book.seriesName} ${tomeLabel(book.tome)}` : book.seriesName)
    return bits.filter(Boolean).join(' · ')
  }

  setView(view: 'grid' | 'list'): void {
    this.view.set(view)
    try { globalThis.localStorage?.setItem(VIEW_KEY, view) } catch { /* private mode: the grid comes back */ }
  }

  async add(): Promise<void> {
    const result = await firstValueFrom(openEditor<BookDialogComponent, BookDialogData, BookDialogResult>(this.dialog, BookDialogComponent, { book: null }).afterClosed())
    if (result?.book) await this.router.navigate(['/reading/book', result.book.id])
  }
}

function readView(): 'grid' | 'list' {
  try { return globalThis.localStorage?.getItem(VIEW_KEY) === 'list' ? 'list' : 'grid' } catch { return 'grid' }
}
