import { Component, computed, inject, signal } from '@angular/core'
import { RouterLink } from '@angular/router'
import { LifeosApiService, apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import type {
  ComposedMealDto,
  DailyNutritionTargetDto,
  PlannedMealDto,
  RecipeDto,
  WeekDto,
} from '@/app/core/api/api.models'

const MEAL_TYPE_LABELS: Record<string, string> = {
  breakfast: 'Petit-déjeuner',
  lunch: 'Déjeuner',
  dinner: 'Dîner',
  snack: 'Collation',
}

const MEAL_STATUS_LABELS: Record<string, string> = {
  planned: 'Prévu',
  consumed: 'Consommé',
  replaced: 'Remplacé',
  skipped: 'Sauté',
}

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './dashboard-page.component.html',
})
export class DashboardPageComponent {
  private readonly api = inject(LifeosApiService)
  readonly loading = signal(true)
  readonly creatingWeek = signal(false)
  readonly error = signal('')
  readonly weeks = signal<WeekDto[]>([])
  readonly selectedWeekId = signal('')
  readonly nutritionTarget = signal<DailyNutritionTargetDto | null>(null)
  readonly nutritionError = signal('')
  readonly recipeNames = signal<Record<string, string>>({})
  readonly composedMealNames = signal<Record<string, string>>({})
  readonly catalogueError = signal('')

  readonly selectedWeek = computed(
    () => this.weeks().find((week) => week.id === this.selectedWeekId()) ?? null,
  )
  readonly today = computed(() => {
    const todayKey = localDateKey(new Date())
    return this.selectedWeek()?.dayPlans.find((day) => day.date === todayKey) ?? null
  })
  readonly todayMeals = computed(() => this.today()?.plannedMeals ?? [])

  constructor() {
    this.loadWeeks()
    this.loadMealNames()
  }

  loadWeeks(): void {
    this.loading.set(true)
    this.error.set('')
    this.api.get<WeekDto[]>('/weeks').subscribe({
      next: (weeks) => {
        const ordered = [...weeks].sort((left, right) => right.startsOn.localeCompare(left.startsOn))
        this.weeks.set(ordered)
        const current = localDateKey(new Date())
        const thisWeek = ordered.find((week) => week.dayPlans.some((day) => day.date === current))
        this.selectedWeekId.set(thisWeek?.id ?? ordered[0]?.id ?? '')
        this.loadTodayNutrition()
        this.loading.set(false)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.loading.set(false)
      },
    })
  }

  selectWeek(weekId: string): void {
    this.selectedWeekId.set(weekId)
    this.nutritionTarget.set(null)
    this.loadTodayNutrition()
  }

  onWeekChange(event: Event): void {
    if (event.target instanceof HTMLSelectElement) {
      this.selectWeek(event.target.value)
    }
  }

  mealName(meal: PlannedMealDto): string {
    if (meal.recipeId) return this.recipeNames()[meal.recipeId] ?? 'Recette'
    if (meal.composedMealId) return this.composedMealNames()[meal.composedMealId] ?? 'Repas composé'
    return 'Repas à organiser'
  }

  mealTypeLabel(meal: PlannedMealDto): string {
    return MEAL_TYPE_LABELS[meal.mealType] ?? meal.mealType
  }

  mealStatusLabel(meal: PlannedMealDto): string {
    return MEAL_STATUS_LABELS[meal.status] ?? meal.status
  }

  createCurrentWeek(): void {
    const startsOn = mondayOfCurrentWeek()
    this.creatingWeek.set(true)
    this.error.set('')
    this.api.post<WeekDto>('/weeks', { startsOn }).subscribe({
      next: (week) => {
        this.weeks.update((weeks) => [week, ...weeks.filter((item) => item.id !== week.id)])
        this.selectedWeekId.set(week.id)
        this.creatingWeek.set(false)
        this.loadTodayNutrition()
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.creatingWeek.set(false)
      },
    })
  }

  private loadTodayNutrition(): void {
    const day = this.today()
    this.nutritionTarget.set(null)
    this.nutritionError.set('')
    if (!day) return
    const dayPlanId = day.id
    this.api.get<DailyNutritionTargetDto>(`/nutrition/calculations/day/${dayPlanId}`).subscribe({
      next: (target) => {
        if (this.today()?.id === dayPlanId) this.nutritionTarget.set(target)
      },
      error: (error: unknown) => {
        if (this.today()?.id === dayPlanId) this.nutritionError.set(apiErrorMessage(error))
      },
    })
  }

  private loadMealNames(): void {
    this.catalogueError.set('')
    this.api.get<RecipeDto[]>('/recipes').subscribe({
      next: (recipes) => {
        this.recipeNames.set(Object.fromEntries(recipes.map((recipe) => [recipe.id, recipe.name])))
      },
      error: (error: unknown) => this.catalogueError.set(apiErrorMessage(error)),
    })
    this.api.get<ComposedMealDto[]>('/composed-meals').subscribe({
      next: (meals) => {
        this.composedMealNames.set(Object.fromEntries(meals.map((meal) => [meal.id, meal.name])))
      },
      error: (error: unknown) => this.catalogueError.set(apiErrorMessage(error)),
    })
  }
}

function localDateKey(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function mondayOfCurrentWeek(): string {
  const today = new Date()
  const monday = new Date(today.getFullYear(), today.getMonth(), today.getDate())
  monday.setDate(monday.getDate() + (monday.getDay() === 0 ? -6 : 1 - monday.getDay()))
  return localDateKey(monday)
}
