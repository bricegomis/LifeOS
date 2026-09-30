import { HttpErrorResponse } from '@angular/common/http'
import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { forkJoin, Observable } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type {
  ActivitySessionDto,
  ActivitySessionRequest,
  ComposedMealDto,
  DailyNutritionTargetDto,
  DayPlanDto,
  BalancedPlanDimension,
  BalancedPlanDto,
  BalancedPlanExplanation,
  ComputedBalancedPlanDto,
  PlannedMealDto,
  PlannedMealPartDto,
  RecipeDto,
  WeekDto,
} from '@/app/core/api/api.models'

interface Option {
  value: string
  label: string
}

interface MealDraft {
  mealType: string
  source: string
}

interface ActivityDraft {
  editingId: string | null
  type: string
  intensity: string
  durationMinutes: number | null
  estimatedEnergyKcal: number | null
}

interface DayDetails {
  loading: boolean
  error: string
  sessions: ActivitySessionDto[]
  nutrition: DailyNutritionTargetDto | null
  nutritionError: string
}

interface BalancedPlanView {
  id: string
  applied: boolean
  createdAt: string
  overallScore: number | null
  nutritionConstraintMet: boolean
  dimensions: BalancedPlanDimension[]
  tradeoffs: string[]
  limitations: string[]
  text: string
  parseError: boolean
}

// Backend Create/UpdateActivitySessionRequest also accept estimatedEnergyKcal and store it verbatim
// (no server-side computation). It is only sent when the user provides it: omitted on create => 0,
// omitted on update => unchanged.
type ActivitySessionPayload = ActivitySessionRequest & { estimatedEnergyKcal?: number }

export const WEEK_STATUSES: Option[] = [
  { value: 'draft', label: 'Brouillon' },
  { value: 'active', label: 'Active' },
  { value: 'past', label: 'Passée' },
]

export const WORK_CONTEXTS: Option[] = [
  { value: 'home', label: 'Télétravail' },
  { value: 'office', label: 'Bureau' },
  { value: 'off', label: 'Repos' },
]

export const MEAL_TYPES: Option[] = [
  { value: 'breakfast', label: 'Petit-déjeuner' },
  { value: 'lunch', label: 'Déjeuner' },
  { value: 'dinner', label: 'Dîner' },
  { value: 'snack', label: 'Collation' },
]

export const MEAL_STATUSES: Option[] = [
  { value: 'planned', label: 'Prévu' },
  { value: 'consumed', label: 'Consommé' },
  { value: 'replaced', label: 'Remplacé' },
  { value: 'skipped', label: 'Sauté' },
]

/**
 * The four dimensions the engine always balances at once. They are shown to explain the
 * result, never to be chosen: the user no longer picks a strategy.
 */
export const BALANCED_DIMENSIONS: Option[] = [
  { value: 'nutrition', label: 'Équilibre nutritionnel' },
  { value: 'cost', label: 'Budget' },
  { value: 'diversity', label: 'Diversité sur le mois' },
  { value: 'waste', label: 'Anti-gaspillage' },
]

export const ACTIVITY_INTENSITIES: Option[] = [
  { value: 'low', label: 'Faible' },
  { value: 'moderate', label: 'Modérée' },
  { value: 'high', label: 'Élevée' },
]

export const ACTIVITY_TYPES: Option[] = [
  { value: 'walk', label: 'Marche' },
  { value: 'run', label: 'Course' },
  { value: 'bike', label: 'Vélo' },
  { value: 'strength', label: 'Renforcement' },
]

const MEAL_ORDER = MEAL_TYPES.map((option) => option.value)

function labelFor(options: Option[], value: string): string {
  return options.find((option) => option.value === value)?.label ?? value
}

