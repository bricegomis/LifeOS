import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { ReadingApiService } from './reading-api.service'
import type { BookSeriesDetailDto, BookSeriesDto } from './reading.models'

/** `series` is null for a new one. */
export interface SeriesDialogData { series: BookSeriesDto | null }
/** `detail` is null when the series was deleted. */
export interface SeriesDialogResult { detail: BookSeriesDetailDto | null, message: string }

/** Creates, edits or deletes a series (ADR 0022); deleting keeps its books, without series nor tome. */
@Component({
  selector: 'app-series-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="series ? 'Modifier la série' : 'Nouvelle série'" formId="series-form" [saveLabel]="series ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="series ? 'Supprimer la série' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="series-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" required maxlength="200" placeholder="Ex. Dune" [(ngModel)]="draft.name" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="total">
          <mat-label>Nombre de tomes</mat-label>
          <input matInput type="number" name="total" min="1" max="999" [(ngModel)]="draft.total" />
          <mat-hint>Facultatif. Connu, il montre les tomes manquants.</mat-hint>
        </mat-form-field>
        @if (series) { <p class="lo-help">Supprimer la série garde ses livres dans la bibliothèque, sans série.</p> }
      </form>
    </app-editor-frame>
  `,
  styles: [`.total { max-width: 240px; }`],
})
export class SeriesDialogComponent {
  private readonly api = inject(ReadingApiService)
  private readonly ref = inject<MatDialogRef<SeriesDialogComponent, SeriesDialogResult>>(MatDialogRef)
  readonly series = inject<SeriesDialogData>(MAT_DIALOG_DATA).series
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = { name: this.series?.name ?? '', total: (this.series?.totalTomes ?? '') as number | '' }
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  async save(): Promise<void> {
    if (this.busy()) return
    const name = this.draft.name.trim()
    if (!name) { this.error.set('Le nom est obligatoire.'); return }
    const total = this.draft.total === '' || this.draft.total === null ? null : Number(this.draft.total)
    if (total !== null && (!Number.isInteger(total) || total < 1 || total > 999)) { this.error.set('Nombre de tomes : de 1 à 999.'); return }
    this.busy.set(true); this.error.set('')
    try {
      const request = { name, totalTomes: total }
      const detail = await firstValueFrom(this.series ? this.api.updateSeries(this.series.id, request) : this.api.createSeries(request))
      this.ref.close({ detail, message: this.series ? 'Série enregistrée' : 'Série ajoutée' })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async remove(): Promise<void> {
    if (!this.series || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.deleteSeries(this.series.id))
      this.ref.close({ detail: null, message: `Série « ${this.series.name} » supprimée ; ses livres restent` })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
