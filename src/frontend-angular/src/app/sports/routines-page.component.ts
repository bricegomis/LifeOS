import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { Router } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { StepperComponent } from '@/app/shared/ui/stepper.component'
import { RoutineDialogComponent, type RoutineDialogData, type RoutineDialogResult } from './routine-dialog.component'
import { openPass, routineSummary } from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { RoutineDto, RoutineReportDto, SportSettingsDto } from './sports.models'

/**
 * Routines of the profile (ADR 0015): a tap opens the routine's window, « Faire » starts or continues
 * today's pass. Below, the effort and discomfort offered at the end of a pass.
 */
@Component({
  selector: 'app-routines-page',
  standalone: true,
  imports: [ListPageComponent, MatButtonModule, MatIconModule, StepperComponent],
  template: `
    <app-list-page heading="Routines" intro="Des exercices à faire certains jours, avec leur cible." addLabel="Ajouter une routine"
      archivedLabel="Afficher les arrêtées" [(query)]="query" [(showArchived)]="showStopped" (add)="open(null)">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (shown().length) {
          <div class="lo-list">
            @for (report of shown(); track report.routine.id) {
              <div class="lo-row">
                <button type="button" class="lo-row-main" (click)="open(report.routine)">
                  <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="event_repeat" /></span>
                  <span class="lo-row-text"><span class="lo-row-title">{{ report.routine.name }}</span>
                    <span class="lo-row-sub">{{ summary(report.routine) }}</span></span>
                  @if (!report.routine.isActive) { <span class="lo-tag">Arrêtée</span> }
                </button>
                @if (report.routine.isActive && report.plannedToday) {
                  <span class="lo-row-action">
                    <button mat-flat-button type="button" class="tonal" (click)="go(report)" [attr.aria-label]="(openPass(report) ? 'Continuer' : 'Faire') + ' la routine ' + report.routine.name">
                      <mat-icon svgIcon="play_arrow" />{{ openPass(report) ? 'Continuer' : 'Faire' }}</button>
                  </span>
                }
              </div>
            }
          </div>
        } @else {
          <p class="lo-empty">{{ routines().length ? 'Aucun résultat.' : 'Aucune routine. Créez vos exercices, puis une routine quotidienne.' }}</p>
        }
        @if (settings(); as current) {
          <section class="lo-card defaults" aria-labelledby="feeling-defaults">
            <h2 id="feeling-defaults">Ressenti proposé à la fin d’un passage</h2>
            <div class="defaults-grid">
              <app-stepper label="Effort" hint="1 facile · 10 maximal" [min]="1" [max]="10" [disabled]="savingSettings()"
                [value]="current.defaultEffort" (valueChange)="saveSettings($event, current.defaultDiscomfort)" />
              <app-stepper label="Gêne" hint="0 = aucune gêne" [min]="0" [max]="10" [disabled]="savingSettings()"
                [value]="current.defaultDiscomfort" (valueChange)="saveSettings(current.defaultEffort, $event)" />
            </div>
          </section>
        }
      }
    </app-list-page>
  `,
  styles: [`
    .tonal { --mat-button-filled-container-color: var(--mat-sys-secondary-container); --mat-button-filled-label-text-color: var(--mat-sys-on-secondary-container); }
    .defaults { display: flex; max-width: 480px; flex-direction: column; gap: 12px; padding: 16px; }
    .defaults h2 { margin: 0; font: var(--mat-sys-title-medium); font-weight: 600; }
    .defaults-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
  `],
})
export class RoutinesPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly router = inject(Router)
  readonly openPass = openPass
  readonly query = signal('')
  readonly showStopped = signal(false)
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly routines = signal<RoutineReportDto[]>([])
  readonly settings = signal<SportSettingsDto | null>(null)
  readonly savingSettings = signal(false)
  readonly shown = computed(() => this.routines()
    .filter(report => (this.showStopped() || report.routine.isActive) && matchesQuery(report.routine.name, this.query())))

  async ngOnInit(): Promise<void> {
    await this.load()
    try { this.settings.set(await firstValueFrom(this.api.settings())) } catch { /* the steppers stay hidden */ }
  }

  summary(routine: RoutineDto): string { return routineSummary(routine) }

  async go(report: RoutineReportDto): Promise<void> {
    try {
      if (!openPass(report)) await firstValueFrom(this.api.startSession(report.routine.id))
      await this.router.navigate(['/sports/routine', report.routine.id])
    } catch (error) { this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 }) }
  }

  async open(routine: RoutineDto | null): Promise<void> {
    let exercises
    try { exercises = await firstValueFrom(this.api.exercises(true)) }
    catch (error) { this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 }); return }
    const result = await firstValueFrom(openEditor<RoutineDialogComponent, RoutineDialogData, RoutineDialogResult>(
      this.dialog, RoutineDialogComponent, { routine, exercises }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  async saveSettings(effort: number, discomfort: number): Promise<void> {
    this.savingSettings.set(true)
    try { this.settings.set(await firstValueFrom(this.api.saveSettings(effort, discomfort))) }
    catch (error) { this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 }) }
    finally { this.savingSettings.set(false) }
  }

  private async load(): Promise<void> {
    try { this.routines.set(await firstValueFrom(this.api.routines(0, true))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }
}
