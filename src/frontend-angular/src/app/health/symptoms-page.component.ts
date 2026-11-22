import { BreakpointObserver } from '@angular/cdk/layout'
import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom, map } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HANDSET_QUERY } from '@/app/shared/ui/breakpoints'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { CheckinDialogComponent, type CheckinDialogData } from './checkin-dialog.component'
import { HealthApiService } from './health-api.service'
import type { SymptomEntryDto, SymptomJournalDto } from './health.models'
import { SymptomEntryDialogComponent, type SymptomEntryDialogData } from './symptom-entry-dialog.component'
import { SymptomListDialogComponent } from './symptom-list-dialog.component'
import { dayStrip, entryMeta, filteredDays, level, longDay } from './symptom-rules'

/**
 * The symptom journal (ADR 0018): the last 14 days at a glance, the daily check-in, the list of
 * symptoms and every entry by day; a filter by physical or mental.
 */
@Component({
  selector: 'app-symptoms-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="lo-page">
      <header class="lo-page-head" [class.is-phone]="handset()">
        <div class="lo-page-title"><h1 class="lo-bar-title">Symptômes</h1>
          <p class="lo-intro">Un journal pour garder une trace ; les liens avec les repas et le sport viendront avec le semainier.</p></div>
        @if (!handset()) { <div class="lo-actions"><button mat-flat-button type="button" (click)="note(null)"><mat-icon svgIcon="add" />Noter un symptôme</button></div> }
      </header>
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (journal(); as j) {
        <div class="actions">
          <button mat-stroked-button type="button" (click)="checkin()"><mat-icon svgIcon="checklist" />Bilan du jour</button>
          <button mat-button type="button" (click)="list()"><mat-icon svgIcon="tune" />Ma liste de symptômes</button>
        </div>
        <section class="lo-section" aria-labelledby="strip">
          <h2 id="strip">Les 14 derniers jours</h2>
          <div class="strip">
            @for (c of strip(); track c.day) {
              <div class="cell"><span class="lo-muted">{{ c.weekday }}</span><span class="dot" [attr.data-tone]="c.tone" role="img" [attr.aria-label]="c.label">{{ c.text }}</span></div>
            }
          </div>
          <p class="lo-help">Le chiffre est l’intensité la plus forte du jour ; ✓ = rien à signaler.</p>
        </section>
        <div class="lo-chips" role="group" aria-label="Filtrer">
          @for (k of kinds; track k.value) {
            <button type="button" class="lo-filter-chip" [class.is-on]="kind() === k.value" [attr.aria-pressed]="kind() === k.value" (click)="kind.set(k.value)">
              @if (kind() === k.value) { <mat-icon svgIcon="check" /> }{{ k.label }}</button>
          }
        </div>
        @for (d of days(); track d.day) {
          <section class="lo-section">
            <h2>{{ dayTitle(d.day) }}</h2>
            <div class="lo-card">
              @for (e of d.entries; track e.id) {
                <button type="button" class="entry" (click)="note(e)" [attr.aria-label]="'Modifier « ' + e.symptomName + ' »'">
                  <span class="level" [attr.data-tone]="tone(e.intensity)">{{ e.intensity }}</span>
                  <span class="entry-text"><strong>{{ e.symptomName }}</strong><span class="lo-muted">{{ meta(e) || (e.kind === 'mental' ? 'Mental' : 'Physique') }}</span></span>
                </button>
              }
            </div>
          </section>
        } @empty {
          <p class="lo-empty">Aucun symptôme noté sur les 30 derniers jours.</p>
        }
      }
      @if (handset()) {
        <button mat-fab type="button" class="lo-fab" (click)="note(null)" aria-label="Noter un symptôme" title="Noter un symptôme"><mat-icon svgIcon="add" /></button>
      }
    </div>
  `,
  styles: [`
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .strip { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 6px; max-width: 420px; }
    .cell { display: flex; flex-direction: column; align-items: center; gap: 4px; }
    .dot { display: flex; width: 36px; height: 36px; align-items: center; justify-content: center; border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 18px; font: var(--mat-sys-label-large); }
    [data-tone='calm'], [data-tone='light'] { border-color: transparent; background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    [data-tone='medium'] { border-color: transparent; background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    [data-tone='strong'] { border-color: transparent; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .entry { display: flex; width: 100%; align-items: center; gap: 12px; padding: 10px 16px; border: 0; background: transparent; color: inherit; text-align: left; cursor: pointer; }
    .entry + .entry { border-top: 1px solid var(--mat-sys-outline-variant); }
    .entry:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent); }
    .level { display: flex; width: 40px; height: 40px; flex: 0 0 auto; align-items: center; justify-content: center; border-radius: 12px; font: var(--mat-sys-title-medium); }
    .entry-text { display: flex; min-width: 0; flex-direction: column; }
  `],
})
export class SymptomsPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
  readonly kinds: { value: '' | 'physique' | 'mental', label: string }[] = [{ value: '', label: 'Tous' }, { value: 'physique', label: 'Physiques' }, { value: 'mental', label: 'Mentaux' }]
  readonly journal = signal<SymptomJournalDto | null>(null)
  readonly error = signal('')
  readonly kind = signal<'' | 'physique' | 'mental'>('')
  readonly strip = computed(() => this.journal() ? dayStrip(this.journal()!) : [])
  readonly days = computed(() => filteredDays(this.journal()?.days ?? [], this.kind()))
  readonly meta = entryMeta
  readonly tone = level

  async ngOnInit(): Promise<void> { await this.load() }

  dayTitle(day: string): string { return longDay(day, this.journal()?.today ?? day) }

  async note(entry: SymptomEntryDto | null): Promise<void> {
    const j = this.journal()
    if (!j) return
    const data: SymptomEntryDialogData = { entry, types: j.types, today: j.today }
    await this.afterClose(openEditor<SymptomEntryDialogComponent, SymptomEntryDialogData, string>(this.dialog, SymptomEntryDialogComponent, data).afterClosed())
  }

  async checkin(): Promise<void> {
    const j = this.journal()
    if (!j) return
    await this.afterClose(openEditor<CheckinDialogComponent, CheckinDialogData, string>(this.dialog, CheckinDialogComponent, { types: j.types, today: j.today }).afterClosed())
  }

  async list(): Promise<void> {
    const j = this.journal()
    if (!j) return
    await firstValueFrom(openEditor(this.dialog, SymptomListDialogComponent, j.types).afterClosed())
    await this.load()
  }

  private async afterClose(closed: import('rxjs').Observable<string | undefined>): Promise<void> {
    const message = await firstValueFrom(closed)
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.journal.set(await firstValueFrom(this.api.symptoms(30))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
