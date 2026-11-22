import { Component, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { appointmentDraftOf, appointmentRequest, documentTypeLabel } from './document-rules'
import { HealthApiService } from './health-api.service'
import type { AppointmentDto, HealthDocumentDto } from './health.models'
import { dateLabel } from './marker-rules'

export interface AppointmentDialogData { appointment: AppointmentDto | null, today: string }

/** Creates or edits a medical appointment (ADR 0018), with its booking link and, afterwards, its report. */
@Component({
  selector: 'app-appointment-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame [heading]="data.appointment ? 'Modifier le rendez-vous' : 'Nouveau rendez-vous'" formId="appointment-form" [busy]="busy()" [dirty]="dirty()"
      [error]="error()" [dangerLabel]="data.appointment ? 'Supprimer le rendez-vous' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="appointment-form" class="lo-form" (ngSubmit)="save()" (input)="dirty.set(true)">
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Médecin ou spécialité</mat-label>
          <input matInput name="who" required maxlength="200" placeholder="Ex. Dr Martin, dermatologue" [(ngModel)]="draft.who" /></mat-form-field>
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Jour</mat-label>
            <input matInput type="date" name="day" required [(ngModel)]="draft.day" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Heure</mat-label>
            <input matInput type="time" name="time" [(ngModel)]="draft.time" /></mat-form-field>
        </div>
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Motif</mat-label>
            <input matInput name="reason" maxlength="300" [(ngModel)]="draft.reason" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Lieu</mat-label>
            <input matInput name="place" maxlength="300" [(ngModel)]="draft.place" /></mat-form-field>
        </div>
        <mat-form-field appearance="outline"><mat-label>Lien de prise de rendez-vous</mat-label>
          <input matInput name="bookingUrl" type="url" inputmode="url" maxlength="1000" placeholder="https://www.doctolib.fr/…" [(ngModel)]="draft.bookingUrl" />
          <mat-hint>Doctolib ou un autre site : un bouton l’ouvrira pour reprendre rendez-vous.</mat-hint></mat-form-field>
        @if (documents().length) {
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Compte rendu</mat-label>
            <mat-select name="report" [(ngModel)]="draft.reportDocumentId" (selectionChange)="dirty.set(true)">
              <mat-option value="">Aucun</mat-option>
              @for (d of documents(); track d.id) { <mat-option [value]="d.id">{{ d.title }} · {{ typeLabel(d.type) }} · {{ date(d.day) }}</mat-option> }
            </mat-select></mat-form-field>
        }
        <mat-form-field appearance="outline"><mat-label>Notes</mat-label>
          <textarea matInput name="notes" rows="3" maxlength="2000" [(ngModel)]="draft.notes"></textarea></mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class AppointmentDialogComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<AppointmentDialogComponent, string>>(MatDialogRef)
  readonly data = inject<AppointmentDialogData>(MAT_DIALOG_DATA)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly dirty = signal(false)
  readonly documents = signal<HealthDocumentDto[]>([])
  readonly typeLabel = documentTypeLabel
  readonly date = dateLabel
  readonly draft = appointmentDraftOf(this.data.appointment, this.data.today)

  async ngOnInit(): Promise<void> {
    try { this.documents.set(await firstValueFrom(this.api.documents())) } catch { /* the report stays optional */ }
  }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = appointmentRequest(this.draft)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      if (this.data.appointment) await firstValueFrom(this.api.updateAppointment(this.data.appointment.id, request))
      else await firstValueFrom(this.api.createAppointment(request))
      this.ref.close(this.data.appointment ? 'Rendez-vous enregistré' : 'Rendez-vous ajouté')
    } catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }

  async remove(): Promise<void> {
    if (!this.data.appointment) return
    this.busy.set(true)
    try { await firstValueFrom(this.api.removeAppointment(this.data.appointment.id)); this.ref.close('Rendez-vous supprimé') }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