function toIsoDate(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function parseIsoDate(value: string): Date {
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  return new Date(year ?? 1970, (month ?? 1) - 1, day ?? 1)
}

function addDays(value: string, days: number): string {
  const date = parseIsoDate(value)
  date.setDate(date.getDate() + days)
  return toIsoDate(date)
}

function nextMonday(from = new Date()): string {
  const date = new Date(from.getFullYear(), from.getMonth(), from.getDate())
  const offset = (8 - date.getDay()) % 7 || 7
  date.setDate(date.getDate() + offset)
  return toIsoDate(date)
}

function httpErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const payload: unknown = error.error
    if (payload && typeof payload === 'object') {
      const problem = payload as { detail?: unknown; title?: unknown; errors?: unknown }
      const validation =
        problem.errors && typeof problem.errors === 'object'
          ? Object.values(problem.errors as Record<string, unknown>)
              .flat()
              .filter((entry): entry is string => typeof entry === 'string')
          : []
      const summary = [problem.detail, problem.title].find((entry): entry is string => typeof entry === 'string')
      const parts = [summary, ...validation].filter(Boolean)
      if (parts.length) return `${parts.join(' · ')} (HTTP ${error.status})`
    }
    if (typeof payload === 'string' && payload.trim()) return `${payload} (HTTP ${error.status})`
    if (error.status === 0) return 'LifeOS API est injoignable. Vérifiez la connexion et la configuration.'
    return `${error.statusText || 'Erreur'} (HTTP ${error.status})`
  }
  return apiErrorMessage(error)
}

function isExplanation(value: unknown): value is BalancedPlanExplanation {
  return typeof value === 'object' && value !== null && 'dimensions' in value
}

function toPlanView(plan: BalancedPlanDto | ComputedBalancedPlanDto): BalancedPlanView {
  let explanation: BalancedPlanExplanation | null = null
  let parseError = false

  if (typeof plan.explanation === 'string') {
    try {
      const parsed: unknown = JSON.parse(plan.explanation)
      if (isExplanation(parsed)) explanation = parsed
      else parseError = true
    } catch {
      parseError = true
    }
  } else {
    explanation = plan.explanation
  }

  return {
    id: plan.id,
    applied: plan.applied,
    createdAt: plan.createdAt,
    overallScore: explanation?.overallScore ?? null,
    nutritionConstraintMet: explanation?.nutritionConstraintMet ?? false,
    dimensions: explanation?.dimensions ?? [],
    tradeoffs: explanation?.tradeoffs ?? [],
    limitations: explanation?.limitations ?? [],
    text: explanation?.textExplanation ?? '',
    parseError,
  }
}

function emptyActivityDraft(): ActivityDraft {
  return { editingId: null, type: 'walk', intensity: 'moderate', durationMinutes: 30, estimatedEnergyKcal: null }
}

