import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { HealthApiService } from './health-api.service'
import { num, parseNumber, rangeLabel } from './marker-rules'
import type { AnalysisDto, MarkerDto } from './health.models'

export interface AnalysisDialogData { analysis: AnalysisDto | null, markers: MarkerDto[], today: string, documentId?: string | null }

/**
 * Enters an analysis (ADR 0018): day, laboratory, notes and one value per marker measured; empty
 * fields are not measured. Claude can fill it from the document. Deleted from here.
 */
@Component({
  selector: 'app-analysis-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="data.analysis ? 'Modifier l’analyse' : 'Saisir une analyse'" formId="analysis-form" saveLabel="Enregistrer"
      [busy]="busy()" [dirty]="true" [error]="error()" [dangerLabel]="data.analysis ? 'Supprimer l’analyse' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="analysis-form" class="lo-form" (ngSubmit)="save()">
        <p class="lo-muted">Laisse vides les marqueurs non mesurés. Claude peut aussi lire le document et remplir ces valeurs.</p>
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Date du prélèvement</mat-label>
            <input matInput type="date" name="day" required [max]="data.today" [(ngModel)]="day" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Laboratoire</mat-label>
            <input matInput name="lab" maxlength="100" placeholder="Ex. Cerballiance" [(ngModel)]="laboratory" /></mat-form-field>
        </div>
        @for (m of markers; track m.id) {
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ m.name }} ({{ m.unit }})</mat-label>
            <input matInput [name]="'v' + m.id" inputmode="decimal" [(ngModel)]="values[m.id]" />
            <mat-hint>Labo {{ range(m.labMin, m.labMax, m.unit) }} · cible {{ range(m.targetMin, m.targetMax, m.unit) }}</mat-hint>
          </mat-form-field>
        } @empty {
          <p class="lo-notice">Crée d’abord tes marqueurs (onglet Marqueurs).</p>
        }
        <mat-form-field appearance="outline"><mat-label>Notes</mat-label><textarea matInput name="notes" rows="2" maxlength="2000" [(ngModel)]="notes"></textarea></mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class AnalysisDialogComponent {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<AnalysisDialogComponent, string>>(MatDialogRef)
  readonly data = inject<AnalysisDialogData>(MAT_DIALOG_DATA)
  readonly markers = this.data.markers.filter(m => !m.isArchived || this.data.analysis?.values.some(v => v.markerId === m.id))
  readonly range = rangeLabel
  readonly busy = signal(false)
  readonly error = signal('')
  day = this.data.analysis?.day ?? this.data.today
  laboratory = this.data.analysis?.laboratory ?? ''
  notes = this.data.analysis?.notes ?? ''
  readonly values: Record<string, string> = Object.fromEntries((this.data.analysis?.values ?? []).map(v => [v.markerId, num(v.value)]))

  async save(): Promise<void> {
    if (this.busy()) return
    const values = Object.entries(this.values).map(([markerId, raw]) => ({ markerId, value: parseNumber(raw) })).filter(v => v.value !== null)
    if (values.some(v => Number.isNaN(v.value))) { this.error.set('Les valeurs sont des nombres.'); return }
    if (!values.length) { this.error.set('Saisis au moins une valeur.'); return }
    const request = { day: this.day || null, laboratory: this.laboratory.trim() || null, notes: this.notes.trim() || null,
      documentId: this.data.analysis?.documentId ?? this.data.documentId ?? null, values: values.map(v => ({ markerId: v.markerId, value: v.value! })) }
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.data.analysis ? this.api.updateAnalysis(this.data.analysis.id, request) : this.api.createAnalysis(request))
      this.ref.close(`Analyse enregistrée : ${values.length} valeur${values.length > 1 ? 's' : ''}`)
    } catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }

  async remove(): Promise<void> {
    if (!this.data.analysis || this.busy()) return
    this.busy.set(true)
    try { await firstValueFrom(this.api.removeAnalysis(this.data.analysis.id)); this.ref.close('Analyse supprimée') }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
