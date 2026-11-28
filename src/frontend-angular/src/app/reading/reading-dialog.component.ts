import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { ReadingApiService } from './reading-api.service'
import type { BookDto, BookReadingDto } from './reading.models'

/**
 * `finish`: the reading in progress is over; `stop`: it is set aside; `notwanted`: a book never read is set aside on
 * purpose; `past`: a reading already over is added; `edit`: a reading is corrected (or removed).
 */
export type ReadingDialogMode = 'finish' | 'stop' | 'notwanted' | 'past' | 'edit'
export interface ReadingDialogData { mode: ReadingDialogMode, book: BookDto, reading?: BookReadingDto, today: string }
export interface ReadingDialogResult { book: BookDto, message: string }

const HEADINGS: Record<ReadingDialogMode, string> = {
  finish: 'Lecture terminée', stop: 'Arrêter la lecture', notwanted: 'Pas envie', past: 'Ajouter une lecture passée', edit: 'Modifier la lecture',
}

/** One window for what happens to a reading (ADR 0022): dates, rating 0 to 10 (0 is a rating), review or reason. */
@Component({
  selector: 'app-reading-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="heading" formId="reading-form" [saveLabel]="saveLabel" [busy]="busy()" [dirty]="dirty()" [error]="error()"
      [dangerLabel]="mode === 'edit' ? 'Retirer cette lecture' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="reading-form" class="lo-form" (ngSubmit)="save()">
        @if (mode === 'notwanted' || mode === 'stop') {
          <p class="lo-help">{{ mode === 'stop' ? 'Le livre passe dans « Arrêté ». Vous pourrez le reprendre plus tard.'
            : 'Le livre passe dans « Pas envie ». Claude ne le proposera plus.' }}</p>
        }
        @if (mode === 'past') {
          <div class="lo-field-group">
            <span class="lo-field-label" id="reading-outcome">Issue</span>
            <mat-button-toggle-group name="outcome" aria-labelledby="reading-outcome" [(ngModel)]="draft.outcome" hideSingleSelectionIndicator>
              <mat-button-toggle value="read">Lu</mat-button-toggle>
              <mat-button-toggle value="stopped">Arrêté</mat-button-toggle>
            </mat-button-toggle-group>
          </div>
        }
        @if (mode === 'past' || mode === 'edit') {
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="day">
              <mat-label>Commencé le</mat-label>
              <input matInput type="date" name="startedOn" [max]="today" [required]="outcome() === 'reading'" [(ngModel)]="draft.startedOn" />
            </mat-form-field>
            @if (outcome() !== 'reading') {
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="day">
                <mat-label>{{ outcome() === 'read' ? 'Fini le' : 'Arrêté le' }}</mat-label>
                <input matInput type="date" name="finishedOn" [max]="today" [(ngModel)]="draft.finishedOn" />
              </mat-form-field>
            }
          </div>
          <span class="lo-help">Dates facultatives pour un livre lu il y a longtemps.</span>
        }
        @if (mode === 'finish' || mode === 'stop') {
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="day">
            <mat-label>{{ mode === 'finish' ? 'Terminé le' : 'Arrêté le' }}</mat-label>
            <input matInput type="date" name="day" required [max]="today" [(ngModel)]="draft.finishedOn" />
          </mat-form-field>
        }
        @if (outcome() === 'read') {
          <div class="lo-field-group">
            <span class="lo-field-label" id="reading-rating">Note sur 10 (facultative)</span>
            <mat-button-toggle-group name="rating" aria-labelledby="reading-rating" [(ngModel)]="draft.rating" hideSingleSelectionIndicator class="rating">
              <mat-button-toggle value="" aria-label="Sans note">—</mat-button-toggle>
              @for (n of ratings; track n) { <mat-button-toggle [value]="n">{{ n }}</mat-button-toggle> }
            </mat-button-toggle-group>
          </div>
          <mat-form-field appearance="outline">
            <mat-label>Avis (facultatif)</mat-label>
            <textarea matInput name="review" rows="3" maxlength="4000" [(ngModel)]="draft.review"></textarea>
          </mat-form-field>
        }
        @if (outcome() === 'stopped' || mode === 'notwanted') {
          <mat-form-field appearance="outline">
            <mat-label>Raison (facultative)</mat-label>
            <input matInput name="reason" maxlength="300" [placeholder]="mode === 'notwanted' ? 'Ex. pas mon genre' : 'Ex. trop long'" [(ngModel)]="draft.reason" />
          </mat-form-field>
        }
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .day { width: 100%; max-width: 240px; }
    .lo-form-row .day { flex: 1 1 200px; }
    .rating { flex-wrap: wrap; }
    .rating mat-button-toggle { min-width: 40px; }
  `],
})
export class ReadingDialogComponent {
  private readonly api = inject(ReadingApiService)
  private readonly ref = inject<MatDialogRef<ReadingDialogComponent, ReadingDialogResult>>(MatDialogRef)
  private readonly data = inject<ReadingDialogData>(MAT_DIALOG_DATA)
  readonly mode = this.data.mode
  readonly today = this.data.today
  readonly heading = HEADINGS[this.mode]
  readonly saveLabel = this.mode === 'stop' ? 'Arrêter' : this.mode === 'notwanted' ? 'Ranger' : this.mode === 'past' ? 'Ajouter' : 'Enregistrer'
  readonly ratings = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
  readonly busy = signal(false)
  readonly error = signal('')
  private readonly reading = this.data.reading
  readonly draft = {
    outcome: 'read' as 'read' | 'stopped',
    startedOn: this.reading?.startedOn ?? '',
    finishedOn: this.reading?.finishedOn ?? (this.mode === 'finish' || this.mode === 'stop' ? this.today : ''),
    rating: (this.reading?.rating ?? '') as number | '',
    review: this.reading?.review ?? '',
    reason: this.reading?.stopReason ?? '',
  }
  private readonly initial = formSnapshot(this.draft)

  /** What the reading is (or becomes): it decides which fields show. */
  outcome(): 'reading' | 'read' | 'stopped' | 'none' {
    if (this.mode === 'finish') return 'read'
    if (this.mode === 'stop') return 'stopped'
    if (this.mode === 'past') return this.draft.outcome
    if (this.mode === 'edit') return this.reading?.outcome ?? 'read'
    return 'none'
  }

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  async save(): Promise<void> {
    if (this.busy()) return
    const d = this.draft
    const rating = d.rating === '' ? null : Number(d.rating)
    const review = d.review.trim() || null
    const reason = d.reason.trim() || null
    const book = this.data.book
    let request: Observable<BookDto>
    let message: string
    switch (this.mode) {
      case 'finish':
        request = this.api.finish(book.id, { day: d.finishedOn || null, rating, review }); message = 'Lecture terminée'; break
      case 'stop':
        request = this.api.stop(book.id, { day: d.finishedOn || null, reason }); message = 'Lecture arrêtée'; break
      case 'notwanted':
        request = this.api.notWanted(book.id, reason); message = 'Rangé dans « Pas envie »'; break
      case 'past':
        request = this.api.addPastReading(book.id, {
          outcome: d.outcome, startedOn: d.startedOn || null, finishedOn: d.finishedOn || null,
          rating: d.outcome === 'read' ? rating : null, review: d.outcome === 'read' ? review : null, reason: d.outcome === 'stopped' ? reason : null,
        })
        message = 'Lecture ajoutée'
        break
      case 'edit': {
        const outcome = this.outcome()
        request = this.api.updateReading(book.id, this.reading!.id, {
          startedOn: d.startedOn || null, finishedOn: outcome === 'reading' ? null : d.finishedOn || null,
          rating: outcome === 'read' ? rating : null, review: outcome === 'read' ? review : null, reason: outcome === 'stopped' ? reason : null,
        })
        message = 'Lecture enregistrée'
        break
      }
    }
    await this.run(request, message)
  }

  remove(): Promise<void> {
    return this.run(this.api.removeReading(this.data.book.id, this.reading!.id), 'Lecture retirée')
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
