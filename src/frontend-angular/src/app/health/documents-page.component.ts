import { BreakpointObserver } from '@angular/cdk/layout'
import { Component, inject, OnInit, signal } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSnackBar } from '@angular/material/snack-bar'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom, map } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HANDSET_QUERY } from '@/app/shared/ui/breakpoints'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { DocumentDialogComponent, type DocumentDialogData } from './document-dialog.component'
import { DOCUMENT_TYPES, documentTypeLabel } from './document-rules'
import { HealthApiService } from './health-api.service'
import type { HealthDocumentDto, HealthDocumentType } from './health.models'
import { dateLabel } from './marker-rules'

/** Health documents (ADR 0018): prescriptions, reports, results and assessments, newest first. */
@Component({
  selector: 'app-documents-page',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, RouterLink],
  template: `
    <div class="lo-page">
      <header class="lo-page-head" [class.is-phone]="handset()">
        <div class="lo-page-title"><h1 class="lo-bar-title">Documents</h1>
          <p class="lo-intro">Ordonnances, comptes rendus, résultats et bilans. Visibles par vous seul, fichiers chiffrés.</p></div>
        @if (!handset()) { <div class="lo-actions"><button mat-flat-button type="button" (click)="add()"><mat-icon svgIcon="add" />Nouveau document</button></div> }
      </header>
      <div class="lo-filters">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search"><mat-label>Rechercher</mat-label>
          <mat-icon matPrefix svgIcon="search" /><input matInput name="q" [(ngModel)]="q" (ngModelChange)="load()" placeholder="Titre, médecin, notes" /></mat-form-field>
        <div class="lo-chips" role="group" aria-label="Type">
          <button type="button" class="lo-filter-chip" [class.is-on]="!type()" [attr.aria-pressed]="!type()" (click)="filter(null)">Tous</button>
          @for (t of types; track t.code) {
            <button type="button" class="lo-filter-chip" [class.is-on]="type() === t.code" [attr.aria-pressed]="type() === t.code" (click)="filter(t.code)">{{ t.label }}</button>
          }
        </div>
      </div>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (documents().length) {
        <div class="lo-list">
          @for (d of documents(); track d.id) {
            <div class="lo-row"><a class="lo-row-main" [routerLink]="['/health/document', d.id]">
              <span class="lo-lead-icon"><mat-icon svgIcon="description" /></span>
              <span class="lo-row-text"><span class="lo-row-title">{{ d.title }}</span>
                <span class="lo-row-sub">{{ typeLabel(d.type) }} · {{ date(d.day) }}{{ d.who ? ' · ' + d.who : '' }}</span></span>
              <span class="lo-muted count">{{ d.files.length }} fichier{{ d.files.length > 1 ? 's' : '' }}</span>
            </a></div>
          }
        </div>
      } @else if (loaded()) {
        <p class="lo-empty">{{ q || type() ? 'Aucun document ne correspond.' : 'Pas encore de document. Ajoutez une ordonnance, un compte rendu ou un résultat.' }}</p>
      }
      @if (handset()) {
        <button mat-fab type="button" class="lo-fab" (click)="add()" aria-label="Nouveau document" title="Nouveau document"><mat-icon svgIcon="add" /></button>
      }
    </div>
  `,
  styles: [`
    .search { flex: 1 1 260px; }
    .count { white-space: nowrap; }
    a.lo-row-main { text-decoration: none; }
  `],
})
export class DocumentsPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly router = inject(Router)
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
  readonly types = DOCUMENT_TYPES
  readonly documents = signal<HealthDocumentDto[]>([])
  readonly type = signal<HealthDocumentType | null>(null)
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly typeLabel = documentTypeLabel
  readonly date = dateLabel
  q = ''
  private loadId = 0

  async ngOnInit(): Promise<void> { await this.load() }

  async filter(type: HealthDocumentType | null): Promise<void> { this.type.set(type); await this.load() }

  async add(): Promise<void> {
    const data: DocumentDialogData = { document: null, today: new Date().toISOString().slice(0, 10), type: this.type() ?? undefined }
    const message = await firstValueFrom(openEditor<DocumentDialogComponent, DocumentDialogData, string>(this.dialog, DocumentDialogComponent, data).afterClosed())
    if (!message) return
    if (message.startsWith('created:')) { this.snack.open('Document ajouté', undefined, { duration: 3000 }); await this.router.navigate(['/health/document', message.slice(8)]); return }
    await this.load()
  }

  async load(): Promise<void> {
    const current = ++this.loadId
    try {
      const documents = await firstValueFrom(this.api.documents(this.type(), this.q))
      if (current !== this.loadId) return
      this.documents.set(documents); this.error.set('')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    this.loaded.set(true)
  }
}
