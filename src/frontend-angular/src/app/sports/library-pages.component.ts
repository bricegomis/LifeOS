import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import {
  ChallengeDialogComponent, GoalDialogComponent, type GoalDialogData, type LibraryDialogResult, TemplateDialogComponent, type TemplateDialogData,
} from './library-dialogs.component'
import { challengeUnitLabel, decimal, duration, integer } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { ChallengeReportDto, SportOption, SportTemplateDto, WeeklyGoalDto } from './sports.models'

/** Daily challenges (ADR 0013): one page, each challenge opens its window. */
@Component({
  selector: 'app-challenges-page',
  standalone: true,
  imports: [ListPageComponent, MatIconModule],
  template: `
    <app-list-page heading="Défis" intro="Un volume à atteindre chaque jour, par séries cumulées." addLabel="Ajouter un défi"
      archivedLabel="Afficher les arrêtés" [(query)]="query" [(showArchived)]="showStopped" (add)="open(null)">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (shown().length) {
          <div class="lo-list">
            @for (report of shown(); track report.challenge.id) {
              <div class="lo-row">
                <button type="button" class="lo-row-main" (click)="open(report)">
                  <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="local_fire_department" /></span>
                  <span class="lo-row-text"><span class="lo-row-title">{{ report.challenge.name }}</span>
                    <span class="lo-row-sub">{{ summary(report) }}</span></span>
                  @if (!report.challenge.isActive) { <span class="lo-tag">Arrêté</span> }
                </button>
              </div>
            }
          </div>
        } @else { <p class="lo-empty">{{ challenges().length ? 'Aucun résultat.' : 'Aucun défi. Ajoute par exemple « 100 pompes par jour ».' }}</p> }
      }
    </app-list-page>
  `,
})
export class ChallengesPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly query = signal('')
  readonly showStopped = signal(false)
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly challenges = signal<ChallengeReportDto[]>([])
  readonly shown = computed(() => this.challenges()
    .filter(report => (this.showStopped() || report.challenge.isActive) && matchesQuery(report.challenge.name, this.query())))

  async ngOnInit(): Promise<void> { await this.load() }

  summary(report: ChallengeReportDto): string {
    const jokers = report.challenge.jokersPerWeek
    return `${integer(report.challenge.dailyTarget)} ${challengeUnitLabel(report.challenge.unit)} par jour · ${jokers} joker${jokers > 1 ? 's' : ''} par semaine`
  }

  async open(report: ChallengeReportDto | null): Promise<void> {
    const result = await firstValueFrom(openEditor<ChallengeDialogComponent, unknown, LibraryDialogResult>(
      this.dialog, ChallengeDialogComponent, report?.challenge ?? null).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.challenges.set(await firstValueFrom(this.api.challenges(0, true))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }
}

/** Weekly goals (ADR 0013, ADR 0015): their target can be changed from the current week. */
@Component({
  selector: 'app-goals-page',
  standalone: true,
  imports: [ListPageComponent, MatIconModule],
  template: `
    <app-list-page heading="Objectifs" intro="Ce qui doit être fait dans la semaine ; seules les séances enregistrées comptent."
      addLabel="Ajouter un objectif" [(query)]="query" (add)="open(null)">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (shown().length) {
          <div class="lo-list">
            @for (goal of shown(); track goal.id) {
              <div class="lo-row">
                <button type="button" class="lo-row-main" (click)="open(goal)">
                  <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="flag" /></span>
                  <span class="lo-row-text"><span class="lo-row-title">{{ goal.label }}</span><span class="lo-row-sub">{{ goal.kindLabel }}</span></span>
                </button>
              </div>
            }
          </div>
        } @else { <p class="lo-empty">{{ goals().length ? 'Aucun résultat.' : 'Aucun objectif. Ajoute par exemple « 3 séances par semaine ».' }}</p> }
      }
    </app-list-page>
  `,
})
export class GoalsPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly query = signal('')
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly goals = signal<WeeklyGoalDto[]>([])
  readonly shown = computed(() => this.goals().filter(goal => matchesQuery(goal.label, this.query())))

  async ngOnInit(): Promise<void> { await this.load() }

  async open(goal: WeeklyGoalDto | null): Promise<void> {
    let data: GoalDialogData
    try {
      const [catalog, templates] = await Promise.all([firstValueFrom(this.api.catalog()), firstValueFrom(this.api.templates())])
      data = { goal, sports: catalog.sports, templates }
    } catch (error) { this.snack.open(apiErrorMessage(error), 'OK', { duration: 6000 }); return }
    const result = await firstValueFrom(openEditor<GoalDialogComponent, GoalDialogData, LibraryDialogResult>(this.dialog, GoalDialogComponent, data).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.goals.set(await firstValueFrom(this.api.goals())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }
}

/** Session templates (ADR 0013): created and edited on the phone as on the computer (ADR 0015). */
@Component({
  selector: 'app-templates-page',
  standalone: true,
  imports: [ListPageComponent, MatIconModule],
  template: `
    <app-list-page heading="Séances types" intro="Des séances prêtes à enregistrer en un geste." addLabel="Ajouter une séance type"
      archivedLabel="Afficher les archivées" [(query)]="query" [(showArchived)]="showArchived" (add)="open(null)">
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (loaded()) {
        @if (shown().length) {
          <div class="lo-list">
            @for (template of shown(); track template.id) {
              <div class="lo-row">
                <button type="button" class="lo-row-main" (click)="open(template)">
                  <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="directions_run" /></span>
                  <span class="lo-row-text"><span class="lo-row-title">{{ template.name }}</span><span class="lo-row-sub">{{ summary(template) }}</span></span>
                  @if (template.isArchived) { <span class="lo-tag">Archivée</span> }
                </button>
              </div>
            }
          </div>
        } @else { <p class="lo-empty">{{ templates().length ? 'Aucun résultat.' : 'Aucune séance type. Ajoute par exemple « Footing 45 min ».' }}</p> }
      }
    </app-list-page>
  `,
})
export class TemplatesPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly query = signal('')
  readonly showArchived = signal(false)
  readonly loaded = signal(false)
  readonly error = signal('')
  readonly templates = signal<SportTemplateDto[]>([])
  private sports: SportOption[] = []
  readonly shown = computed(() => this.templates()
    .filter(template => (this.showArchived() || !template.isArchived) && matchesQuery(template.name, this.query())))

  async ngOnInit(): Promise<void> {
    try { this.sports = (await firstValueFrom(this.api.catalog())).sports } catch { /* shown by load */ }
    await this.load()
  }

  summary(template: SportTemplateDto): string {
    const bits = [template.sportLabel, duration(template.durationMinutes)]
    if (template.distanceKm !== null) bits.push(`${decimal(template.distanceKm)} km`)
    bits.push(`${integer(template.calories)} kcal`)
    return bits.join(' · ')
  }

  async open(template: SportTemplateDto | null): Promise<void> {
    const result = await firstValueFrom(openEditor<TemplateDialogComponent, TemplateDialogData, LibraryDialogResult>(
      this.dialog, TemplateDialogComponent, { template, sports: this.sports }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.templates.set(await firstValueFrom(this.api.templates())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loaded.set(true) }
  }
}
