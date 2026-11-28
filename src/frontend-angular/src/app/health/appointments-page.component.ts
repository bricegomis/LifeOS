import { BreakpointObserver } from '@angular/cdk/layout'
import { NgTemplateOutlet } from '@angular/common'
import { Component, inject, OnInit, signal } from '@angular/core'
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
import { AppointmentDialogComponent, type AppointmentDialogData } from './appointment-dialog.component'
import { appointmentWhen, inDays } from './document-rules'
import { HealthApiService } from './health-api.service'
import type { AppointmentDto, AppointmentListDto } from './health.models'

/**
 * Medical appointments (ADR 0018): upcoming first, then past ones with their report. LifeOS never
 * connects to a booking site; the assistant can fill appointments from e-mails or the calendar.
 */
@Component({
  selector: 'app-appointments-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, NgTemplateOutlet, RouterLink],
  template: `
    <div class="lo-page">
      <header class="lo-page-head" [class.is-phone]="handset()">
        <div class="lo-page-title"><h1 class="lo-bar-title">Rendez-vous</h1>
          <p class="lo-intro">Tes rendez-vous médicaux. Claude peut les remplir depuis tes e-mails ou ton agenda.</p></div>
        @if (!handset()) { <div class="lo-actions"><button mat-flat-button type="button" (click)="edit(null)"><mat-icon svgIcon="add" />Nouveau rendez-vous</button></div> }
      </header>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (list(); as l) {
        <section class="lo-section">
          <h2>À venir</h2>
          @if (l.upcoming.length) {
            <div class="lo-list">@for (a of l.upcoming; track a.id) { <ng-container *ngTemplateOutlet="row; context: { $implicit: a, upcoming: true }" /> }</div>
          } @else { <p class="lo-muted">Aucun rendez-vous à venir.</p> }
        </section>
        @if (l.past.length) {
          <section class="lo-section">
            <h2>Passés</h2>
            <div class="lo-list">@for (a of l.past; track a.id) { <ng-container *ngTemplateOutlet="row; context: { $implicit: a, upcoming: false }" /> }</div>
          </section>
        }
      }
      <ng-template #row let-a let-upcoming="upcoming">
        <div class="lo-row">
          <button type="button" class="lo-row-main" (click)="edit(a)">
            <span class="lo-lead-icon"><mat-icon svgIcon="event" /></span>
            <span class="lo-row-text"><span class="lo-row-title">{{ a.who }}</span>
              <span class="lo-row-sub">{{ when(a) }}{{ upcoming ? ' · ' + soon(a) : '' }}</span>
              @if (a.reason || a.place) { <span class="lo-row-sub">{{ details(a) }}</span> }</span>
          </button>
          <span class="lo-row-action actions">
            @if (a.reportDocumentId) {
              <a mat-button [routerLink]="['/health/document', a.reportDocumentId]" [attr.title]="a.reportTitle"><mat-icon svgIcon="description" />Compte rendu</a>
            }
            @if (a.bookingUrl) {
              <a mat-stroked-button [href]="a.bookingUrl" target="_blank" rel="noopener noreferrer">
                <mat-icon svgIcon="open_in_new" />{{ upcoming ? 'Gérer' : 'Reprendre rendez-vous' }}</a>
            }
          </span>
        </div>
      </ng-template>
      @if (handset()) {
        <button mat-fab type="button" class="lo-fab" (click)="edit(null)" aria-label="Nouveau rendez-vous" title="Nouveau rendez-vous"><mat-icon svgIcon="add" /></button>
      }
    </div>
  `,
  styles: [`
    .actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 4px; }
  `],
})
export class AppointmentsPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
  readonly list = signal<AppointmentListDto | null>(null)
  readonly error = signal('')
  readonly when = appointmentWhen

  async ngOnInit(): Promise<void> { await this.load() }

  details(a: AppointmentDto): string { return [a.reason, a.place].filter(Boolean).join(' · ') }
  soon(a: AppointmentDto): string { return inDays(a.day, this.list()?.today ?? a.day) }

  async edit(a: AppointmentDto | null): Promise<void> {
    const data: AppointmentDialogData = { appointment: a, today: this.list()?.today ?? new Date().toISOString().slice(0, 10) }
    const message = await firstValueFrom(openEditor<AppointmentDialogComponent, AppointmentDialogData, string>(this.dialog, AppointmentDialogComponent, data).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.list.set(await firstValueFrom(this.api.appointments())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
