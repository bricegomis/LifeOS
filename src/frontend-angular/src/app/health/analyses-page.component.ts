import { BreakpointObserver } from '@angular/cdk/layout'
import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { RouterLink } from '@angular/router'
import { firstValueFrom, map } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HANDSET_QUERY } from '@/app/shared/ui/breakpoints'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { AnalysisDialogComponent, type AnalysisDialogData } from './analysis-dialog.component'
import { HealthApiService } from './health-api.service'
import type { AnalysisDto, MarkerListDto } from './health.models'
import { MarkerCardComponent } from './marker-card.component'
import { MarkerDialogComponent, type MarkerDialogData } from './marker-dialog.component'
import { dateLabel } from './marker-rules'

type Tab = 'markers' | 'analyses' | 'panels'

/** Analyses (ADR 0018): markers (to measure first), analyses by date, and markers grouped in panels. */
@Component({
  selector: 'app-analyses-page',
  standalone: true,
  imports: [MarkerCardComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="lo-page">
      <header class="lo-page-head" [class.is-phone]="handset()">
        <div class="lo-page-title"><h1 class="lo-bar-title">Analyses</h1><p class="lo-intro">Tes marqueurs, leur évolution et ce qu’il faut refaire.</p></div>
        @if (!handset()) { <div class="lo-actions"><button mat-flat-button type="button" (click)="addAnalysis()"><mat-icon svgIcon="add" />Saisir une analyse</button></div> }
      </header>
      <div class="tabs" role="tablist">
        @for (t of tabs; track t.value) {
          <button type="button" role="tab" class="tab" [class.is-on]="tab() === t.value" [attr.aria-selected]="tab() === t.value" (click)="tab.set(t.value)">{{ t.label }}</button>
        }
      </div>
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (list(); as l) {
        @switch (tab()) {
          @case ('markers') {
            <div><button mat-stroked-button type="button" (click)="addMarker()"><mat-icon svgIcon="add" />Nouveau marqueur</button></div>
            @if (due().length) {
              <section class="lo-section"><div class="lo-section-head"><h2>À faire ou refaire</h2><span class="lo-muted">{{ due().length }}</span></div>
                <div class="cards">@for (m of due(); track m.id) { <app-marker-card [marker]="m" /> }</div></section>
            }
            <section class="lo-section"><div class="lo-section-head"><h2>À jour</h2><span class="lo-muted">{{ upToDate().length }}</span></div>
              @if (upToDate().length) { <div class="cards">@for (m of upToDate(); track m.id) { <app-marker-card [marker]="m" /> }</div> }
              @else if (!l.markers.length) { <p class="lo-empty">Aucun marqueur. Ajoute par exemple « Ferritine », en µg/L.</p> }</section>
          }
          @case ('analyses') {
            <p class="lo-notice">Le plus simple : enregistre le PDF ou la photo dans Documents, puis demande à Claude « saisis ma dernière prise de sang ».
              Il lit le document et propose les valeurs, que tu confirmes.</p>
            <div class="lo-card">
              @for (a of analyses(); track a.id) {
                <a class="row" [routerLink]="['/health/analysis', a.id]">
                  <span class="row-text"><strong>Analyse du {{ date(a.day) }}</strong>
                    <span class="lo-muted">{{ [a.laboratory, a.values.length + ' marqueur' + (a.values.length > 1 ? 's' : '')].filter(x => x).join(' · ') }}</span></span>
                  <span class="lo-tag" [class.is-ok]="!outOfTarget(a)" [class.is-warn]="outOfTarget(a)">{{ outOfTarget(a) ? outOfTarget(a) + ' hors de ta cible' : 'Tout dans ta cible' }}</span>
                </a>
              } @empty { <p class="lo-empty">Aucune analyse.</p> }
            </div>
          }
          @case ('panels') {
            <p class="lo-muted">Des groupes de marqueurs, pour savoir quoi demander au médecin.</p>
            @for (p of l.panels; track p) {
              <section class="lo-section"><div class="lo-section-head"><h2>{{ p }}</h2><span class="lo-muted">{{ inPanel(p).length }} marqueurs</span></div>
                <div class="cards">@for (m of inPanel(p); track m.id) { <app-marker-card [marker]="m" /> }</div></section>
            } @empty { <p class="lo-empty">Aucun bilan : ajoute-les dans la fenêtre d’un marqueur.</p> }
          }
        }
      }
      @if (handset()) {
        <button mat-fab type="button" class="lo-fab" (click)="addAnalysis()" aria-label="Saisir une analyse" title="Saisir une analyse"><mat-icon svgIcon="add" /></button>
      }
    </div>
  `,
  styles: [`
    .tabs { display: flex; gap: 4px; overflow-x: auto; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .tab { min-height: 48px; padding: 0 16px; border: 0; border-bottom: 3px solid transparent; background: transparent; color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-title-small); cursor: pointer; }
    .tab.is-on { border-bottom-color: var(--mat-sys-primary); color: var(--mat-sys-primary); }
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    .row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 16px; color: inherit; text-decoration: none; }
    .row + .row { border-top: 1px solid var(--mat-sys-outline-variant); }
    .row:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .row-text { display: flex; min-width: 0; flex-direction: column; }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class AnalysesPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
  readonly tabs: { value: Tab, label: string }[] = [{ value: 'markers', label: 'Marqueurs' }, { value: 'analyses', label: 'Analyses' }, { value: 'panels', label: 'Bilans' }]
  readonly tab = signal<Tab>('markers')
  readonly list = signal<MarkerListDto | null>(null)
  readonly analyses = signal<AnalysisDto[]>([])
  readonly error = signal('')
  readonly due = computed(() => this.list()?.markers.filter(m => m.due) ?? [])
  readonly upToDate = computed(() => this.list()?.markers.filter(m => !m.due) ?? [])
  readonly date = dateLabel

  async ngOnInit(): Promise<void> { await this.load() }

  inPanel(panel: string) { return this.list()?.markers.filter(m => m.panels.includes(panel)) ?? [] }
  outOfTarget(a: AnalysisDto): number { return a.values.filter(v => v.status !== 'in_target').length }

  async addMarker(): Promise<void> {
    const data: MarkerDialogData = { marker: null, panels: this.list()?.panels ?? [] }
    await this.after(openEditor<MarkerDialogComponent, MarkerDialogData, string>(this.dialog, MarkerDialogComponent, data).afterClosed())
  }

  async addAnalysis(): Promise<void> {
    const l = this.list()
    if (!l) return
    const data: AnalysisDialogData = { analysis: null, markers: l.markers, today: l.today }
    await this.after(openEditor<AnalysisDialogComponent, AnalysisDialogData, string>(this.dialog, AnalysisDialogComponent, data).afterClosed())
  }

  private async after(closed: import('rxjs').Observable<string | undefined>): Promise<void> {
    const message = await firstValueFrom(closed)
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try {
      const [list, analyses] = await Promise.all([firstValueFrom(this.api.markers()), firstValueFrom(this.api.analyses())])
      this.list.set(list); this.analyses.set(analyses); this.error.set('')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
