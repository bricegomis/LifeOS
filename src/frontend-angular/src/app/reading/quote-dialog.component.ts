import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { ReadingApiService } from './reading-api.service'
import type { BookDto, BookQuoteDto } from './reading.models'

/** `quote` is null for a new one. */
export interface QuoteDialogData { book: BookDto, quote: BookQuoteDto | null }
export interface QuoteDialogResult { book: BookDto, message: string }

/** Adds, edits or removes a quote of a book, with its optional place (ADR 0022). */
@Component({
  selector: 'app-quote-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="quote ? 'Modifier la citation' : 'Ajouter une citation'" formId="quote-form" [saveLabel]="quote ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="quote ? 'Retirer la citation' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="quote-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Citation</mat-label>
          <textarea matInput name="text" rows="4" required maxlength="2000" [(ngModel)]="draft.text"></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Emplacement (facultatif)</mat-label>
          <input matInput name="place" maxlength="100" placeholder="p. 42, chap. 3…" [(ngModel)]="draft.place" />
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class QuoteDialogComponent {
  private readonly api = inject(ReadingApiService)
  private readonly ref = inject<MatDialogRef<QuoteDialogComponent, QuoteDialogResult>>(MatDialogRef)
  private readonly data = inject<QuoteDialogData>(MAT_DIALOG_DATA)
  readonly quote = this.data.quote
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = { text: this.quote?.text ?? '', place: this.quote?.place ?? '' }
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  save(): Promise<void> {
    const text = this.draft.text.trim()
    if (!text) { this.error.set('La citation est vide.'); return Promise.resolve() }
    const request = { text, place: this.draft.place.trim() || null }
    const id = this.data.book.id
    return this.run(this.quote ? this.api.updateQuote(id, this.quote.id, request) : this.api.addQuote(id, request),
      this.quote ? 'Citation enregistrée' : 'Citation ajoutée')
  }

  remove(): Promise<void> {
    return this.run(this.api.removeQuote(this.data.book.id, this.quote!.id), 'Citation retirée')
  }

  private async run(request: Observable<BookDto>, message: string): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      this.ref.close({ book: await firstValueFrom(request), message })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
