import { Component, DestroyRef, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { BookCoverComponent } from './book-cover.component'
import { ReadingApiService } from './reading-api.service'
import type { BookCreateRequest, BookDto, BookFormat, BookSeriesDto } from './reading.models'
import { FORMATS, parseGenres, parseTome, tomeLabel } from './reading-rules'

/** `book` is null for a new one; `seriesId` and `tome` prefill a new book (a missing tome of a series). */
export interface BookDialogData { book: BookDto | null, seriesId?: string, tome?: number, title?: string }
/** `book` is null when the book was deleted. */
export interface BookDialogResult { book: BookDto | null, message: string }

/**
 * Creates or edits a book (ADR 0022): cover, title, authors, format, genres, series and tome, notes; a new book also
 * says how it enters the library (à lire, en cours, déjà lu, pas envie). Deleted from here (second click).
 */
@Component({
  selector: 'app-book-dialog',
  standalone: true,
  imports: [BookCoverComponent, EditorFrameComponent, FormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame [heading]="book ? 'Modifier le livre' : 'Ajouter un livre'" formId="book-form" [saveLabel]="book ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="book ? 'Supprimer le livre' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="book-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-field-group">
          <span class="lo-field-label">Couverture</span>
          <div class="photo-row">
            <div class="photo">
              @if (preview(); as source) {
                <img [src]="source" alt="Nouvelle couverture" />
              } @else if (book && book.hasImage && !removeCover()) {
                <app-book-cover [bookId]="book.id" [hasImage]="true" [title]="book.title" [alt]="'Couverture : ' + book.title" />
              } @else {
                <app-book-cover bookId="" [title]="draft.title || 'Sans couverture'" alt="Pas de couverture" />
              }
              <label class="photo-edit" [attr.title]="hasCover() ? 'Changer la couverture' : 'Ajouter une couverture'">
                <mat-icon svgIcon="photo_camera" /><span class="sr-only">{{ hasCover() ? 'Changer la couverture' : 'Ajouter une couverture' }}</span>
                <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" (change)="pick($event)" />
              </label>
            </div>
            <div class="photo-side">
              <span class="lo-help">Facultative. Claude peut aussi la trouver pour toi.</span>
              @if (hasCover()) {
                <button mat-button type="button" class="photo-delete" (click)="dropCover()"><mat-icon svgIcon="delete" />Retirer la couverture</button>
              }
            </div>
          </div>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Titre</mat-label>
          <input matInput name="title" required maxlength="200" [(ngModel)]="draft.title" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Auteurs</mat-label>
          <input matInput name="authors" maxlength="300" placeholder="Ex. Frank Herbert" [(ngModel)]="draft.authors" />
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label" id="book-format">Format</span>
          <mat-button-toggle-group name="format" aria-labelledby="book-format" [(ngModel)]="draft.format" hideSingleSelectionIndicator class="wrap">
            @for (f of formats; track f.value) { <mat-button-toggle [value]="f.value"><mat-icon [svgIcon]="f.icon" />{{ f.label }}</mat-button-toggle> }
          </mat-button-toggle-group>
        </div>
        @if (!book) {
          <div class="lo-field-group">
            <span class="lo-field-label" id="book-state">État</span>
            <mat-button-toggle-group name="state" aria-labelledby="book-state" [(ngModel)]="draft.state" hideSingleSelectionIndicator class="wrap">
              <mat-button-toggle value="toread">À lire</mat-button-toggle>
              <mat-button-toggle value="reading">En cours</mat-button-toggle>
              <mat-button-toggle value="read">Déjà lu</mat-button-toggle>
              <mat-button-toggle value="notwanted">Pas envie</mat-button-toggle>
            </mat-button-toggle-group>
            <span class="lo-help">« En cours » commence aujourd’hui. Dates, note et avis d’un livre déjà lu se règlent ensuite sur sa page.</span>
          </div>
        }
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Genres</mat-label>
          <input matInput name="genres" maxlength="400" placeholder="Ex. SF, Polar" [(ngModel)]="draft.genres" />
          <mat-hint>Séparés par des virgules.{{ knownGenres().length ? ' Déjà utilisés : ' + knownGenres().join(', ') + '.' : '' }}</mat-hint>
        </mat-form-field>
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="series">
            <mat-label>Série</mat-label>
            <mat-select name="series" [(ngModel)]="draft.seriesId">
              <mat-option value="">Aucune</mat-option>
              @for (s of series(); track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
            </mat-select>
            <mat-hint>Les séries se créent depuis la page Séries.</mat-hint>
          </mat-form-field>
          @if (draft.seriesId) {
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="tome">
              <mat-label>Tome</mat-label>
              <input matInput name="tome" inputmode="decimal" placeholder="Ex. 2" [(ngModel)]="draft.tome" />
            </mat-form-field>
          }
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Notes</mat-label>
          <textarea matInput name="notes" rows="3" maxlength="4000" [(ngModel)]="draft.notes"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .photo-row { display: flex; align-items: flex-end; gap: 16px; }
    .photo { position: relative; width: 96px; flex: 0 0 auto; }
    .photo img { display: block; width: 96px; aspect-ratio: 2 / 3; border-radius: 8px; object-fit: cover; }
    .photo-edit {
      position: absolute; right: -8px; bottom: -8px; display: inline-flex; width: 40px; height: 40px; align-items: center; justify-content: center;
      border-radius: 12px; background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); box-shadow: var(--mat-sys-level2); cursor: pointer;
    }
    .photo-edit:focus-within { outline: 3px solid var(--mat-sys-primary); outline-offset: 2px; }
    .photo-side { display: flex; min-width: 0; flex-direction: column; align-items: flex-start; gap: 4px; }
    .photo-delete { color: var(--mat-sys-error); }
    .wrap { flex-wrap: wrap; }
    .series { flex: 1 1 240px; }
    .tome { flex: 0 1 120px; }
  `],
})
export class BookDialogComponent {
  private readonly api = inject(ReadingApiService)
  private readonly ref = inject<MatDialogRef<BookDialogComponent, BookDialogResult>>(MatDialogRef)
  private readonly data = inject<BookDialogData>(MAT_DIALOG_DATA)
  readonly book = this.data.book
  readonly formats = FORMATS
  readonly busy = signal(false)
  readonly error = signal('')
  readonly preview = signal<string | null>(null)
  readonly removeCover = signal(false)
  readonly series = signal<BookSeriesDto[]>([])
  readonly knownGenres = signal<string[]>([])
  readonly draft = {
    title: this.book?.title ?? this.data.title ?? '',
    authors: this.book?.authors ?? '',
    format: (this.book?.format ?? 'paper') as BookFormat,
    state: 'toread' as BookCreateRequest['state'],
    genres: this.book?.genres.join(', ') ?? '',
    seriesId: this.book?.seriesId ?? this.data.seriesId ?? '',
    tome: this.book?.tome != null ? tomeLabel(this.book.tome) : this.data.tome != null ? tomeLabel(this.data.tome) : '',
    notes: this.book?.notes ?? '',
  }
  private file: File | null = null
  private readonly initial = formSnapshot(this.draft)

  constructor() {
    inject(DestroyRef).onDestroy(() => this.releasePreview())
    firstValueFrom(this.api.seriesList()).then(list => this.series.set(list), () => { /* no series to pick */ })
    firstValueFrom(this.api.genres()).then(list => this.knownGenres.set(list), () => { /* no hint */ })
  }

  hasCover(): boolean { return !!this.preview() || (!!this.book?.hasImage && !this.removeCover()) }
  dirty(): boolean { return hasChanges(this.initial, this.draft) || !!this.file || this.removeCover() }

  pick(event: Event): void {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return
    this.releasePreview()
    this.file = file
    this.removeCover.set(false)
    this.preview.set(URL.createObjectURL(file))
  }

  dropCover(): void {
    this.releasePreview()
    this.file = null
    this.removeCover.set(!!this.book?.hasImage)
  }

  async save(): Promise<void> {
    if (this.busy()) return
    const title = this.draft.title.trim()
    if (!title) { this.error.set('Le titre est obligatoire.'); return }
    const seriesId = this.draft.seriesId || null
    const tome = seriesId ? parseTome(this.draft.tome) : null
    if (seriesId && String(this.draft.tome).trim() && tome === null) { this.error.set('Le tome est un nombre positif, par exemple 2 ou 2,5.'); return }
    const request = {
      title, authors: this.draft.authors.trim() || null, format: this.draft.format, genres: parseGenres(this.draft.genres),
      notes: this.draft.notes.trim() || null, seriesId, tome,
    }
    this.busy.set(true); this.error.set('')
    try {
      let book = await firstValueFrom(this.book ? this.api.updateBook(this.book.id, request) : this.api.createBook({ ...request, state: this.draft.state }))
      if (this.file) book = await firstValueFrom(this.api.uploadCover(book.id, this.file))
      else if (this.removeCover() && book.hasImage) {
        await firstValueFrom(this.api.removeCover(book.id))
        book = { ...book, hasImage: false }
      }
      this.ref.close({ book, message: this.book ? 'Livre enregistré' : `« ${book.title} » ajouté` })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async remove(): Promise<void> {
    if (!this.book || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.deleteBook(this.book.id))
      this.ref.close({ book: null, message: `« ${this.book.title} » supprimé` })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  private releasePreview(): void {
    const url = this.preview()
    if (url) URL.revokeObjectURL(url)
    this.preview.set(null)
  }
}
