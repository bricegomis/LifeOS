import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { HealthApiService } from './health-api.service'
import { level } from './symptom-rules'
import type { SymptomEntryDto, SymptomTypeDto } from './health.models'

export interface SymptomEntryDialogData { entry: SymptomEntryDto | null, types: SymptomTypeDto[], today: string }

/** Notes a symptom at any time (ADR 0018): which one, intensity 1-10, day, optional time and note; removed from here. */
@Component({
  selector: 'app-symptom-entry-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame [heading]="data.entry ? 'Modifier le symptôme' : 'Noter un symptôme'" formId="symptom-form" [saveLabel]="data.entry ? 'Enregistrer' : 'Noter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="data.entry ? 'Supprimer' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="symptom-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Symptôme</mat-label>
          <mat-select name="type" required [(ngModel)]="draft.symptomTypeId">
            @for (t of types; track t.id) { <mat-option [value]="t.id">{{ t.name }}{{ t.kind === 'mental' ? ' (mental)' : '' }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Intensité (1 = léger, 10 = très fort)</span>
          <div class="lo-chips" role="group" aria-label="Intensité">
            @for (n of levels; track n) {
              <button type="button" class="lo-filter-chip level" [class.is-on]="draft.intensity === n" [attr.data-level]="levelOf(n)" [attr.aria-pressed]="draft.intensity === n"
                (click)="draft.intensity = n">{{ n }}</button>
            }
          </div>
        </div>
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Jour</mat-label>
            <input matInput type="date" name="day" required [max]="data.today" [(ngModel)]="draft.day" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Heure (facultative)</mat-label>
            <input matInput type="time" name="time" [(ngModel)]="draft.time" />
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Note</mat-label>
          <textarea matInput name="note" rows="2" maxlength="1000" placeholder="Ex. après le déjeuner" [(ngModel)]="draft.note"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .level { min-width: 40px; justify-content: center; }
    .level.is-on[data-level='light'] { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); border-color: transparent; }
    .level.is-on[data-level='medium'] { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); border-color: transparent; }
    .level.is-on[data-level='strong'] { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); border-color: transparent; }
  `],
})
export class SymptomEntryDialogComponent {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<SymptomEntryDialogComponent, string>>(MatDialogRef)
  readonly data = inject<SymptomEntryDialogData>(MAT_DIALOG_DATA)
  readonly types = this.data.types.filter(t => !t.isArchived || t.id === this.data.entry?.symptomTypeId)
  readonly levels = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
  readonly levelOf = level
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = this.data.entry
    ? { symptomTypeId: this.data.entry.symptomTypeId, intensity: this.data.entry.intensity, day: this.data.entry.day, time: this.data.entry.time?.slice(0, 5) ?? '', note: this.data.entry.note ?? '' }
    : { symptomTypeId: this.types[0]?.id ?? '', intensity: 5, day: this.data.today, time: '', note: '' }
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  async save(): Promise<void> {
    if (this.busy()) return
    if (!this.draft.symptomTypeId) { this.error.set('Choisis le symptôme.'); return }
    const request = { symptomTypeId: this.draft.symptomTypeId, day: this.draft.day || null, time: this.draft.time ? `${this.draft.time}:00` : null,
      intensity: this.draft.intensity, note: this.draft.note.trim() || null }
    this.busy.set(true); this.error.set('')
    try {
      const entry = await firstValueFrom(this.data.entry ? this.api.updateSymptom(this.data.entry.id, request) : this.api.addSymptom(request))
      this.ref.close(this.data.entry ? 'Symptôme modifié' : `${entry.symptomName} noté, ${entry.intensity}/10`)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async remove(): Promise<void> {
    if (!this.data.entry || this.busy()) return
    this.busy.set(true)
    try { await firstValueFrom(this.api.removeSymptom(this.data.entry.id)); this.ref.close('Symptôme supprimé') }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
