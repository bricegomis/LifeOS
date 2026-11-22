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
import type { SymptomTypeDto } from './health.models'

export interface CheckinDialogData { types: SymptomTypeDto[], today: string }

/** The daily check-in (ADR 0018): "rien à signaler", or the symptoms felt with their intensity. */
@Component({
  selector: 'app-checkin-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame heading="Bilan du jour" formId="checkin-form" saveLabel="Valider" [busy]="busy()" [dirty]="picked().length > 0" [error]="error()">
      <form id="checkin-form" class="lo-form" (ngSubmit)="save()">
        <p class="lo-muted">Coche ce que tu as ressenti, avec son intensité. Une journée sans rien compte aussi.</p>
        <mat-button-toggle-group name="calm" aria-label="Aujourd’hui" [(ngModel)]="calm" hideSingleSelectionIndicator="false">
          <mat-button-toggle [value]="true">Rien à signaler</mat-button-toggle>
          <mat-button-toggle [value]="false">J’ai des symptômes</mat-button-toggle>
        </mat-button-toggle-group>
        @if (!calm) {
          <div class="lo-chips" role="group" aria-label="Symptômes">
            @for (t of types; track t.id) {
              <button type="button" class="lo-filter-chip" [class.is-on]="isPicked(t.id)" [attr.aria-pressed]="isPicked(t.id)" (click)="flip(t.id)">
                @if (isPicked(t.id)) { <mat-icon svgIcon="check" /> }{{ t.name }}</button>
            }
          </div>
          @for (p of picked(); track p.id) {
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="intensity">
              <mat-label>Intensité : {{ nameOf(p.id) }}</mat-label>
              <input matInput type="number" min="1" max="10" step="1" [name]="'i' + p.id" [(ngModel)]="p.intensity" />
            </mat-form-field>
          }
        }
      </form>
    </app-editor-frame>
  `,
  styles: [`.intensity { max-width: 320px; }`],
})
export class CheckinDialogComponent {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<CheckinDialogComponent, string>>(MatDialogRef)
  private readonly data = inject<CheckinDialogData>(MAT_DIALOG_DATA)
  readonly types = this.data.types.filter(t => !t.isArchived)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly picked = signal<{ id: string, intensity: number }[]>([])
  calm = false

  isPicked(id: string): boolean { return this.picked().some(p => p.id === id) }
  nameOf(id: string): string { return this.types.find(t => t.id === id)?.name ?? '' }
  flip(id: string): void { this.picked.update(list => this.isPicked(id) ? list.filter(p => p.id !== id) : [...list, { id, intensity: 5 }]) }

  async save(): Promise<void> {
    if (this.busy()) return
    if (!this.calm && !this.picked().length) { this.error.set('Coche au moins un symptôme, ou « rien à signaler ».'); return }
    if (this.picked().some(p => !Number.isInteger(Number(p.intensity)) || p.intensity < 1 || p.intensity > 10)) { this.error.set('Intensité : de 1 à 10.'); return }
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.checkin({ day: this.data.today, calm: this.calm, symptoms: this.calm ? [] : this.picked().map(p => ({ symptomTypeId: p.id, intensity: Number(p.intensity) })) }))
      this.ref.close(this.calm ? 'Journée notée : rien à signaler' : 'Bilan du jour enregistré')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
