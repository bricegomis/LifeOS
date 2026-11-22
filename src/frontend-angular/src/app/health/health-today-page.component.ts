import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatSnackBar } from '@angular/material/snack-bar'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { CheckinDialogComponent, type CheckinDialogData } from './checkin-dialog.component'
import { SymptomEntryDialogComponent, type SymptomEntryDialogData } from './symptom-entry-dialog.component'
import { checkinStatus } from './symptom-rules'
import { HealthApiService } from './health-api.service'
import type { MarkerDto, SupplementDto, SupplementListDto, SymptomJournalDto } from './health.models'
import { MarkerCardComponent } from './marker-card.component'
import { SupplementActionsService } from './supplement-actions.service'
import { SupplementCardComponent } from './supplement-card.component'
import { todayGroups } from './supplement-rules'

/** Home of the Health module (ADR 0018): today's supplements by moment, one tick each, and the daily check-in. */
@Component({
  selector: 'app-health-today-page',
  standalone: true,
  imports: [MarkerCardComponent, MatButtonModule, MatIconModule, RouterLink, SupplementCardComponent],
  template: `
    <div class="lo-page">
      <header class="lo-page-head">
        <div class="lo-page-title"><h1 class="lo-bar-title">Aujourd’hui</h1><p class="lo-intro">Ta santé, visible par toi seul.</p></div>
      </header>
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (list(); as l) {
        <section class="lo-section" aria-labelledby="supps">
          <div class="lo-section-head"><h2 id="supps">Compléments du jour</h2>
            <span class="lo-muted">{{ takenCount() }} / {{ activeCount() }} pris</span>
            <a mat-button routerLink="/health/supplements"><mat-icon svgIcon="tune" />Gérer</a></div>
          @for (group of groups(); track group.moment) {
            <h3 class="moment">{{ group.label }}</h3>
            <div class="cards">
              @for (s of group.items; track s.id) {
                <app-supplement-card [supplement]="s" [tick]="true" [busy]="pending() === s.id" (taken)="toggle($event)" />
              }
            </div>
          } @empty {
            <p class="lo-muted">{{ l.supplements.length ? 'Aucun complément à prendre aujourd’hui (cures en pause).' : 'Aucun complément. Ajoute-les dans Compléments.' }}</p>
          }
        </section>
      }
      @if (journal(); as j) {
        <section class="lo-section" aria-labelledby="checkin">
          <div class="lo-section-head"><h2 id="checkin">Bilan du jour</h2><a mat-button routerLink="/health/symptoms"><mat-icon svgIcon="history" />Journal</a></div>
          <p class="lo-notice">{{ status() }}</p>
          <div class="actions">
            <button mat-flat-button type="button" (click)="checkin(j)"><mat-icon svgIcon="checklist" />Faire le bilan du jour</button>
            <button mat-stroked-button type="button" [disabled]="busyCalm()" (click)="calm(j)"><mat-icon svgIcon="sentiment_satisfied" />Rien à signaler</button>
            <button mat-button type="button" (click)="note(j)"><mat-icon svgIcon="add" />Noter un symptôme</button>
          </div>
        </section>
      }
      @if (dueMarkers().length) {
        <section class="lo-section" aria-labelledby="markers">
          <div class="lo-section-head"><h2 id="markers">Marqueurs à faire ou refaire</h2><a mat-button routerLink="/health/analyses"><mat-icon svgIcon="biotech" />Analyses</a></div>
          <div class="cards">@for (m of dueMarkers(); track m.id) { <app-marker-card [marker]="m" /> }</div>
        </section>
      }
    </div>
  `,
  styles: [`
    .moment { margin: 4px 0 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-title-small); }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class HealthTodayPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly actions = inject(SupplementActionsService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly journal = signal<SymptomJournalDto | null>(null)
  readonly dueMarkers = signal<MarkerDto[]>([])
  readonly busyCalm = signal(false)
  readonly status = computed(() => this.journal() ? checkinStatus(this.journal()!) : '')
  readonly list = signal<SupplementListDto | null>(null)
  readonly error = signal('')
  readonly pending = signal('')
  readonly groups = computed(() => todayGroups(this.list()?.supplements ?? []))
  readonly activeCount = computed(() => this.groups().reduce((n, g) => n + g.items.length, 0))
  readonly takenCount = computed(() => this.groups().reduce((n, g) => n + g.items.filter(s => s.takenToday).length, 0))

  async ngOnInit(): Promise<void> { await this.load() }

  async toggle(supplement: SupplementDto): Promise<void> {
    this.pending.set(supplement.id)
    const updated = await this.actions.toggle(supplement)
    this.pending.set('')
    if (updated) this.list.update(l => l && { ...l, supplements: l.supplements.map(s => s.id === updated.id ? updated : s) })
  }

  async checkin(j: SymptomJournalDto): Promise<void> {
    const message = await firstValueFrom(openEditor<CheckinDialogComponent, CheckinDialogData, string>(this.dialog, CheckinDialogComponent, { types: j.types, today: j.today }).afterClosed())
    if (message) { this.snack.open(message, undefined, { duration: 4000 }); await this.loadJournal() }
  }

  async note(j: SymptomJournalDto): Promise<void> {
    const data: SymptomEntryDialogData = { entry: null, types: j.types, today: j.today }
    const message = await firstValueFrom(openEditor<SymptomEntryDialogComponent, SymptomEntryDialogData, string>(this.dialog, SymptomEntryDialogComponent, data).afterClosed())
    if (message) { this.snack.open(message, undefined, { duration: 4000 }); await this.loadJournal() }
  }

  async calm(j: SymptomJournalDto): Promise<void> {
    this.busyCalm.set(true)
    try {
      await firstValueFrom(this.api.checkin({ day: j.today, calm: true, symptoms: [] }))
      this.snack.open('Journée notée : rien à signaler', undefined, { duration: 4000 })
      await this.loadJournal()
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
    } finally {
      this.busyCalm.set(false)
    }
  }

  private async load(): Promise<void> {
    try { this.list.set(await firstValueFrom(this.api.supplements())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    await this.loadJournal()
    try { this.dueMarkers.set((await firstValueFrom(this.api.markers())).markers.filter(m => m.due)) } catch { /* the section stays hidden */ }
  }

  private async loadJournal(): Promise<void> {
    try { this.journal.set(await firstValueFrom(this.api.symptoms(1))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
