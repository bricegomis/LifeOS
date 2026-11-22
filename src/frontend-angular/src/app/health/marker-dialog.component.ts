import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { HealthApiService } from './health-api.service'
import { markerDraftOf, markerRequest } from './marker-rules'
import type { MarkerDto } from './health.models'

export interface MarkerDialogData { marker: MarkerDto | null, panels: string[] }

/** Creates or edits a marker (ADR 0018): unit, lab range, personal target, frequency and panels; archived from here. */
@Component({
  selector: 'app-marker-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="data.marker ? 'Modifier le marqueur' : 'Nouveau marqueur'" formId="marker-form" [saveLabel]="data.marker ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="data.marker && !data.marker.isArchived ? 'Archiver' : ''" (danger)="archive()">
      <form id="marker-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-form-row">
          <mat-form-field appearance="outline"><mat-label>Nom</mat-label><input matInput name="name" required maxlength="100" placeholder="Ex. Ferritine" [(ngModel)]="draft.name" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Unité</mat-label><input matInput name="unit" required maxlength="30" placeholder="Ex. µg/L" [(ngModel)]="draft.unit" /></mat-form-field>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Plage du labo (recopiée du compte rendu ; une borne peut rester vide)</span>
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Min</mat-label><input matInput name="labMin" inputmode="decimal" [(ngModel)]="draft.labMin" /></mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Max</mat-label><input matInput name="labMax" inputmode="decimal" [(ngModel)]="draft.labMax" /></mat-form-field>
          </div>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Ta cible, souvent plus étroite pour être en très bonne forme</span>
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Min</mat-label><input matInput name="targetMin" inputmode="decimal" [(ngModel)]="draft.targetMin" /></mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Max</mat-label><input matInput name="targetMax" inputmode="decimal" [(ngModel)]="draft.targetMax" /></mat-form-field>
          </div>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">À refaire</span>
          <div class="lo-chips" role="group" aria-label="À refaire">
            @for (f of frequencies; track f.value) {
              <button type="button" class="lo-filter-chip" [class.is-on]="draft.frequencyMonths === f.value" [attr.aria-pressed]="draft.frequencyMonths === f.value"
                (click)="draft.frequencyMonths = f.value">@if (draft.frequencyMonths === f.value) { <mat-icon svgIcon="check" /> }{{ f.label }}</button>
            }
          </div>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Bilans</span>
          <div class="lo-chips" role="group" aria-label="Bilans">
            @for (p of choices(); track p) {
              <button type="button" class="lo-filter-chip" [class.is-on]="hasPanel(p)" [attr.aria-pressed]="hasPanel(p)" (click)="flip(p)">
                @if (hasPanel(p)) { <mat-icon svgIcon="check" /> }{{ p }}</button>
            }
          </div>
          <div class="panel-add">
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Nouveau bilan</mat-label>
              <input matInput name="newPanel" maxlength="60" placeholder="Ex. Bilan thyroïde" [(ngModel)]="newPanel" (keydown.enter)="addPanel($event)" /></mat-form-field>
            <button mat-stroked-button type="button" (click)="addPanel()">Ajouter</button>
          </div>
        </div>
      </form>
    </app-editor-frame>
  `,
  styles: [`.panel-add { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-top: 8px; } .panel-add mat-form-field { flex: 1 1 200px; max-width: 320px; }`],
})
export class MarkerDialogComponent {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<MarkerDialogComponent, string>>(MatDialogRef)
  readonly data = inject<MarkerDialogData>(MAT_DIALOG_DATA)
  readonly frequencies = [{ value: 0, label: 'Une fois' }, { value: 3, label: 'Tous les 3 mois' }, { value: 6, label: 'Tous les 6 mois' },
    { value: 12, label: 'Tous les ans' }, { value: 24, label: 'Tous les 2 ans' }]
  readonly busy = signal(false)
  readonly error = signal('')
  readonly draft = markerDraftOf(this.data.marker)
  readonly choices = signal([...new Set([...this.data.panels, ...this.draft.panels])])
  newPanel = ''
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }
  hasPanel(p: string): boolean { return this.draft.panels.includes(p) }
  flip(p: string): void { this.draft.panels = this.hasPanel(p) ? this.draft.panels.filter(x => x !== p) : [...this.draft.panels, p] }

  addPanel(event?: Event): void {
    event?.preventDefault()
    const name = this.newPanel.trim()
    if (!name) return
    if (!this.choices().includes(name)) this.choices.update(c => [...c, name])
    if (!this.hasPanel(name)) this.draft.panels = [...this.draft.panels, name]
    this.newPanel = ''
  }

  async save(): Promise<void> {
    if (this.busy()) return
    if (this.newPanel.trim()) this.addPanel()
    const request = markerRequest(this.draft)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.data.marker ? this.api.updateMarker(this.data.marker.id, request) : this.api.createMarker(request))
      this.ref.close(this.data.marker ? 'Marqueur enregistré' : 'Marqueur ajouté')
    } catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }

  async archive(): Promise<void> {
    if (!this.data.marker || this.busy()) return
    this.busy.set(true)
    try { await firstValueFrom(this.api.archiveMarker(this.data.marker.id)); this.ref.close('Marqueur archivé ; ses valeurs restent') }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
