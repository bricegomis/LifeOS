import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatSlideToggleModule } from '@angular/material/slide-toggle'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { HealthApiService } from './health-api.service'
import { measureTypeRequest, num } from './marker-rules'
import type { MeasureTypeDto } from './health.models'

/** Edits a kind of body measure (ADR 0018): name, unit, optional personal target and Garmin link; archived from here. */
@Component({
  selector: 'app-measure-type-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule, MatSlideToggleModule],
  template: `
    <app-editor-frame heading="Modifier la mesure" formId="measure-type-form" saveLabel="Enregistrer" [busy]="busy()" [dirty]="dirty()" [error]="error()"
      dangerLabel="Archiver" (danger)="archive()">
      <form id="measure-type-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-form-row">
          <mat-form-field appearance="outline"><mat-label>Nom</mat-label><input matInput name="name" required maxlength="100" [(ngModel)]="name" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Unité</mat-label><input matInput name="unit" required maxlength="30" [(ngModel)]="unit" /></mat-form-field>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Ta cible, facultative (une borne peut rester vide ; aucune pour ce qui ne change pas, comme la taille)</span>
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Min</mat-label><input matInput name="targetMin" inputmode="decimal" [(ngModel)]="targetMin" /></mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Max</mat-label><input matInput name="targetMax" inputmode="decimal" [(ngModel)]="targetMax" /></mat-form-field>
          </div>
        </div>
        <mat-slide-toggle name="garmin" [(ngModel)]="garmin">Reprendre la FC au repos de la Garmin</mat-slide-toggle>
        <p class="lo-muted">Chaque jour synchronisé s'ajoute aux valeurs ; une valeur notée à la main le même jour l'emporte.</p>
      </form>
    </app-editor-frame>
  `,
})
export class MeasureTypeDialogComponent {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<MeasureTypeDialogComponent, string>>(MatDialogRef)
  readonly type = inject<MeasureTypeDto>(MAT_DIALOG_DATA)
  readonly busy = signal(false)
  readonly error = signal('')
  name = this.type.name
  unit = this.type.unit
  targetMin = this.type.targetMin === null ? '' : num(this.type.targetMin)
  targetMax = this.type.targetMax === null ? '' : num(this.type.targetMax)
  garmin = this.type.source === 'garmin_resting_heart_rate'
  private readonly initial = this.snapshot()

  dirty(): boolean { return this.snapshot() !== this.initial }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = measureTypeRequest(this.name, this.unit, this.targetMin, this.targetMax, this.garmin ? 'garmin_resting_heart_rate' : null)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try { await firstValueFrom(this.api.updateMeasureType(this.type.id, request)); this.ref.close('Mesure enregistrée') }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }

  async archive(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true)
    try { await firstValueFrom(this.api.archiveMeasureType(this.type.id)); this.ref.close('Mesure archivée ; ses valeurs restent') }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }

  private snapshot(): string { return JSON.stringify([this.name, this.unit, this.targetMin, this.targetMax, this.garmin]) }
}
