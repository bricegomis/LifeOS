import { Component, computed, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { SportsApiService } from './sports-api.service'
import type {
  DailyChallengeDto, DailyChallengeRequest, GoalKind, SportOption, SportTemplateDto, SportTemplateRequest, WeeklyGoalDto, WeeklyGoalRequest,
} from './sports.models'

/** What every library window returns: a message for the snack bar. */
export interface LibraryDialogResult { message: string }

/** Save and danger actions shared by the small library windows. */
abstract class LibraryDialog {
  protected readonly api = inject(SportsApiService)
  private readonly ref = inject<MatDialogRef<unknown, LibraryDialogResult>>(MatDialogRef)
  readonly busy = signal(false)
  readonly error = signal('')

  protected async run(call: () => Observable<unknown>, message: string): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(call())
      this.ref.close({ message })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}

const IMPORTS = [EditorFrameComponent, FormsModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule]

/** A daily challenge (ADR 0013): a target changed applies from today; its unit no longer changes. */
@Component({
  selector: 'app-challenge-dialog',
  standalone: true,
  imports: IMPORTS,
  template: `
    <app-editor-frame [heading]="challenge ? 'Modifier le défi' : 'Nouveau défi'" formId="challenge-form" [saveLabel]="challenge ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="challenge?.isActive ? 'Arrêter' : ''" (danger)="stop()">
      <form id="challenge-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" required maxlength="100" placeholder="Ex. Pompes" [(ngModel)]="draft.name" />
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Unité</span>
          @if (challenge) { <span>{{ draft.unit === 'reps' ? 'Répétitions' : 'Minutes' }}</span> } @else {
            <mat-button-toggle-group name="unit" aria-label="Unité du défi" [(ngModel)]="draft.unit">
              <mat-button-toggle value="reps">Répétitions</mat-button-toggle>
              <mat-button-toggle value="minutes">Minutes</mat-button-toggle>
            </mat-button-toggle-group>
          }
        </div>
        <mat-form-field appearance="outline" class="narrow">
          <mat-label>Cible par jour</mat-label>
          <input matInput type="number" name="target" required min="1" max="100000" inputmode="numeric" [(ngModel)]="draft.dailyTarget" />
          @if (challenge) { <mat-hint>Une cible modifiée vaut à partir d’aujourd’hui.</mat-hint> }
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Jokers par semaine</span>
          <mat-button-toggle-group name="jokers" aria-label="Jokers par semaine" [(ngModel)]="draft.jokersPerWeek">
            @for (count of [0, 1, 2]; track count) { <mat-button-toggle [value]="count">{{ count }}</mat-button-toggle> }
          </mat-button-toggle-group>
          <p class="lo-help">Un joker couvre un jour manqué sans casser la série.</p>
        </div>
      </form>
    </app-editor-frame>
  `,
  styles: [`.narrow { max-width: 240px; }`],
})
export class ChallengeDialogComponent extends LibraryDialog {
  readonly challenge = inject<DailyChallengeDto | null>(MAT_DIALOG_DATA)
  readonly draft: DailyChallengeRequest = this.challenge
    ? { name: this.challenge.name, unit: this.challenge.unit, dailyTarget: this.challenge.dailyTarget, jokersPerWeek: this.challenge.jokersPerWeek }
    : { name: '', unit: 'reps', dailyTarget: 100, jokersPerWeek: 1 }
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  save(): Promise<void> {
    const name = this.draft.name.trim(), target = Number(this.draft.dailyTarget)
    if (!name) { this.error.set('Donnez un nom au défi.'); return Promise.resolve() }
    if (!Number.isInteger(target) || target < 1) { this.error.set('La cible est un nombre entier d’au moins 1.'); return Promise.resolve() }
    const request = { ...this.draft, name, dailyTarget: target }
    return this.run(() => this.challenge ? this.api.updateChallenge(this.challenge.id, request) : this.api.createChallenge(request),
      this.challenge ? 'Défi enregistré' : 'Défi ajouté : il compte dès aujourd’hui')
  }

  stop(): Promise<void> {
    if (!this.challenge) return Promise.resolve()
    return this.run(() => this.api.stopChallenge(this.challenge!.id), `« ${this.challenge.name} » arrêté ; son historique est conservé`)
  }
}

export interface GoalDialogData { goal: WeeklyGoalDto | null, sports: SportOption[], templates: SportTemplateDto[] }

export const GOAL_KINDS: { id: GoalKind, label: string }[] = [
  { id: 'template', label: 'Séance type' }, { id: 'sport', label: 'Sport' }, { id: 'sessions', label: 'Nombre de séances' }, { id: 'volume', label: 'Volume' },
]

/** A weekly goal (ADR 0013, ADR 0015): its target changes from the current week; its form never changes. */
@Component({
  selector: 'app-goal-dialog',
  standalone: true,
  imports: IMPORTS,
  template: `
    <app-editor-frame [heading]="goal ? 'Modifier l’objectif' : 'Nouvel objectif'" formId="goal-form" [saveLabel]="goal ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="goal ? 'Arrêter' : ''" (danger)="stop()">
      <form id="goal-form" class="lo-form" (ngSubmit)="save()">
        @if (goal) {
          <div class="lo-field-group"><span class="lo-field-label">Objectif</span><span>{{ goal.label }}</span>
            <p class="lo-help">La forme d’un objectif ne change pas : pour une autre forme, arrêtez-le et créez-en un.</p></div>
        } @else {
          <div class="lo-field-group" role="group" aria-label="Forme de l’objectif">
            <span class="lo-field-label">Forme</span>
            <div class="lo-chips">
              @for (kind of kinds; track kind.id) {
                <button type="button" class="lo-chip" [class.is-on]="draft.kind === kind.id" [attr.aria-pressed]="draft.kind === kind.id"
                  (click)="draft.kind = kind.id">@if (draft.kind === kind.id) { <mat-icon svgIcon="check" /> }{{ kind.label }}</button>
              }
            </div>
          </div>
          @if (draft.kind === 'template') {
            <mat-form-field appearance="outline">
              <mat-label>Séance type</mat-label>
              <mat-select name="template" required [(ngModel)]="draft.templateId">
                @for (template of activeTemplates(); track template.id) { <mat-option [value]="template.id">{{ template.name }}</mat-option> }
              </mat-select>
            </mat-form-field>
          }
          @if (draft.kind === 'sport') {
            <mat-form-field appearance="outline">
              <mat-label>Sport</mat-label>
              <mat-select name="sport" [(ngModel)]="draft.sport">
                @for (sport of data.sports; track sport.code) { <mat-option [value]="sport.code">{{ sport.label }}</mat-option> }
              </mat-select>
            </mat-form-field>
          }
          @if (draft.kind === 'volume') {
            <mat-form-field appearance="outline">
              <mat-label>Unité</mat-label>
              <mat-select name="unit" [(ngModel)]="draft.unit">
                <mat-option value="kcal">Calories (kcal)</mat-option><mat-option value="km">Distance (km)</mat-option><mat-option value="hours">Durée (heures)</mat-option>
              </mat-select>
            </mat-form-field>
          }
        }
        <mat-form-field appearance="outline" class="narrow">
          <mat-label>{{ draft.kind === 'volume' ? 'Cible par semaine' : 'Séances par semaine' }}</mat-label>
          <input matInput type="number" name="target" required [min]="draft.kind === 'volume' ? 0.5 : 1" [step]="draft.kind === 'volume' ? 'any' : 1"
            [(ngModel)]="draft.target" />
          @if (goal) { <mat-hint>Le changement vaut à partir de cette semaine ; les semaines passées gardent l’objectif d’alors.</mat-hint> }
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`.narrow { max-width: 320px; }`],
})
export class GoalDialogComponent extends LibraryDialog {
  readonly data = inject<GoalDialogData>(MAT_DIALOG_DATA)
  readonly goal = this.data.goal
  readonly kinds = GOAL_KINDS
  readonly activeTemplates = computed(() => this.data.templates.filter(template => !template.isArchived))
  readonly draft: WeeklyGoalRequest = this.goal
    ? { kind: this.goal.kind, templateId: this.goal.templateId, sport: this.goal.sport, unit: this.goal.unit, target: this.goal.target }
    : { kind: 'sessions', templateId: this.activeTemplates()[0]?.id ?? null, sport: 'running', unit: 'kcal', target: 3 }
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }

  save(): Promise<void> {
    const d = this.draft, target = Number(d.target)
    if (!(target > 0)) { this.error.set('Donnez une cible.'); return Promise.resolve() }
    if (d.kind === 'template' && !d.templateId) { this.error.set('Choisissez une séance type.'); return Promise.resolve() }
    const request: WeeklyGoalRequest = {
      kind: d.kind, target, templateId: d.kind === 'template' ? d.templateId : null,
      sport: d.kind === 'sport' ? d.sport : null, unit: d.kind === 'volume' ? d.unit : null,
    }
    return this.run(() => this.goal ? this.api.updateGoal(this.goal.id, request) : this.api.createGoal(request),
      this.goal ? 'Objectif modifié à partir de cette semaine' : 'Objectif ajouté à partir de cette semaine')
  }

  stop(): Promise<void> {
    if (!this.goal) return Promise.resolve()
    return this.run(() => this.api.stopGoal(this.goal!.id), `« ${this.goal.label} » arrêté ; il compte encore cette semaine`)
  }
}

export interface TemplateDialogData { template: SportTemplateDto | null, sports: SportOption[] }

/** A session template (ADR 0013): editing it never rewrites the sessions already logged. */
@Component({
  selector: 'app-template-dialog',
  standalone: true,
  imports: IMPORTS,
  template: `
    <app-editor-frame [heading]="template ? 'Modifier la séance type' : 'Nouvelle séance type'" formId="template-form" [saveLabel]="template ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="template && !template.isArchived ? 'Archiver' : ''" (danger)="archive()">
      <form id="template-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" required maxlength="200" placeholder="Ex. Footing 45 min" [(ngModel)]="draft.name" />
        </mat-form-field>
        <div class="lo-form-row">
          <mat-form-field appearance="outline">
            <mat-label>Sport</mat-label>
            <mat-select name="sport" [(ngModel)]="draft.sport">
              @for (sport of data.sports; track sport.code) { <mat-option [value]="sport.code">{{ sport.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Durée (min)</mat-label>
            <input matInput type="number" name="duration" required min="1" max="1440" inputmode="numeric" [(ngModel)]="draft.durationMinutes" />
          </mat-form-field>
        </div>
        <div class="lo-form-row">
          <mat-form-field appearance="outline">
            <mat-label>Calories proposées (kcal)</mat-label>
            <input matInput type="number" name="calories" required min="0" max="20000" inputmode="numeric" [(ngModel)]="draft.calories" />
          </mat-form-field>
          @if (hasDistance()) {
            <mat-form-field appearance="outline">
              <mat-label>Distance (km, facultative)</mat-label>
              <input matInput type="number" name="distance" min="0" max="1000" step="0.01" inputmode="decimal" [(ngModel)]="draft.distanceKm" />
            </mat-form-field>
          }
        </div>
        <p class="lo-help">Modifier une séance type ne change pas les séances déjà enregistrées.</p>
      </form>
    </app-editor-frame>
  `,
})
export class TemplateDialogComponent extends LibraryDialog {
  readonly data = inject<TemplateDialogData>(MAT_DIALOG_DATA)
  readonly template = this.data.template
  readonly draft: SportTemplateRequest = this.template
    ? { name: this.template.name, sport: this.template.sport, durationMinutes: this.template.durationMinutes, distanceKm: this.template.distanceKm, calories: this.template.calories }
    : { name: '', sport: 'running', durationMinutes: 45, distanceKm: null, calories: 0 }
  private readonly initial = formSnapshot(this.draft)

  dirty(): boolean { return hasChanges(this.initial, this.draft) }
  hasDistance(): boolean { return this.data.sports.find(sport => sport.code === this.draft.sport)?.hasDistance ?? false }

  save(): Promise<void> {
    const name = this.draft.name.trim()
    if (!name) { this.error.set('Donnez un nom à la séance type.'); return Promise.resolve() }
    const distance = this.draft.distanceKm === null || String(this.draft.distanceKm) === '' ? null : Number(this.draft.distanceKm)
    const request: SportTemplateRequest = {
      ...this.draft, name, durationMinutes: Number(this.draft.durationMinutes), calories: Number(this.draft.calories),
      distanceKm: this.hasDistance() ? distance : null,
    }
    return this.run(() => this.template ? this.api.updateTemplate(this.template.id, request) : this.api.createTemplate(request),
      this.template ? 'Séance type enregistrée' : 'Séance type ajoutée')
  }

  archive(): Promise<void> {
    if (!this.template) return Promise.resolve()
    return this.run(() => this.api.archiveTemplate(this.template!.id), `« ${this.template.name} » archivée ; les séances passées gardent son nom`)
  }
}
