import { BreakpointObserver } from '@angular/cdk/layout'
import { Component, inject, OnInit, signal } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom, map } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HANDSET_QUERY } from '@/app/shared/ui/breakpoints'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { HealthApiService } from './health-api.service'
import type { MeasureTypeDto, MeasurementDto } from './health.models'
import { MeasureDialogComponent, type MeasureDialogData } from './measure-dialog.component'
import { dateLabel, MEASURE_STATUS, num, rangeLabel } from './marker-rules'
import { MeasureTypeDialogComponent } from './measure-type-dialog.component'
import { ConfirmService } from '@/app/shared/ui/confirm'

/** Body measures (ADR 0018): each kind with its last value, its change, its optional target and its evolution. */
@Component({
  selector: 'app-measures-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="lo-page">
      <header class="lo-page-head" [class.is-phone]="handset()">
        <div class="lo-page-title"><h1 class="lo-bar-title">Mesures</h1><p class="lo-intro">Poids, tour de taille, fréquence cardiaque… dans le temps.</p></div>
        @if (!handset()) { <div class="lo-actions"><button mat-flat-button type="button" (click)="add(null)"><mat-icon svgIcon="add" />Noter une mesure</button></div> }
      </header>
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @for (t of active(); track t.id) {
        <section class="lo-section">
          <div class="lo-section-head"><h2>{{ t.name }}</h2>
            <span class="lo-muted">{{ summary(t) }}</span>
            @if (t.status) { <span [class]="'lo-tag ' + measureStatus[t.status].tag">{{ measureStatus[t.status].label }}</span> }
            <span class="head-actions">
              <button mat-button type="button" (click)="add(t.id)"><mat-icon svgIcon="add" />Noter</button>
              <button mat-icon-button type="button" (click)="edit(t)" [attr.aria-label]="'Modifier « ' + t.name + ' »'" title="Modifier la mesure et sa cible"><mat-icon svgIcon="edit" /></button>
            </span></div>
          <p class="lo-muted target">{{ targetText(t) }}</p>
          @if (t.values.length) {
            <div class="lo-card chart" role="img" [attr.aria-label]="t.name + ' dans le temps'">
              @for (v of recent(t); track v.id) {
                <div class="col"><span class="col-value">{{ num(v.value) }}</span><span class="col-bar" [attr.data-status]="v.status" [style.height.px]="height(t, v)"></span>
                  <span class="lo-muted col-label">{{ short(v.day) }}</span></div>
              }
            </div>
            <details class="values"><summary>Toutes les valeurs ({{ t.values.length }})</summary>
              <div class="lo-card">
                @for (v of reversed(t); track v.id) {
                  <div class="row"><span>{{ date(v.day) }}{{ v.note ? ' · ' + v.note : '' }}</span><span class="row-end"><strong>{{ num(v.value) }} {{ t.unit }}</strong>
                    <button mat-icon-button type="button" (click)="remove(v)" [attr.aria-label]="'Supprimer la mesure du ' + date(v.day)" title="Supprimer"><mat-icon svgIcon="delete" /></button></span></div>
                }
              </div>
            </details>
          } @else { <p class="lo-muted">Pas encore de valeur.</p> }
        </section>
      }
      @if (archived().length) {
        <p class="lo-muted">Mesures archivées :
          @for (t of archived(); track t.id) { <button mat-button type="button" (click)="archive(t)">Rétablir « {{ t.name }} »</button> }</p>
      }
      @if (handset()) {
        <button mat-fab type="button" class="lo-fab" (click)="add(null)" aria-label="Noter une mesure" title="Noter une mesure"><mat-icon svgIcon="add" /></button>
      }
    </div>
  `,
  styles: [`
    .head-actions { display: flex; gap: 4px; margin-left: auto; }
    .chart { display: flex; align-items: flex-end; gap: 8px; height: 150px; padding: 12px 12px 0; overflow-x: auto; }
    .col { display: flex; min-width: 44px; flex: 1 1 0; flex-direction: column; align-items: center; justify-content: flex-end; gap: 4px; height: 100%; }
    .col-value { font: var(--mat-sys-label-medium); }
    .col-bar { width: 100%; max-width: 48px; border-radius: 6px 6px 0 0; background: var(--mat-sys-secondary); }
    .col-bar[data-status='in_target'] { background: var(--mat-sys-primary); }
    .col-bar[data-status='below_target'], .col-bar[data-status='above_target'] { background: var(--mat-sys-tertiary); }
    .target { margin: 0; }
    .col-label { padding-bottom: 6px; white-space: nowrap; }
    .values summary { cursor: pointer; color: var(--mat-sys-primary); font: var(--mat-sys-label-large); padding: 8px 0; }
    .row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 4px 4px 4px 16px; }
    .row + .row { border-top: 1px solid var(--mat-sys-outline-variant); }
    .row-end { display: flex; align-items: center; gap: 4px; }
  `],
})
export class MeasuresPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly confirm = inject(ConfirmService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
  readonly types = signal<MeasureTypeDto[]>([])
  readonly error = signal('')
  readonly num = num
  readonly date = dateLabel
  readonly measureStatus = MEASURE_STATUS

  async ngOnInit(): Promise<void> { await this.load() }

  active(): MeasureTypeDto[] { return this.types().filter(t => !t.isArchived) }
  archived(): MeasureTypeDto[] { return this.types().filter(t => t.isArchived) }
  recent(t: MeasureTypeDto): MeasurementDto[] { return t.values.slice(-12) }
  reversed(t: MeasureTypeDto): MeasurementDto[] { return [...t.values].reverse() }
  short(day: string): string { return this.date(day).replace(/ \d{4}$/, '') }

  summary(t: MeasureTypeDto): string {
    const last = t.values.at(-1)
    if (!last) return '—'
    const prev = t.values.at(-2)
    const diff = prev ? last.value - prev.value : null
    return `${num(last.value)} ${t.unit}${diff !== null ? ` (${diff > 0 ? '+' : ''}${num(diff)})` : ''}`
  }

  targetText(t: MeasureTypeDto): string {
    return t.targetMin === null && t.targetMax === null ? 'Sans cible' : `Ta cible : ${rangeLabel(t.targetMin, t.targetMax, t.unit)}`
  }

  height(t: MeasureTypeDto, v: MeasurementDto): number {
    const values = this.recent(t).map(x => x.value)
    const [min, max] = [Math.min(...values), Math.max(...values)]
    return Math.round(30 + (max === min ? 40 : (v.value - min) / (max - min) * 66))
  }

  async add(typeId: string | null): Promise<void> {
    const data: MeasureDialogData = { types: this.types(), today: new Date().toISOString().slice(0, 10), typeId: typeId ?? undefined }
    const message = await firstValueFrom(openEditor<MeasureDialogComponent, MeasureDialogData, string>(this.dialog, MeasureDialogComponent, data).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  async edit(t: MeasureTypeDto): Promise<void> {
    const message = await firstValueFrom(openEditor<MeasureTypeDialogComponent, MeasureTypeDto, string>(this.dialog, MeasureTypeDialogComponent, t).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  async archive(t: MeasureTypeDto): Promise<void> {
    try { await firstValueFrom(this.api.archiveMeasureType(t.id, t.isArchived)); await this.load() }
    catch (error) { this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 }) }
  }

  async remove(v: MeasurementDto): Promise<void> {
    if (!await this.confirm.ask({ message: 'Supprimer cette mesure ?', confirmLabel: 'Supprimer', danger: true })) return
    try { await firstValueFrom(this.api.removeMeasurement(v.id)); this.snack.open('Mesure supprimée', undefined, { duration: 3000 }); await this.load() }
    catch (error) { this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 }) }
  }

  private async load(): Promise<void> {
    try { this.types.set(await firstValueFrom(this.api.measures())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
