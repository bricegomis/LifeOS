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
import { RoutineDialogComponent, type RoutineDialogData, type RoutineDialogResult } from './routine-dialog.component'
import { openPass, routineSummary } from './routine-format'
import { SportsApiService } from './sports-api.service'
import type { RoutineDto, RoutineReportDto } from './sports.models'

/**
 * Routines of the profile (ADR 0015): a tap opens the routine's window, « Faire » starts or continues
 * today's pass.
 */
@Component({
  selector: 'app-routines-page',
  standalone: true,
  imports: [ListPageComponent, MatButtonModule, MatIconModule],
  template: `
    <app-list-page heading="Routines" intro="Des exercices à faire certains jours, avec leur cible." addLabel="Ajouter une routine"
      [(query)]="query" (add)="open(null)">
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
                </button>
                @if (report.plannedToday) {
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
      }
    </app-list-page>
  `,
  styles: [`
    .tonal { --mat-button-filled-container-color: var(--mat-sys-secondary-container); --mat-button-filled-label-text-color: var(--mat-sys-on-secondary-container); }
  `],
})
export class RoutinesPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  private readonly router = inject(Router)
  readonly openPass = openPass
  readonly query = signal('')
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly routines = signal<RoutineReportDto[]>([])
  readonly shown = computed(() => this.routines()
    .filter(report => matchesQuery(report.routine.name, this.query())))

  async ngOnInit(): Promise<void> {
    await this.load()
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

  private async load(): Promise<void> {
    try { this.routines.set(await firstValueFrom(this.api.routines(0))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }
}
