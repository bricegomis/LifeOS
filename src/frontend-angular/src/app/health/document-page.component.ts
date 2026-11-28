import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { AnalysisDialogComponent, type AnalysisDialogData } from './analysis-dialog.component'
import { DocumentDialogComponent, type DocumentDialogData } from './document-dialog.component'
import { documentTypeLabel, fileLabel } from './document-rules'
import { HealthApiService } from './health-api.service'
import type { AnalysisDto, HealthDocumentDto, HealthDocumentFileDto } from './health.models'
import { dateLabel } from './marker-rules'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

/**
 * One health document (ADR 0018): its files (photos shown, PDF opened in a new tab, both decrypted by
 * the API) and, for a result, the analyses entered from it, by hand or by the assistant.
 */
@Component({
  selector: 'app-document-page',
  standalone: true,
  imports: [SkeletonComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page detail-page">
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (doc(); as d) {
        <header class="head">
          <a mat-icon-button class="lo-back-link" routerLink="/health/documents" aria-label="Retour aux documents"><mat-icon svgIcon="arrow_back" /></a>
          <h1 class="lo-bar-title head-title">{{ d.title }}</h1>
          <button mat-icon-button type="button" (click)="edit(d)" aria-label="Modifier le document" title="Modifier le document"><mat-icon svgIcon="edit" /></button>
        </header>
        <p class="lo-muted">{{ typeLabel(d.type) }} · {{ date(d.day) }}{{ d.who ? ' · ' + d.who : '' }}</p>
        @if (d.notes) { <p class="notes">{{ d.notes }}</p> }

        <section class="lo-panel">
          <h2>Fichiers</h2>
          @if (d.files.length) {
            <div class="files">
              @for (f of d.files; track f.id; let i = $index) {
                @if (f.contentType === 'application/pdf') {
                  <button mat-stroked-button type="button" class="pdf" (click)="open(d, f)"><mat-icon svgIcon="picture_as_pdf" />{{ label(f, i) }}<mat-icon svgIcon="open_in_new" /></button>
                } @else {
                  <button type="button" class="photo" (click)="open(d, f)" [attr.aria-label]="'Ouvrir ' + label(f, i)">
                    @if (previews()[f.id]; as src) { <img [src]="src" [alt]="label(f, i)" /> } @else { <span class="lo-muted">Chargement…</span> }
                  </button>
                }
              }
            </div>
          } @else { <p class="lo-muted">Pas de fichier. Ajoute une photo ou un PDF depuis « Modifier ».</p> }
        </section>

        @if (d.type === 'resultat' || d.type === 'bilan' || analyses().length) {
          <section class="lo-panel">
            <div class="lo-section-head"><h2>Valeurs saisies</h2>
              <button mat-button type="button" (click)="enter(d)"><mat-icon svgIcon="edit" />Saisir à la main</button></div>
            @for (a of analyses(); track a.id) {
              <a class="analysis" [routerLink]="['/health/analysis', a.id]"><mat-icon svgIcon="biotech" />
                Analyse du {{ date(a.day) }} · {{ a.values.length }} valeur{{ a.values.length > 1 ? 's' : '' }}<mat-icon svgIcon="chevron_right" /></a>
            }
            @if (!analyses().length) {
              <p class="lo-notice">Demande à Claude : « lis le document « {{ d.title }} » et saisis les valeurs ». Il lit les fichiers et propose les marqueurs ; LifeOS ne lit rien lui-même.</p>
            }
          </section>
        }
      } @else if (!error()) { <app-skeleton variant="page" /> }
    </div>
  `,
  styles: [`
    .detail-page { max-width: 880px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .notes { margin: 0; white-space: pre-line; }
    .files { display: flex; flex-wrap: wrap; gap: 12px; }
    .photo { display: flex; width: 160px; height: 200px; align-items: center; justify-content: center; overflow: hidden; padding: 0; border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px; background: var(--mat-sys-surface-container); cursor: zoom-in; }
    .photo img { width: 100%; height: 100%; object-fit: cover; }
    .pdf { height: auto; min-height: 48px; }
    .analysis { display: flex; align-items: center; gap: 12px; padding: 10px 4px; color: inherit; text-decoration: none; }
    .analysis + .analysis { border-top: 1px solid var(--mat-sys-outline-variant); }
    .analysis mat-icon:last-child { margin-left: auto; }
  `],
})
export class DocumentPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly router = inject(Router)
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? ''
  readonly doc = signal<HealthDocumentDto | null>(null)
  readonly analyses = signal<AnalysisDto[]>([])
  readonly previews = signal<Record<string, string>>({})
  readonly error = signal('')
  readonly typeLabel = documentTypeLabel
  readonly label = fileLabel
  readonly date = dateLabel

  async ngOnInit(): Promise<void> { await this.load() }
  ngOnDestroy(): void { this.clearPreviews() }

  async open(d: HealthDocumentDto, f: HealthDocumentFileDto): Promise<void> {
    // Opened before the download so that Safari keeps it as a user action.
    const tab = globalThis.open('', '_blank')
    try {
      const url = URL.createObjectURL(await firstValueFrom(this.api.documentFile(d.id, f.id)))
      if (tab) tab.location.href = url; else globalThis.location.assign(url)
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (error) { tab?.close(); this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 }) }
  }

  async edit(d: HealthDocumentDto): Promise<void> {
    const data: DocumentDialogData = { document: d, today: new Date().toISOString().slice(0, 10) }
    const message = await firstValueFrom(openEditor<DocumentDialogComponent, DocumentDialogData, string>(this.dialog, DocumentDialogComponent, data).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    if (message === 'Document supprimé') { await this.router.navigateByUrl('/health/documents'); return }
    await this.load()
  }

  async enter(d: HealthDocumentDto): Promise<void> {
    const list = await firstValueFrom(this.api.markers())
    const data: AnalysisDialogData = { analysis: null, markers: list.markers, today: list.today, documentId: d.id }
    const message = await firstValueFrom(openEditor<AnalysisDialogComponent, AnalysisDialogData, string>(this.dialog, AnalysisDialogComponent, data).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try {
      const d = await firstValueFrom(this.api.document(this.id))
      this.doc.set(d); this.error.set('')
      this.analyses.set(await Promise.all(d.analysisIds.map(a => firstValueFrom(this.api.analysis(a)))))
      this.clearPreviews()
      const previews: Record<string, string> = {}
      for (const f of d.files.filter(f => f.contentType !== 'application/pdf'))
        previews[f.id] = URL.createObjectURL(await firstValueFrom(this.api.documentFile(d.id, f.id)))
      this.previews.set(previews)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
  }

  private clearPreviews(): void { Object.values(this.previews()).forEach(url => URL.revokeObjectURL(url)); this.previews.set({}) }
}
