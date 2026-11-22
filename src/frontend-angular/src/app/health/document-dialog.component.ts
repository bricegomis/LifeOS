import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { DOCUMENT_TYPES, fileLabel, sizeLabel } from './document-rules'
import { HealthApiService } from './health-api.service'
import type { HealthDocumentDto, HealthDocumentType } from './health.models'

export interface DocumentDialogData { document: HealthDocumentDto | null, today: string, type?: HealthDocumentType }

const ACCEPTED = 'image/jpeg,image/png,image/webp,application/pdf'

/**
 * Creates or edits a health document (ADR 0018): type, title, date, doctor or laboratory, notes and
 * its files (photos taken or chosen, or PDF). Files are stored encrypted; nothing is read from them.
 */
@Component({
  selector: 'app-document-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="data.document ? 'Modifier le document' : 'Nouveau document'" formId="document-form" [busy]="busy()" [dirty]="dirty()"
      [error]="error()" [dangerLabel]="data.document ? 'Supprimer le document' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="document-form" class="lo-form" (ngSubmit)="save()" (input)="dirty.set(true)">
        <div class="lo-field-group">
          <span class="lo-field-label">Type</span>
          <div class="lo-chips" role="group" aria-label="Type de document">
            @for (t of types; track t.code) {
              <button type="button" class="lo-filter-chip" [class.is-on]="type === t.code" [attr.aria-pressed]="type === t.code" (click)="type = t.code; dirty.set(true)">
                @if (type === t.code) { <mat-icon svgIcon="check" /> }{{ t.label }}</button>
            }
          </div>
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Titre</mat-label>
          <input matInput name="title" required maxlength="200" placeholder="Ex. Bilan sanguin annuel" [(ngModel)]="title" /></mat-form-field>
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Date</mat-label>
            <input matInput type="date" name="day" required [max]="data.today" [(ngModel)]="day" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Médecin ou laboratoire</mat-label>
            <input matInput name="who" maxlength="200" [(ngModel)]="who" /></mat-form-field>
        </div>

        <div class="lo-field-group">
          <span class="lo-field-label">Fichiers</span>
          <div class="drop">
            <mat-icon svgIcon="upload_file" />
            <span>Ajoutez des photos du document ou un PDF. Ils sont chiffrés sur le serveur.</span>
            <div class="drop-actions">
              <label mat-stroked-button class="picker"><mat-icon svgIcon="photo_camera" />Prendre une photo
                <input class="sr-only" type="file" accept="image/*" capture="environment" (change)="pick($event)" /></label>
              <label mat-stroked-button class="picker"><mat-icon svgIcon="attach_file" />Choisir un fichier (photo ou PDF)
                <input class="sr-only" type="file" [accept]="accepted" multiple (change)="pick($event)" /></label>
            </div>
          </div>
          @if (kept().length || added().length) {
            <div class="lo-card files">
              @for (f of kept(); track f.id; let i = $index) {
                <div class="file"><mat-icon [svgIcon]="f.contentType === 'application/pdf' ? 'picture_as_pdf' : 'image'" /><span class="file-name">{{ label(f, i) }}</span>
                  <button mat-icon-button type="button" (click)="drop(f.id)" [attr.aria-label]="'Retirer ' + label(f, i)" title="Retirer"><mat-icon svgIcon="delete" /></button></div>
              }
              @for (f of added(); track $index) {
                <div class="file"><mat-icon [svgIcon]="f.type === 'application/pdf' ? 'picture_as_pdf' : 'image'" /><span class="file-name">{{ f.name }} · {{ size(f.size) }} · à envoyer</span>
                  <button mat-icon-button type="button" (click)="unpick($index)" [attr.aria-label]="'Retirer ' + f.name" title="Retirer"><mat-icon svgIcon="delete" /></button></div>
              }
            </div>
          }
        </div>
        <mat-form-field appearance="outline"><mat-label>Notes</mat-label>
          <textarea matInput name="notes" rows="3" maxlength="4000" [(ngModel)]="notes"></textarea></mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .drop { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 20px 16px; border: 2px dashed var(--mat-sys-outline-variant);
      border-radius: 16px; text-align: center; color: var(--mat-sys-on-surface-variant); }
    .drop-actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 8px; }
    .picker { cursor: pointer; }
    .files { margin-top: 8px; }
    .file { display: flex; align-items: center; gap: 12px; padding: 4px 4px 4px 16px; }
    .file + .file { border-top: 1px solid var(--mat-sys-outline-variant); }
    .file-name { flex: 1 1 auto; min-width: 0; overflow-wrap: anywhere; }
  `],
})
export class DocumentDialogComponent {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<DocumentDialogComponent, string>>(MatDialogRef)
  readonly data = inject<DocumentDialogData>(MAT_DIALOG_DATA)
  readonly types = DOCUMENT_TYPES
  readonly accepted = ACCEPTED
  readonly busy = signal(false)
  readonly error = signal('')
  readonly dirty = signal(false)
  readonly kept = signal(this.data.document?.files ?? [])
  readonly added = signal<File[]>([])
  readonly label = fileLabel
  readonly size = sizeLabel
  private created: HealthDocumentDto | null = null
  type: HealthDocumentType = this.data.document?.type ?? this.data.type ?? 'resultat'
  title = this.data.document?.title ?? ''
  day = this.data.document?.day ?? this.data.today
  who = this.data.document?.who ?? ''
  notes = this.data.document?.notes ?? ''

  pick(event: Event): void {
    const input = event.target as HTMLInputElement
    const files = [...(input.files ?? [])]
    input.value = ''
    const wrong = files.find(f => !ACCEPTED.split(',').includes(f.type) && !f.type.startsWith('image/'))
    if (wrong) { this.error.set(`« ${wrong.name} » n’est ni une photo ni un PDF.`); return }
    this.added.update(list => [...list, ...files])
    this.dirty.set(true)
  }

  unpick(index: number): void { this.added.update(list => list.filter((_, i) => i !== index)) }
  drop(fileId: string): void { this.kept.update(list => list.filter(f => f.id !== fileId)); this.dirty.set(true) }

  async save(): Promise<void> {
    if (this.busy()) return
    if (!this.title.trim()) { this.error.set('Le titre est obligatoire.'); return }
    this.busy.set(true); this.error.set('')
    const request = { type: this.type, title: this.title.trim(), day: this.day || null, who: this.who.trim() || null, notes: this.notes.trim() || null }
    try {
      // After a failed upload the document already exists: a second try updates it instead of creating another.
      const existing = this.data.document ?? this.created
      const doc = existing ? await firstValueFrom(this.api.updateDocument(existing.id, request)) : await firstValueFrom(this.api.createDocument(request))
      this.created = doc
      for (const f of doc.files.filter(f => !this.kept().some(k => k.id === f.id))) await firstValueFrom(this.api.removeDocumentFile(doc.id, f.id))
      for (const file of this.added()) {
        await firstValueFrom(this.api.addDocumentFile(doc.id, file))
        this.added.update(list => list.slice(1))
      }
      this.ref.close(this.data.document ? 'Document enregistré' : `created:${doc.id}`)
    } catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }

  async remove(): Promise<void> {
    if (!this.data.document) return
    this.busy.set(true)
    try { await firstValueFrom(this.api.removeDocument(this.data.document.id)); this.ref.close('Document supprimé') }
    catch (error) { this.error.set(apiErrorMessage(error)); this.busy.set(false) }
  }
}