@Component({
  selector: 'app-planning-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './planning-page.component.html',
  styles: [`
    .planning-section { display: grid; gap: 14px; }
    .planning-section-heading { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 8px; }
    .planning-section-heading h2 { margin: 0; font-size: 1.2rem; }
    .planning-feedback { margin: 0; padding: 12px 14px; border-radius: 12px; font-weight: 700; }
    .planning-feedback.is-error { background: #fbe9e4; color: #8a2f1c; }
    .planning-feedback.is-info { background: var(--lifeos-accent-soft); color: var(--lifeos-accent-strong); }
    .planning-inline-form { display: flex; flex-wrap: wrap; align-items: end; gap: 10px; }
    .planning-field { display: grid; gap: 4px; font-size: 0.85rem; font-weight: 700; }
    .planning-field .text-input { min-height: 2.3rem; padding: 0.45rem 0.6rem; }
    .planning-check { display: inline-flex; align-items: center; gap: 6px; font-size: 0.9rem; font-weight: 650; }
    .planning-objectives { display: flex; flex-wrap: wrap; gap: 12px; margin: 0; padding: 0; border: 0; }
    .planning-objectives legend { margin-bottom: 6px; font-size: 0.85rem; font-weight: 800; }
    .planning-button { min-height: 2.3rem; padding: 0.45rem 0.9rem; border: 1px solid var(--lifeos-accent); border-radius: 9px; background: var(--lifeos-accent); color: white; font-weight: 750; cursor: pointer; }
    .planning-button.is-secondary { background: transparent; color: var(--lifeos-accent-strong); }
    .planning-button.is-danger { border-color: #b4452c; background: transparent; color: #8a2f1c; }
    .planning-button:disabled { cursor: not-allowed; opacity: 0.6; }
    .planning-button.is-small { min-height: 2rem; padding: 0.3rem 0.6rem; font-size: 0.82rem; }
    .plan-list, .day-list, .meal-list, .session-list { display: grid; gap: 10px; margin: 0; padding: 0; list-style: none; }
    .plan-list { grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); }
    .plan-card, .day-card { display: grid; gap: 10px; padding: 16px; border: 1px solid var(--lifeos-line); border-radius: 18px; background: var(--lifeos-surface); }
    .plan-card.is-applied { border-color: var(--lifeos-accent); background: var(--lifeos-accent-soft); }
    .plan-card h3, .day-card h3 { margin: 0; font-size: 1rem; }
    .plan-card p { margin: 0; color: var(--lifeos-text-soft); line-height: 1.45; }
    .plan-card-header { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 8px; }
    .plan-flag { font-size: 0.8rem; font-weight: 750; color: var(--lifeos-accent-strong); }
    .plan-flag.is-warning { color: #8a2f1c; }
    .plan-dimensions { display: grid; gap: 8px; margin: 0; padding: 0; list-style: none; }
    .plan-dimension-title { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 6px; font-size: 0.88rem; }
    .plan-notes { display: grid; gap: 4px; font-size: 0.85rem; }
    .plan-notes h4 { margin: 0; font-size: 0.85rem; }
    .plan-notes ul { margin: 0; padding-left: 18px; color: var(--lifeos-text-soft); line-height: 1.45; }
    .plan-notes.is-limits ul { color: #8a2f1c; }
    .day-list { grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); }
    .day-card-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 10px; }
    .day-context-controls { display: flex; flex-wrap: wrap; align-items: end; gap: 10px; }
    .meal-item { display: grid; gap: 8px; padding: 10px 12px; border-radius: 12px; background: var(--lifeos-surface-soft); }
    .meal-item-title { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 6px; }
    .meal-item-title strong { font-size: 0.95rem; }
    .meal-item-actions { display: flex; flex-wrap: wrap; align-items: end; gap: 8px; }
    .meal-parts { display: grid; gap: 6px; margin: 0; padding: 0; list-style: none; font-size: 0.85rem; }
    .meal-parts li { display: flex; flex-wrap: wrap; align-items: end; gap: 6px; }
    .day-details { display: grid; gap: 10px; padding-top: 10px; border-top: 1px solid var(--lifeos-line); }
    .session-list li { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; font-size: 0.9rem; }
    .nutrition-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 6px; margin: 0; font-size: 0.85rem; }
    .nutrition-grid dt { color: var(--lifeos-text-soft); }
    .nutrition-grid dd { margin: 0; font-weight: 750; }
    .missing-days { display: flex; flex-wrap: wrap; gap: 8px; }
    .muted { margin: 0; color: var(--lifeos-text-soft); font-size: 0.88rem; }
  `],
})
export class PlanningPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)

  readonly weekStatuses = WEEK_STATUSES
  readonly workContexts = WORK_CONTEXTS
  readonly mealTypes = MEAL_TYPES
  readonly mealStatuses = MEAL_STATUSES
  readonly balancedDimensions = BALANCED_DIMENSIONS
  readonly activityIntensities = ACTIVITY_INTENSITIES
  readonly activityTypes = ACTIVITY_TYPES

  readonly weeks = signal<WeekDto[]>([])
  readonly selectedWeekId = signal<string | null>(null)
  readonly loadingWeeks = signal(false)
  readonly error = signal('')
  readonly message = signal('')
  readonly busy = signal<string | null>(null)

  readonly recipes = signal<RecipeDto[]>([])
  readonly composedMeals = signal<ComposedMealDto[]>([])
  readonly catalogueError = signal('')

  readonly plans = signal<BalancedPlanView[]>([])
  readonly plansLoading = signal(false)
  readonly planError = signal('')

  readonly dayDetails = signal<Record<string, DayDetails>>({})
  readonly expandedDays = signal<string[]>([])

  newWeekStartsOn = nextMonday()
  readonly mealDrafts: Record<string, MealDraft> = {}
  readonly activityDrafts: Record<string, ActivityDraft> = {}
  readonly replaceDrafts: Record<string, string> = {}
  readonly partDrafts: Record<string, number> = {}

  readonly sortedWeeks = computed(() => [...this.weeks()].sort((a, b) => b.startsOn.localeCompare(a.startsOn)))

  readonly selectedWeek = computed(() => this.weeks().find((week) => week.id === this.selectedWeekId()) ?? null)

  readonly sortedDays = computed<DayPlanDto[]>(() =>
    [...(this.selectedWeek()?.dayPlans ?? [])].sort((a, b) => a.date.localeCompare(b.date)),
  )

  readonly missingDates = computed(() => {
    const week = this.selectedWeek()
    if (!week) return []
    const existing = new Set(week.dayPlans.map((day) => day.date.slice(0, 10)))
    return Array.from({ length: 7 }, (_, index) => addDays(week.startsOn, index)).filter((date) => !existing.has(date))
  })

  readonly summary = computed(() => {
    const meals = this.sortedDays().flatMap((day) => day.plannedMeals)
    return {
      days: this.sortedDays().length,
      meals: meals.length,
      consumed: meals.filter((meal) => meal.status === 'consumed').length,
      officeDays: this.sortedDays().filter((day) => day.workContext === 'office').length,
      bikeDays: this.sortedDays().filter((day) => day.bikeCommute).length,
      retainedPlan: this.plans().find((plan) => plan.applied) ?? null,
    }
  })

  readonly mealSourceOptions = computed<Option[]>(() => [
    ...this.composedMeals().map((meal) => ({ value: `composed:${meal.id}`, label: `Repas composé · ${meal.name}` })),
    ...this.recipes().map((recipe) => ({ value: `recipe:${recipe.id}`, label: `Recette · ${recipe.name}` })),
  ])

  ngOnInit(): void {
    this.loadWeeks()
    this.loadCatalogue()
  }

  label(options: Option[], value: string): string {
    return labelFor(options, value)
  }

  isKnown(options: Option[], value: string): boolean {
    return options.some((option) => option.value === value)
  }

  formatDate(value: string, options: Intl.DateTimeFormatOptions = { weekday: 'long', day: 'numeric', month: 'long' }): string {
    return new Intl.DateTimeFormat('fr-FR', options).format(parseIsoDate(value))
  }

  weekLabel(week: WeekDto): string {
    return `Semaine du ${this.formatDate(week.startsOn, { day: 'numeric', month: 'long', year: 'numeric' })} · ${labelFor(WEEK_STATUSES, week.status)}`
  }

  mealName(meal: PlannedMealDto): string {
    if (meal.composedMealId) {
      const composed = this.composedMeals().find((entry) => entry.id === meal.composedMealId)
      return composed ? composed.name : `Repas composé ${meal.composedMealId.slice(0, 8)}`
    }
    if (meal.recipeId) {
      const recipe = this.recipes().find((entry) => entry.id === meal.recipeId)
      return recipe ? recipe.name : `Recette ${meal.recipeId.slice(0, 8)}`
    }
    return 'Repas sans référence'
  }

  sortedMeals(day: DayPlanDto): PlannedMealDto[] {
    return [...day.plannedMeals].sort((a, b) => MEAL_ORDER.indexOf(a.mealType) - MEAL_ORDER.indexOf(b.mealType))
  }

  mealDraft(dayId: string): MealDraft {
    return (this.mealDrafts[dayId] ??= { mealType: 'lunch', source: '' })
  }

  activityDraft(dayId: string): ActivityDraft {
    return (this.activityDrafts[dayId] ??= emptyActivityDraft())
  }

  partDraft(part: PlannedMealPartDto): number {
    return (this.partDrafts[part.id] ??= part.portionMultiplier)
  }

  isExpanded(dayId: string): boolean {
    return this.expandedDays().includes(dayId)
  }

  selectWeek(weekId: string | null): void {
    this.selectedWeekId.set(weekId)
    this.expandedDays.set([])
    this.dayDetails.set({})
    this.loadPlans()
  }

  loadWeeks(preferredId?: string): void {
    this.loadingWeeks.set(true)
    this.error.set('')
    this.api.get<WeekDto[]>('/weeks').subscribe({
      next: (weeks) => {
        this.weeks.set(weeks)
        this.loadingWeeks.set(false)
        const current = preferredId ?? this.selectedWeekId()
        const target = weeks.find((week) => week.id === current) ?? this.defaultWeek(weeks)
        this.selectWeek(target?.id ?? null)
      },
      error: (error: unknown) => {
        this.loadingWeeks.set(false)
        this.error.set(`Impossible de charger les semaines : ${httpErrorMessage(error)}`)
      },
    })
  }

  private defaultWeek(weeks: WeekDto[]): WeekDto | undefined {
    const today = toIsoDate(new Date())
    const containing = weeks.find((week) => week.startsOn <= today && addDays(week.startsOn, 6) >= today)
    return containing ?? [...weeks].sort((a, b) => b.startsOn.localeCompare(a.startsOn))[0]
  }

  private loadCatalogue(): void {
    this.catalogueError.set('')
    forkJoin({
      recipes: this.api.get<RecipeDto[]>('/recipes'),
      composedMeals: this.api.get<ComposedMealDto[]>('/composed-meals'),
    }).subscribe({
      next: ({ recipes, composedMeals }) => {
        this.recipes.set(recipes)
        this.composedMeals.set(composedMeals)
      },
      error: (error: unknown) =>
        this.catalogueError.set(`Catalogue recettes / repas composés indisponible : ${httpErrorMessage(error)}`),
    })
  }

  private refreshWeek(weekId: string, successMessage?: string): void {
    this.api.get<WeekDto>(`/weeks/${weekId}`).subscribe({
      next: (week) => {
        this.weeks.update((weeks) => weeks.map((entry) => (entry.id === week.id ? week : entry)))
        this.busy.set(null)
        if (successMessage) this.message.set(successMessage)
      },
      error: (error: unknown) => {
        this.busy.set(null)
        this.error.set(`Impossible de recharger la semaine : ${httpErrorMessage(error)}`)
      },
    })
  }

  private run<T>(key: string, request: Observable<T>, onSuccess: (result: T) => void, errorPrefix: string): void {
    this.busy.set(key)
    this.error.set('')
    this.message.set('')
    request.subscribe({
      next: (result) => onSuccess(result),
      error: (error: unknown) => {
        this.busy.set(null)
        this.error.set(`${errorPrefix} : ${httpErrorMessage(error)}`)
      },
    })
  }

  createWeek(): void {
    if (!this.newWeekStartsOn) {
      this.error.set('Choisissez une date de début de semaine.')
      return
    }
    this.run(
      'create-week',
      this.api.post<WeekDto>('/weeks', { startsOn: this.newWeekStartsOn, status: 'draft' }),
      (week) => {
        this.busy.set(null)
        this.message.set('Semaine créée en brouillon.')
        this.loadWeeks(week.id)
      },
      'Création de la semaine impossible',
    )
  }

  updateWeekStatus(week: WeekDto, status: string): void {
    if (status === week.status) return
    this.run(
      'week-status',
      this.api.put<WeekDto>(`/weeks/${week.id}/status`, { status }),
      () => this.refreshWeek(week.id, `Statut mis à jour : ${labelFor(WEEK_STATUSES, status)}.`),
      'Changement de statut impossible',
    )
  }

  deleteWeek(week: WeekDto): void {
    if (!confirm(`Supprimer la semaine du ${this.formatDate(week.startsOn, { day: 'numeric', month: 'long' })} ?`)) return
    this.run(
      'delete-week',
      this.api.delete(`/weeks/${week.id}`),
      () => {
        this.busy.set(null)
        this.message.set('Semaine supprimée.')
        this.selectedWeekId.set(null)
        this.loadWeeks()
      },
      'Suppression de la semaine impossible',
    )
  }

  addDay(week: WeekDto, date: string): void {
    this.run(
      `add-day-${date}`,
      this.api.post<DayPlanDto>(`/day-plans/${week.id}/days`, { date, workContext: 'home', bikeCommute: false }),
      () => this.refreshWeek(week.id, 'Jour ajouté.'),
      'Ajout du jour impossible',
    )
  }

  updateDay(week: WeekDto, day: DayPlanDto, changes: { workContext?: string; bikeCommute?: boolean }): void {
    this.run(
      `day-${day.id}`,
      this.api.put<DayPlanDto>(`/day-plans/${day.id}`, changes),
      () => {
        this.refreshWeek(week.id, 'Contexte du jour enregistré.')
        if (this.isExpanded(day.id)) this.loadDayDetails(day.id)
      },
      'Mise à jour du jour impossible',
    )
  }

  deleteDay(week: WeekDto, day: DayPlanDto): void {
    if (!confirm(`Supprimer ${this.formatDate(day.date)} et ses repas ?`)) return
    this.run(
      `delete-day-${day.id}`,
      this.api.delete(`/day-plans/${day.id}`),
      () => this.refreshWeek(week.id, 'Jour supprimé.'),
      'Suppression du jour impossible',
    )
  }

  addMeal(week: WeekDto, day: DayPlanDto): void {
    const draft = this.mealDraft(day.id)
    const reference = this.referenceFromSource(draft.source)
    if (!reference) {
      this.error.set('Choisissez une recette ou un repas composé avant d’ajouter le repas.')
      return
    }
    this.run(
      `add-meal-${day.id}`,
      this.api.post<PlannedMealDto>(`/planned-meals/${day.id}/meals`, {
        mealType: draft.mealType,
        ...reference,
        status: 'planned',
      }),
      () => {
        draft.source = ''
        this.refreshWeek(week.id, 'Repas ajouté.')
      },
      'Ajout du repas impossible',
    )
  }

  updateMealStatus(week: WeekDto, meal: PlannedMealDto, status: string): void {
    if (status === meal.status) return
    this.run(
      `meal-${meal.id}`,
      this.api.put<PlannedMealDto>(`/planned-meals/${meal.id}/status`, { status }),
      () => this.refreshWeek(week.id, `Repas marqué « ${labelFor(MEAL_STATUSES, status)} ».`),
      'Mise à jour du repas impossible',
    )
  }

  replaceMeal(week: WeekDto, meal: PlannedMealDto): void {
    const reference = this.referenceFromSource(this.replaceDrafts[meal.id] ?? '')
    if (!reference) {
      this.error.set('Choisissez la recette ou le repas composé de remplacement.')
      return
    }
    this.run(
      `replace-${meal.id}`,
      this.api.put<PlannedMealDto>(`/planned-meals/${meal.id}/replace`, reference),
      () => {
        delete this.replaceDrafts[meal.id]
        this.refreshWeek(week.id, 'Repas remplacé.')
      },
      'Remplacement du repas impossible',
    )
  }

  deleteMeal(week: WeekDto, meal: PlannedMealDto): void {
    if (!confirm(`Supprimer « ${this.mealName(meal)} » ?`)) return
    this.run(
      `delete-meal-${meal.id}`,
      this.api.delete(`/planned-meals/${meal.id}`),
      () => this.refreshWeek(week.id, 'Repas supprimé.'),
      'Suppression du repas impossible',
    )
  }

  updatePart(week: WeekDto, meal: PlannedMealDto, part: PlannedMealPartDto): void {
    const portionMultiplier = Number(this.partDrafts[part.id])
    if (!Number.isFinite(portionMultiplier) || portionMultiplier <= 0) {
      this.error.set('La portion doit être un nombre strictement positif.')
      return
    }
    this.run(
      `part-${part.id}`,
      this.api.put<PlannedMealDto>(`/planned-meals/${meal.id}/parts/${part.id}`, { portionMultiplier }),
      () => this.refreshWeek(week.id, 'Portion mise à jour.'),
      'Mise à jour de la portion impossible',
    )
  }

  deletePart(week: WeekDto, meal: PlannedMealDto, part: PlannedMealPartDto): void {
    this.run(
      `delete-part-${part.id}`,
      this.api.delete(`/planned-meals/${meal.id}/parts/${part.id}`),
      () => {
        delete this.partDrafts[part.id]
        this.refreshWeek(week.id, 'Portion retirée.')
      },
      'Suppression de la portion impossible',
    )
  }

  private referenceFromSource(source: string): { composedMealId: string } | { recipeId: string } | null {
    const [kind, id] = source.split(':')
    if (!id) return null
    if (kind === 'composed') return { composedMealId: id }
    if (kind === 'recipe') return { recipeId: id }
    return null
  }

  toggleDayDetails(dayId: string): void {
    if (this.isExpanded(dayId)) {
      this.expandedDays.update((days) => days.filter((entry) => entry !== dayId))
      return
    }
    this.expandedDays.update((days) => [...days, dayId])
    this.loadDayDetails(dayId)
  }

  private patchDetails(dayId: string, changes: Partial<DayDetails>): void {
    this.dayDetails.update((details) => {
      const current = details[dayId] ?? { loading: false, error: '', sessions: [], nutrition: null, nutritionError: '' }
      return { ...details, [dayId]: { ...current, ...changes } }
    })
  }

  loadDayDetails(dayId: string): void {
    this.patchDetails(dayId, { loading: true, error: '' })
    this.api.get<ActivitySessionDto[]>(`/activity-sessions/day/${dayId}`).subscribe({
      next: (sessions) => this.patchDetails(dayId, { loading: false, sessions }),
      error: (error: unknown) =>
        this.patchDetails(dayId, { loading: false, error: `Activités indisponibles : ${httpErrorMessage(error)}` }),
    })
    this.api.get<DailyNutritionTargetDto>(`/nutrition/calculations/day/${dayId}`).subscribe({
      next: (nutrition) => this.patchDetails(dayId, { nutrition, nutritionError: '' }),
      error: (error: unknown) =>
        this.patchDetails(dayId, { nutrition: null, nutritionError: `Objectif nutritionnel indisponible : ${httpErrorMessage(error)}` }),
    })
  }

  editSession(dayId: string, session: ActivitySessionDto): void {
    this.activityDrafts[dayId] = {
      editingId: session.id,
      type: session.type,
      intensity: session.intensity,
      durationMinutes: session.durationMinutes,
      estimatedEnergyKcal: session.estimatedEnergyKcal,
    }
  }

  cancelSessionEdit(dayId: string): void {
    this.activityDrafts[dayId] = emptyActivityDraft()
  }

  saveSession(dayId: string): void {
    const draft = this.activityDraft(dayId)
    const durationMinutes = Number(draft.durationMinutes)
    const rawEnergy = draft.estimatedEnergyKcal as number | string | null
    const estimatedEnergyKcal = rawEnergy === null || rawEnergy === '' ? null : Number(rawEnergy)
    if (!draft.type.trim() || !draft.intensity || !Number.isInteger(durationMinutes) || durationMinutes < 1) {
      this.patchDetails(dayId, { error: 'Renseignez un type, une intensité et une durée entière d’au moins 1 minute.' })
      return
    }
    if (estimatedEnergyKcal !== null && (!Number.isFinite(estimatedEnergyKcal) || estimatedEnergyKcal < 0)) {
      this.patchDetails(dayId, { error: 'La dépense estimée doit être positive ou vide.' })
      return
    }
    const body: ActivitySessionPayload = { type: draft.type.trim(), intensity: draft.intensity, durationMinutes }
    if (estimatedEnergyKcal !== null) body.estimatedEnergyKcal = estimatedEnergyKcal
    const request = draft.editingId
      ? this.api.put<ActivitySessionDto>(`/activity-sessions/${draft.editingId}`, body)
      : this.api.post<ActivitySessionDto>(`/activity-sessions/day/${dayId}`, body)
    this.busy.set(`session-${dayId}`)
    this.patchDetails(dayId, { error: '' })
    request.subscribe({
      next: () => {
        this.busy.set(null)
        this.activityDrafts[dayId] = emptyActivityDraft()
        this.loadDayDetails(dayId)
      },
      error: (error: unknown) => {
        this.busy.set(null)
        this.patchDetails(dayId, { error: `Enregistrement de l’activité impossible : ${httpErrorMessage(error)}` })
      },
    })
  }

  deleteSession(dayId: string, session: ActivitySessionDto): void {
    if (!confirm('Supprimer cette activité ?')) return
    this.busy.set(`session-${dayId}`)
    this.api.delete(`/activity-sessions/${session.id}`).subscribe({
      next: () => {
        this.busy.set(null)
        if (this.activityDrafts[dayId]?.editingId === session.id) this.activityDrafts[dayId] = emptyActivityDraft()
        this.loadDayDetails(dayId)
      },
      error: (error: unknown) => {
        this.busy.set(null)
        this.patchDetails(dayId, { error: `Suppression de l’activité impossible : ${httpErrorMessage(error)}` })
      },
    })
  }

  /** Percentage shown in the UI for a 0..1 score. */
  percent(score: number | null): string {
    return score === null ? '—' : `${Math.round(score * 100)} %`
  }

  dimensionLabel(key: string): string {
    return labelFor(BALANCED_DIMENSIONS, key)
  }

  loadPlans(): void {
    const weekId = this.selectedWeekId()
    this.plans.set([])
    this.planError.set('')
    if (!weekId) return
    this.plansLoading.set(true)
    this.api.get<BalancedPlanDto[]>(`/weeks/${weekId}/balanced-plan`).subscribe({
      next: (plans) => {
        if (this.selectedWeekId() !== weekId) return
        this.plans.set(plans.map(toPlanView).sort((a, b) => b.createdAt.localeCompare(a.createdAt)))
        this.plansLoading.set(false)
      },
      error: (error: unknown) => {
        this.plansLoading.set(false)
        this.planError.set(`Menu équilibré indisponible : ${httpErrorMessage(error)}`)
      },
    })
  }

  /** Runs the single balanced computation. There is no objective to pick. */
  computeBalancedPlan(week: WeekDto): void {
    this.busy.set('compute')
    this.planError.set('')
    this.api.post<ComputedBalancedPlanDto>(`/weeks/${week.id}/balanced-plan/compute`, {}).subscribe({
      next: (computed) => {
        this.busy.set(null)
        this.message.set(
          computed.explanation.overallScore === null
            ? 'Calcul effectué, mais les données disponibles ne permettent pas encore de score global.'
            : 'Menu équilibré calculé. Relisez les arbitrages avant de le retenir.',
        )
        this.loadPlans()
      },
      error: (error: unknown) => {
        this.busy.set(null)
        this.planError.set(`Calcul impossible : ${httpErrorMessage(error)}`)
      },
    })
  }

  retainPlan(week: WeekDto, plan: BalancedPlanView): void {
    if (!confirm('Retenir ce menu équilibré pour la semaine ?')) return
    this.busy.set(`apply-${plan.id}`)
    this.planError.set('')
    this.api.patch<void>(`/weeks/${week.id}/balanced-plan/${plan.id}/apply`).subscribe({
      next: () => {
        this.loadPlans()
        this.refreshWeek(week.id, 'Menu équilibré retenu.')
      },
      error: (error: unknown) => {
        this.busy.set(null)
        this.planError.set(`Impossible de retenir ce menu : ${httpErrorMessage(error)}`)
      },
    })
  }
}
