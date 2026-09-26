import { Component, OnInit, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import {
  AddComposedMealPartRequest,
  ComposedMealDto,
  ComposedMealRequest,
  RecipeDto,
} from '@/app/core/api/api.models'

@Component({
  selector: 'app-composed-meals-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './composed-meal-page.component.html',
  styles: [`
    .composed-meals-page { gap: 28px; }
    .meals-workspace { display: grid; grid-template-columns: minmax(260px, .8fr) minmax(0, 1.6fr); gap: 20px; align-items: start; }
    .meals-directory, .meal-editor { min-width: 0; padding: 22px; border: 1px solid var(--lifeos-line); border-radius: 22px; background: var(--lifeos-surface); box-shadow: 0 8px 26px rgb(68 55 35 / 7%); }
    .meals-directory { display: grid; gap: 14px; }
    .meals-directory-heading, .meal-row, .meal-section-heading, .meal-part-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .meals-directory-heading h2, .meal-section-heading h3 { margin: 0; font-size: 1.05rem; }
    .meal-row { width: 100%; padding: 13px 14px; border: 1px solid transparent; border-radius: 14px; background: var(--lifeos-surface-soft); color: var(--lifeos-text); text-align: left; cursor: pointer; }
    .meal-row:hover, .meal-row.is-selected { border-color: var(--lifeos-accent); background: var(--lifeos-accent-soft); }
    .meal-row span { min-width: 0; font-weight: 800; overflow-wrap: anywhere; }
    .meal-row small { color: var(--lifeos-text-soft); white-space: nowrap; }
    .meal-directory-list, .meal-editor-content, .meal-parts { display: grid; gap: 12px; }
    .meal-editor { display: grid; gap: 22px; }
    .meal-editor-heading { display: flex; flex-wrap: wrap; align-items: start; justify-content: space-between; gap: 12px; }
    .meal-editor-heading h2 { margin: 0; font-size: 1.3rem; }
    .meal-field { display: grid; gap: 7px; color: var(--lifeos-text); font-weight: 700; }
    .meal-field > span { color: var(--lifeos-text-soft); font-size: .78rem; font-weight: 800; letter-spacing: .03em; text-transform: uppercase; }
    .meal-field input, .meal-field select { width: 100%; min-height: 2.8rem; padding: .65rem .75rem; border: 1px solid var(--lifeos-line); border-radius: 9px; background: #fff; color: var(--lifeos-text); font: inherit; }
    .meal-section { display: grid; gap: 12px; padding-top: 18px; border-top: 1px solid var(--lifeos-line); }
    .meal-part-row { flex-wrap: wrap; padding: 12px 14px; border-radius: 13px; background: var(--lifeos-surface-soft); }
    .meal-part-row strong { overflow-wrap: anywhere; }
    .meal-part-row span, .meal-muted, .meal-empty { color: var(--lifeos-text-soft); }
    .meal-empty { margin: 0; line-height: 1.5; }
    .meal-error { margin: 0; padding: 12px 14px; border-radius: 12px; background: #fff0ed; color: #8f2017; font-weight: 700; }
    .meal-feedback { margin: 0; color: var(--lifeos-accent-strong); font-weight: 700; }
    .meal-inline-form { display: grid; grid-template-columns: minmax(0, 1fr) minmax(120px, .55fr) auto; align-items: end; gap: 10px; }
    .meal-form-actions { display: flex; flex-wrap: wrap; gap: 10px; }
    @media (max-width: 850px) { .meals-workspace { grid-template-columns: minmax(0, 1fr); } }
    @media (max-width: 560px) { .meal-inline-form { grid-template-columns: minmax(0, 1fr); } .meals-directory, .meal-editor { padding: 17px; } }
  `],
})
export class ComposedMealsPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)

  readonly meals = signal<ComposedMealDto[]>([])
  readonly recipes = signal<RecipeDto[]>([])
  readonly selectedMeal = signal<ComposedMealDto | null>(null)
  readonly loadingMeals = signal(false)
  readonly loadingRecipes = signal(false)
  readonly saving = signal(false)
  readonly error = signal<string | null>(null)
  readonly feedback = signal('')
  readonly editingId = signal<string | null>(null)
  readonly mealName = signal('')
  readonly selectedRecipeId = signal('')
  readonly quantityFactor = signal(1)

  ngOnInit(): void {
    this.loadMeals()
    this.loadRecipes()
  }

  get mealNameValue(): string {
    return this.mealName()
  }

  set mealNameValue(value: string) {
    this.mealName.set(value)
  }

  get selectedRecipeIdValue(): string {
    return this.selectedRecipeId()
  }

  set selectedRecipeIdValue(value: string) {
    this.selectedRecipeId.set(value)
  }

  get quantityFactorValue(): number {
    return this.quantityFactor()
  }

  set quantityFactorValue(value: number) {
    this.quantityFactor.set(value)
  }

  recipeName(recipeId: string): string {
    return this.recipes().find((recipe) => recipe.id === recipeId)?.name ?? `Recette ${recipeId}`
  }

  startNewMeal(): void {
    this.editingId.set(null)
    this.selectedMeal.set(null)
    this.mealName.set('')
    this.error.set(null)
    this.feedback.set('')
  }

  selectMeal(id: string): void {
    this.error.set(null)
    this.feedback.set('')
    this.loadingMeals.set(true)
    this.api.get<ComposedMealDto>(`/composed-meals/${encodeURIComponent(id)}`).subscribe({
      next: (meal) => {
        this.selectedMeal.set(meal)
        this.editingId.set(meal.id)
        this.mealName.set(meal.name)
        this.loadingMeals.set(false)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.loadingMeals.set(false)
      },
    })
  }

  saveMeal(): void {
    const name = this.mealName().trim()
    if (!name) {
      this.error.set('Saisissez un nom pour le repas composé.')
      return
    }
    const request: ComposedMealRequest = { name }
    const id = this.editingId()
    this.saving.set(true)
    this.error.set(null)
    this.feedback.set('')
    const request$ = id
      ? this.api.put<ComposedMealDto>(`/composed-meals/${encodeURIComponent(id)}`, request)
      : this.api.post<ComposedMealDto>('/composed-meals', request)
    request$.subscribe({
      next: (meal) => {
        this.saving.set(false)
        this.feedback.set(id ? 'Repas composé mis à jour.' : 'Repas composé créé.')
        this.loadMeals(meal?.id ?? id ?? undefined)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  addPart(): void {
    const meal = this.selectedMeal()
    const recipeId = this.selectedRecipeId()
    const quantityFactor = Number(this.quantityFactor())
    if (!meal) {
      this.error.set('Enregistrez ou sélectionnez un repas composé avant d’ajouter une recette.')
      return
    }
    if (!recipeId || !Number.isFinite(quantityFactor) || quantityFactor <= 0) {
      this.error.set('Choisissez une recette et indiquez un facteur de quantité supérieur à zéro.')
      return
    }

    const request: AddComposedMealPartRequest = { recipeId, quantityFactor }
    this.saving.set(true)
    this.error.set(null)
    this.api.post<ComposedMealDto>(`/composed-meals/${encodeURIComponent(meal.id)}/parts`, request).subscribe({
      next: () => {
        this.saving.set(false)
        this.quantityFactor.set(1)
        this.feedback.set('Recette ajoutée au repas composé.')
        this.loadMeals(meal.id)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  removePart(partId: string): void {
    const meal = this.selectedMeal()
    if (!meal) return
    this.saving.set(true)
    this.error.set(null)
    this.api.delete(`/composed-meals/${encodeURIComponent(meal.id)}/parts/${encodeURIComponent(partId)}`).subscribe({
      next: () => {
        this.saving.set(false)
        this.feedback.set('Recette retirée du repas composé.')
        this.loadMeals(meal.id)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  deleteMeal(): void {
    const meal = this.selectedMeal()
    if (!meal || !window.confirm(`Supprimer le repas composé « ${meal.name} » ?`)) return
    this.saving.set(true)
    this.error.set(null)
    this.api.delete(`/composed-meals/${encodeURIComponent(meal.id)}`).subscribe({
      next: () => {
        this.saving.set(false)
        this.startNewMeal()
        this.feedback.set('Repas composé supprimé.')
        this.loadMeals()
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  private loadMeals(selectId?: string): void {
    this.loadingMeals.set(true)
    this.api.get<ComposedMealDto[]>('/composed-meals').subscribe({
      next: (meals) => {
        this.meals.set(meals)
        this.loadingMeals.set(false)
        if (selectId) this.selectMeal(selectId)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.loadingMeals.set(false)
      },
    })
  }

  private loadRecipes(): void {
    this.loadingRecipes.set(true)
    this.api.get<RecipeDto[]>('/recipes').subscribe({
      next: (recipes) => {
        this.recipes.set(recipes)
        this.loadingRecipes.set(false)
        const firstRecipe = recipes.at(0)
        if (firstRecipe && !this.selectedRecipeId()) this.selectedRecipeId.set(firstRecipe.id)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.loadingRecipes.set(false)
      },
    })
  }
}
