import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { HealthApiService } from './health-api.service'
import { measureTypeRequest, parseNumber } from './marker-rules'
import type { MeasureTypeDto } from './health.models'

export interface MeasureDialogData { types: MeasureTypeDto[], today: string, typeId?: string }

/** Notes a body measure (ADR 0018), or creates a new kind of measure with its unit and optional target. */
@Component({
  selector: 'app-measure-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame heading="Noter une mesure" formId="measure-form" saveLabel="Noter" [busy]="busy()" [dirty]="!!value" [error]="error()">
      <form id="measure-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-field-group">
          <span class="lo-field-label">Mesure</span>
          <div class="lo-chips" role="group" aria-label="Mesure">
            @for (t of types; track t.id) {
              <button type="button" class="lo-filter-chip" [class.is-on]="typeId === t.id" [attr.aria-pressed]="typeId === t.id" (click)="typeId = t.id">
                @if (typeId === t.id) { <mat-icon svgIcon="check" /> }{{ t.name }}</button>
            }
            <button type="button" class="lo-filter-chip" [class.is-on]="typeId === ''" [attr.aria-pressed]="typeId === ''" (click)="typeId = ''"><mat-icon svgIcon="add" />Nouvelle mesure</button>
          </div>
        </div>
        @if (typeId === '') {
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Nom</mat-label><input matInput name="name" maxlength="100" placeholder="Ex. Tension systolique" [(ngModel)]="name" /></mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Unité</mat-label><input matInput name="unit" maxlength="30" placeholder="Ex. mmHg" [(ngModel)]="unit" /></mat-form-field>
          </div>
          <div class="lo-field-group">
            <span class="lo-field-label">Ta cible, facultative</span>
            <div class="lo-form-row">
              <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Min</mat-label><input matInput name="targetMin" inputmode="decimal" [(ngModel)]="targetMin" /></mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Max</mat-label><input matInput name="targetMax" inputmode="decimal" [(ngModel)]="targetMax" /></mat-form-field>
            </div>
          </div>
        }
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Valeur{{ unitOf() ? ' (' + unitOf() + ')' : '' }}</mat-label>
            <input matInput name="value" required inputmode="decimal" [(ngModel)]="value" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Jour</mat-label>
            <input matInput type="date" name="day" required [max]="data.today" [(ngModel)]="day" /></mat-form-field>
        </div>
        <mat-form-field appearance="outline"><mat-label>Note</mat-label><input matInput name="note" maxlength="500" [(ngModel)]="note" /></mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class MeasureDialogComponent {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<MeasureDialogComponent, string>>(MatDialogRef)
  readonly data = inject<MeasureDialogData>(MAT_DIALOG_DATA)
  readonly types = this.data.types.filter(t => !t.isArchived)
  readonly busy = signal(false)
  readonly error = signal('')
  typeId = this.data.typeId ?? this.types[0]?.id ?? ''
  name = ''
  unit = ''
  targetMin = ''
  targetMax = ''
  value = ''
  day = this.data.today
  note = ''

  unitOf(): string { return this.typeId ? this.types.find(t => t.id === this.typeId)?.unit ?? '' : this.unit }

  async save(): Promise<void> {
    if (this.busy()) return
    const value = parseNumber(this.value)
    if (value === null || Number.isNaN(value)) { this.error.set('La valeur est un nombre.'); return }
    const type = this.typeId ? null : measureTypeRequest(this.name, this.unit, this.targetMin, this.targetMax)
    if (typeof type === 'string') { this.error.set(type); return }
    this.busy.set(true); this.error.set('')
    try {
      const typeId = this.typeId || (await firstValueFrom(this.api.createMeasureType(type!))).id
      await firstValueFrom(this.api.addMeasurement(typeId, value, this.day || null, this.note.trim() || null))
      this.ref.close('Mesure notée')
    } catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
