import { Component, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { AnalysisDialogComponent, type AnalysisDialogData } from './analysis-dialog.component'
import { HealthApiService } from './health-api.service'
import type { AnalysisDto } from './health.models'
import { dateLabel, STATUS, valueLabel } from './marker-rules'

/** One analysis (ADR 0018): its values, each placed against the personal target, and its document. */
@Component({
  selector: 'app-analysis-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page detail-page">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (analysis(); as a) {
        <header class="head">
          <a mat-icon-button routerLink="/health/analyses" aria-label="Retour aux analyses"><mat-icon svgIcon="arrow_back" /></a>
          <h1 class="lo-bar-title head-title">Analyse du {{ date(a.day) }}</h1>
          <button mat-icon-button type="button" (click)="edit(a)" aria-label="Modifier l’analyse" title="Modifier l’analyse"><mat-icon svgIcon="edit" /></button>
        </header>
        <p class="lo-muted">{{ a.laboratory || 'Laboratoire non renseigné' }}</p>
        @if (a.documentId) { <div><a mat-stroked-button [routerLink]="['/health/document', a.documentId]"><mat-icon svgIcon="description" />Voir le document</a></div> }
        <div class="lo-card">
          @for (v of a.values; track v.markerId) {
            <a class="row" [routerLink]="['/health/marker', v.markerId]"><span>{{ v.markerName }}</span>
              <span class="row-end"><strong>{{ valueLabel(v.value, v.unit) }}</strong><span [class]="'lo-tag ' + status[v.status].tag">{{ status[v.status].label }}</span></span></a>
          }
        </div>
        @if (a.notes) { <p class="notes">{{ a.notes }}</p> }
      } @else if (!error()) { <p class="lo-muted" role="status">Chargement…</p> }
    </div>
  `,
  styles: [`
    .detail-page { max-width: 880px; }
    .head { display: flex; align-items: center; gap: 8px; }
    .head-title { flex: 1 1 auto; margin: 0; }
    .row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 16px; color: inherit; text-decoration: none; }
    .row + .row { border-top: 1px solid var(--mat-sys-outline-variant); }
    .row-end { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: 8px; }
    .notes { margin: 0; white-space: pre-line; }
  `],
})
export class AnalysisPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly router = inject(Router)
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? ''
  readonly analysis = signal<AnalysisDto | null>(null)
  readonly error = signal('')
  readonly status = STATUS
  readonly date = dateLabel
  readonly valueLabel = valueLabel

  async ngOnInit(): Promise<void> { await this.load() }

  async edit(a: AnalysisDto): Promise<void> {
    const list = await firstValueFrom(this.api.markers(true))
    const data: AnalysisDialogData = { analysis: a, markers: list.markers, today: list.today }
    const message = await firstValueFrom(openEditor<AnalysisDialogComponent, AnalysisDialogData, string>(this.dialog, AnalysisDialogComponent, data).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    if (message === 'Analyse supprimée') { await this.router.navigateByUrl('/health/analyses'); return }
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.analysis.set(await firstValueFrom(this.api.analysis(this.id))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
