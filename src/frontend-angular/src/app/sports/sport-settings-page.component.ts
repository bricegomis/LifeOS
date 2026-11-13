import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { SportNavComponent } from './sport-nav.component'
import { challengeUnitLabel, decimal, duration, integer } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type {
  ChallengeReportDto, DailyChallengeRequest, GoalKind, SportOption, SportTemplateDto, SportTemplateRequest, WeeklyGoalDto, WeeklyGoalRequest,
} from './sports.models'

const GOAL_KINDS: { id: GoalKind, label: string }[] = [
  { id: 'template', label: 'Séance type' }, { id: 'sport', label: 'Sport' }, { id: 'sessions', label: 'Nombre de séances' }, { id: 'volume', label: 'Volume' },
]

const emptyGoal = (): WeeklyGoalRequest => ({ kind: 'sessions', templateId: null, sport: 'running', unit: 'kcal', target: 3 })
const emptyChallenge = (): DailyChallengeRequest => ({ name: '', unit: 'reps', dailyTarget: 100, jokersPerWeek: 1 })
const emptyTemplate = (): SportTemplateRequest => ({ name: '', sport: 'running', durationMinutes: 45, distanceKm: null, calories: 0 })

/** Weekly goals, daily challenges and session templates (templates are edited on a computer). */
@Component({
  selector: 'app-sport-settings-page',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent, SportNavComponent],
  template: `
    <div class="page">
      <app-sport-nav />
      <div class="page-header"><h1>Réglages du sport</h1></div>
      @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
      @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="status-text" role="status">Chargement…</p> } @else {
        <div class="week-grid">
          <section class="sport-section" aria-labelledby="settings-goals">
            <h2 id="settings-goals">Objectifs de la semaine</h2>
            @if (goals().length) {
              <div class="settings-list">
                @for (goal of goals(); track goal.id) {
                  <div class="settings-row">
                    <span><strong>{{ goal.label }}</strong><span class="muted small">{{ goal.kindLabel }}</span></span>
                    <button type="button" class="lifeos-button lifeos-button-text danger-text" [disabled]="busy()" (click)="stopGoal(goal)">Arrêter</button>
                  </div>
                }
              </div>
            } @else { <p class="empty-state">Aucun objectif.</p> }
            @if (goalDraft) {
              <form class="inline-form" (ngSubmit)="saveGoal()">
                <div class="choice-grid" role="group" aria-label="Forme de l'objectif">
                  @for (kind of goalKinds; track kind.id) {
                    <button type="button" class="pill" [attr.aria-pressed]="goalDraft.kind === kind.id" (click)="goalDraft.kind = kind.id">{{ kind.label }}</button>
                  }
                </div>
                @if (goalDraft.kind === 'template') {
                  <label class="field"><span>Séance type</span>
                    <select class="text-input" name="goalTemplate" required [(ngModel)]="goalDraft.templateId">
                      @for (template of activeTemplates(); track template.id) { <option [value]="template.id">{{ template.name }}</option> }
                    </select></label>
                  @if (!activeTemplates().length) { <p class="muted small">Créez d'abord une séance type, plus bas.</p> }
                }
                @if (goalDraft.kind === 'sport') {
                  <label class="field"><span>Sport</span>
                    <select class="text-input" name="goalSport" [(ngModel)]="goalDraft.sport">
                      @for (sport of sports(); track sport.code) { <option [value]="sport.code">{{ sport.label }}</option> }
                    </select></label>
                }
                @if (goalDraft.kind === 'volume') {
                  <label class="field"><span>Unité</span>
                    <select class="text-input" name="goalUnit" [(ngModel)]="goalDraft.unit">
                      <option value="kcal">Calories (kcal)</option><option value="km">Distance (km)</option><option value="hours">Durée (heures)</option>
                    </select></label>
                }
                <label class="field"><span>{{ goalDraft.kind === 'volume' ? 'Cible par semaine' : 'Nombre de séances par semaine' }}</span>
                  <input class="text-input" type="number" name="goalTarget" required [min]="goalDraft.kind === 'volume' ? 0.5 : 1"
                    [max]="goalDraft.kind === 'volume' ? 100000 : 21" [step]="goalDraft.kind === 'volume' ? 'any' : 1" [(ngModel)]="goalDraft.target" /></label>
                <p class="muted small">L'objectif compte à partir de cette semaine ; les semaines passées gardent leurs objectifs d'alors.</p>
                <div class="page-actions">
                  <button type="submit" class="lifeos-button" [disabled]="busy()">Ajouter</button>
                  <button type="button" class="lifeos-button lifeos-button-secondary" (click)="goalDraft = null">Annuler</button>
                </div>
              </form>
            } @else {
              <button type="button" class="lifeos-button lifeos-button-secondary align-start" (click)="goalDraft = emptyGoal()">+ Ajouter un objectif</button>
            }
          </section>

          <section class="sport-section" aria-labelledby="settings-challenges">
            <h2 id="settings-challenges">Défis du jour</h2>
            @if (challenges().length) {
              <div class="settings-list">
                @for (report of challenges(); track report.challenge.id) {
                  <div class="settings-row">
                    <span><strong>{{ report.challenge.name }}</strong>
                      <span class="muted small">{{ challengeSummary(report) }}</span></span>
                    <span class="page-actions">
                      <button type="button" class="lifeos-button lifeos-button-text" [disabled]="busy()" (click)="editChallenge(report)">Modifier</button>
                      <button type="button" class="lifeos-button lifeos-button-text danger-text" [disabled]="busy()" (click)="stopChallenge(report)">Arrêter</button>
                    </span>
                  </div>
                }
              </div>
            } @else { <p class="empty-state">Aucun défi.</p> }
            @if (challengeDraft) {
              <form class="inline-form" (ngSubmit)="saveChallenge()">
                <label class="field"><span>Nom</span>
                  <input class="text-input" name="challengeName" required maxlength="100" placeholder="Ex. Pompes" [(ngModel)]="challengeDraft.name" /></label>
                <div class="field-row">
                  <label class="field"><span>Unité</span>
                    <select class="text-input" name="challengeUnit" [disabled]="!!editingChallengeId" [(ngModel)]="challengeDraft.unit">
                      <option value="reps">Répétitions</option><option value="minutes">Minutes</option></select></label>
                  <label class="field"><span>Cible par jour</span>
                    <input class="text-input" type="number" name="challengeTarget" required min="1" max="100000" step="1" [(ngModel)]="challengeDraft.dailyTarget" /></label>
                </div>
                <div class="field"><span>Jokers par semaine (jours manqués sans casser la série)</span>
                  <div class="choice-grid" role="group" aria-label="Jokers par semaine">
                    @for (count of [0, 1, 2]; track count) {
                      <button type="button" class="pill" [attr.aria-pressed]="challengeDraft.jokersPerWeek === count" (click)="challengeDraft.jokersPerWeek = count">{{ count }}</button>
                    }
                  </div>
                </div>
                @if (editingChallengeId) { <p class="muted small">Une nouvelle cible vaut à partir d'aujourd'hui ; les jours passés gardent la leur.</p> }
                <div class="page-actions">
                  <button type="submit" class="lifeos-button" [disabled]="busy()">{{ editingChallengeId ? 'Enregistrer' : 'Ajouter' }}</button>
                  <button type="button" class="lifeos-button lifeos-button-secondary" (click)="challengeDraft = null">Annuler</button>
                </div>
              </form>
            } @else {
              <button type="button" class="lifeos-button lifeos-button-secondary align-start" (click)="newChallenge()">+ Ajouter un défi</button>
            }
          </section>
        </div>

        <section class="sport-section" aria-labelledby="settings-templates">
          <div class="sport-section-head"><h2 id="settings-templates">Séances types</h2>
            <button type="button" class="lifeos-button lifeos-button-secondary desktop-only" (click)="openTemplate(null)">+ Ajouter une séance type</button></div>
          @if (activeTemplates().length) {
            <div class="settings-list">
              @for (template of activeTemplates(); track template.id) {
                <div class="settings-row">
                  <span><strong>{{ template.name }}</strong><span class="muted small">{{ templateSummary(template) }}</span></span>
                  <span class="page-actions desktop-only">
                    <button type="button" class="lifeos-button lifeos-button-text" [disabled]="busy()" (click)="openTemplate(template)">Modifier</button>
                    <button type="button" class="lifeos-button lifeos-button-text danger-text" [disabled]="busy()" (click)="archiveTemplate(template)">Archiver</button>
                  </span>
                </div>
              }
            </div>
          } @else { <p class="empty-state">Aucune séance type.</p> }
          <p class="muted small">Modifier une séance type ne change pas les séances déjà enregistrées. <span class="mobile-only">Création et modification sur ordinateur.</span></p>
        </section>
      }
    </div>

    <app-lifeos-dialog [open]="templateDialog()" [heading]="editingTemplateId ? 'Modifier la séance type' : 'Nouvelle séance type'" [centered]="true"
      formId="sport-template-form" [submitLabel]="editingTemplateId ? 'Enregistrer' : 'Créer'" [busy]="busy()" [dirty]="templateDirty()"
      [error]="dialogError()" description="Les calories sont proposées à chaque séance et restent modifiables." (dismissed)="templateDialog.set(false)">
      <form id="sport-template-form" class="dialog-form" (ngSubmit)="saveTemplate()">
        <label class="dialog-field"><span>Nom</span><input class="text-input" name="name" required maxlength="200" [(ngModel)]="templateDraft.name" /></label>
        <div class="dialog-row">
          <label class="dialog-field"><span>Sport</span>
            <select class="text-input" name="sport" [(ngModel)]="templateDraft.sport">
              @for (sport of sports(); track sport.code) { <option [value]="sport.code">{{ sport.label }}</option> }
            </select></label>
          <label class="dialog-field"><span>Durée (min)</span>
            <input class="text-input" type="number" name="duration" required min="1" max="1440" step="1" [(ngModel)]="templateDraft.durationMinutes" /></label>
        </div>
        <div class="dialog-row">
          <label class="dialog-field"><span>Calories proposées (kcal)</span>
            <input class="text-input" type="number" name="calories" required min="0" max="20000" step="1" [(ngModel)]="templateDraft.calories" /></label>
          @if (templateHasDistance()) {
            <label class="dialog-field"><span>Distance (km, facultative)</span>
              <input class="text-input" type="number" name="distance" min="0" max="1000" step="0.01" [(ngModel)]="templateDraft.distanceKm" /></label>
          }
        </div>
      </form>
    </app-lifeos-dialog>
  `,
  styleUrl: './sports.css',
  styles: [`.align-start { align-self: flex-start; }`],
})
export class SportSettingsPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  readonly loading = signal(true)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly dialogError = signal('')
  readonly sports = signal<SportOption[]>([])
  readonly templates = signal<SportTemplateDto[]>([])
  readonly goals = signal<WeeklyGoalDto[]>([])
  readonly challenges = signal<ChallengeReportDto[]>([])
  readonly activeTemplates = computed(() => this.templates().filter(template => !template.isArchived))
  readonly templateDialog = signal(false)
  readonly goalKinds = GOAL_KINDS
  readonly emptyGoal = emptyGoal

  goalDraft: WeeklyGoalRequest | null = null
  challengeDraft: DailyChallengeRequest | null = null
  editingChallengeId = ''
  templateDraft = emptyTemplate()
  editingTemplateId = ''
  private templateInitial = formSnapshot(this.templateDraft)

  async ngOnInit(): Promise<void> {
    try {
      const [catalog, templates, goals, challenges] = await Promise.all([
        firstValueFrom(this.api.catalog()), firstValueFrom(this.api.templates()),
        firstValueFrom(this.api.goals()), firstValueFrom(this.api.challenges(0)),
      ])
      this.sports.set(catalog.sports); this.templates.set(templates); this.goals.set(goals); this.challenges.set(challenges)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }

  challengeSummary(report: ChallengeReportDto): string {
    const jokers = report.challenge.jokersPerWeek
    return `${integer(report.challenge.dailyTarget)} ${challengeUnitLabel(report.challenge.unit)} par jour · ${jokers} joker${jokers > 1 ? 's' : ''} par semaine`
  }
  templateSummary(template: SportTemplateDto): string {
    const bits = [template.sportLabel, duration(template.durationMinutes)]
    if (template.distanceKm !== null) bits.push(`${decimal(template.distanceKm)} km`)
    bits.push(`${integer(template.calories)} kcal`)
    return bits.join(' · ')
  }
  templateHasDistance(): boolean { return this.sports().find(sport => sport.code === this.templateDraft.sport)?.hasDistance ?? false }
  templateDirty(): boolean { return hasChanges(this.templateInitial, this.templateDraft) }

  async saveGoal(): Promise<void> {
    const draft = this.goalDraft
    if (!draft) return
    const request: WeeklyGoalRequest = {
      kind: draft.kind, target: Number(draft.target),
      templateId: draft.kind === 'template' ? draft.templateId ?? this.activeTemplates()[0]?.id ?? null : null,
      sport: draft.kind === 'sport' ? draft.sport : null,
      unit: draft.kind === 'volume' ? draft.unit : null,
    }
    await this.run(async () => {
      const goal = await firstValueFrom(this.api.createGoal(request))
      this.goals.update(goals => [...goals, goal])
      this.goalDraft = null
      return 'Objectif ajouté à partir de cette semaine.'
    })
  }

  stopGoal(goal: WeeklyGoalDto): Promise<void> {
    if (!window.confirm(`Arrêter l'objectif « ${goal.label} » ? Il compte encore cette semaine.`)) return Promise.resolve()
    return this.run(async () => {
      await firstValueFrom(this.api.stopGoal(goal.id))
      this.goals.update(goals => goals.filter(item => item.id !== goal.id))
      return 'Objectif arrêté : il disparaît des semaines suivantes.'
    })
  }

  newChallenge(): void { this.editingChallengeId = ''; this.challengeDraft = emptyChallenge() }
  editChallenge(report: ChallengeReportDto): void {
    const { name, unit, dailyTarget, jokersPerWeek } = report.challenge
    this.editingChallengeId = report.challenge.id
    this.challengeDraft = { name, unit, dailyTarget, jokersPerWeek }
  }

  async saveChallenge(): Promise<void> {
    const draft = this.challengeDraft
    if (!draft) return
    const request = { ...draft, dailyTarget: Number(draft.dailyTarget) }
    await this.run(async () => {
      if (this.editingChallengeId) await firstValueFrom(this.api.updateChallenge(this.editingChallengeId, request))
      else await firstValueFrom(this.api.createChallenge(request))
      this.challenges.set(await firstValueFrom(this.api.challenges(0)))
      const message = this.editingChallengeId ? 'Défi modifié.' : 'Défi ajouté : il apparaît dès aujourd’hui.'
      this.challengeDraft = null; this.editingChallengeId = ''
      return message
    })
  }

  stopChallenge(report: ChallengeReportDto): Promise<void> {
    if (!window.confirm(`Arrêter le défi « ${report.challenge.name} » ? Son historique est conservé.`)) return Promise.resolve()
    return this.run(async () => {
      await firstValueFrom(this.api.stopChallenge(report.challenge.id))
      this.challenges.update(list => list.filter(item => item.challenge.id !== report.challenge.id))
      return 'Défi arrêté. Son historique est conservé.'
    })
  }

  openTemplate(template: SportTemplateDto | null): void {
    this.editingTemplateId = template?.id ?? ''
    this.templateDraft = template
      ? { name: template.name, sport: template.sport, durationMinutes: template.durationMinutes, distanceKm: template.distanceKm, calories: template.calories }
      : emptyTemplate()
    this.templateInitial = formSnapshot(this.templateDraft)
    this.dialogError.set('')
    this.templateDialog.set(true)
  }

  async saveTemplate(): Promise<void> {
    if (this.busy()) return
    const draft = this.templateDraft
    const request: SportTemplateRequest = { ...draft, distanceKm: this.templateHasDistance() && draft.distanceKm !== null ? Number(draft.distanceKm) : null }
    this.busy.set(true); this.dialogError.set('')
    try {
      const saved = await firstValueFrom(this.editingTemplateId
        ? this.api.updateTemplate(this.editingTemplateId, request) : this.api.createTemplate(request))
      this.templates.update(list => [...list.filter(item => item.id !== saved.id), saved].sort((a, b) => a.name.localeCompare(b.name, 'fr')))
      this.templateDialog.set(false)
      this.notice.set('Séance type enregistrée. Les séances déjà enregistrées ne changent pas.')
    } catch (error) { this.dialogError.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  archiveTemplate(template: SportTemplateDto): Promise<void> {
    if (!window.confirm(`Archiver « ${template.name} » ? Les séances passées gardent son nom.`)) return Promise.resolve()
    return this.run(async () => {
      await firstValueFrom(this.api.archiveTemplate(template.id))
      this.templates.update(list => list.map(item => item.id === template.id ? { ...item, isArchived: true } : item))
      return 'Séance type archivée.'
    })
  }

  private async run(action: () => Promise<string>): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set(''); this.notice.set('')
    try { this.notice.set(await action()) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
