import { Component, OnInit, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import {
  AddRecipeIngredientRequest,
  FoodItemDto,
  RecipeDto,
  RecipeRequest,
} from '@/app/core/api/api.models'

@Component({
  selector: 'app-recipes-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './recipes-page.component.html',
  styles: [`
    .recipes-page { gap: 28px; }
    .recipes-workspace { display: grid; grid-template-columns: minmax(260px, .8fr) minmax(0, 1.6fr); gap: 20px; align-items: start; }
    .recipes-directory, .recipe-editor { min-width: 0; padding: 22px; border: 1px solid var(--lifeos-line); border-radius: 22px; background: var(--lifeos-surface); box-shadow: 0 8px 26px rgb(68 55 35 / 7%); }
    .recipes-directory { display: grid; gap: 14px; }
    .recipes-directory-heading, .recipe-row, .recipe-section-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .recipes-directory-heading h2, .recipe-section-heading h3 { margin: 0; font-size: 1.05rem; }
    .recipe-row { width: 100%; padding: 13px 14px; border: 1px solid transparent; border-radius: 14px; background: var(--lifeos-surface-soft); color: var(--lifeos-text); text-align: left; cursor: pointer; }
    .recipe-row:hover, .recipe-row.is-selected { border-color: var(--lifeos-accent); background: var(--lifeos-accent-soft); }
    .recipe-row span { min-width: 0; font-weight: 800; overflow-wrap: anywhere; }
    .recipe-row small { color: var(--lifeos-text-soft); white-space: nowrap; }
    .recipe-directory-list, .recipe-editor-form, .recipe-editor-content, .recipe-ingredients { display: grid; gap: 12px; }
    .recipe-editor { display: grid; gap: 22px; }
    .recipe-editor-heading { display: flex; flex-wrap: wrap; align-items: start; justify-content: space-between; gap: 12px; }
    .recipe-editor-heading h2 { margin: 0; font-size: 1.3rem; }
    .recipe-editor-form { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .recipe-field { display: grid; min-width: 0; gap: 7px; color: var(--lifeos-text); font-weight: 700; }
    .recipe-field > span { color: var(--lifeos-text-soft); font-size: .78rem; font-weight: 800; letter-spacing: .03em; text-transform: uppercase; }
    .recipe-field input, .recipe-field textarea, .recipe-field select { width: 100%; min-height: 2.8rem; padding: .65rem .75rem; border: 1px solid var(--lifeos-line); border-radius: 9px; background: #fff; color: var(--lifeos-text); font: inherit; }
    .recipe-field textarea { min-height: 6rem; resize: vertical; }
    .recipe-field-wide, .recipe-form-actions, .recipe-feedback, .recipe-ingredients { grid-column: 1 / -1; }
    .recipe-form-actions, .recipe-inline-form { display: flex; flex-wrap: wrap; align-items: end; gap: 10px; }
    .recipe-inline-form .recipe-field { flex: 1 1 150px; }
    .recipe-section { display: grid; gap: 12px; padding-top: 18px; border-top: 1px solid var(--lifeos-line); }
    .recipe-ingredient-row { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px 16px; padding: 12px 14px; border-radius: 13px; background: var(--lifeos-surface-soft); }
    .recipe-ingredient-row strong { overflow-wrap: anywhere; }
    .recipe-ingredient-row span, .recipe-muted { color: var(--lifeos-text-soft); }
    .recipe-feedback { margin: 0; color: var(--lifeos-accent-strong); font-weight: 700; }
    .recipe-error { margin: 0; padding: 12px 14px; border-radius: 12px; background: #fff0ed; color: #8f2017; font-weight: 700; }
    .recipe-empty { margin: 0; color: var(--lifeos-text-soft); line-height: 1.5; }
    .recipe-form-actions .p-button { min-height: 2.65rem; }
    @media (max-width: 850px) { .recipes-workspace { grid-template-columns: minmax(0, 1fr); } }
    @media (max-width: 560px) { .recipe-editor-form { grid-template-columns: minmax(0, 1fr); } .recipe-field-wide, .recipe-form-actions, .recipe-feedback, .recipe-ingredients { grid-column: auto; } .recipes-directory, .recipe-editor { padding: 17px; } }
  `],
})
export class RecipesPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)

  readonly recipes = signal<RecipeDto[]>([])
  readonly foodItems = signal<FoodItemDto[]>([])
  readonly selectedRecipe = signal<RecipeDto | null>(null)
  readonly loading = signal(false)
  readonly loadingFoodItems = signal(false)
  readonly saving = signal(false)
  readonly error = signal<string | null>(null)
  readonly feedback = signal('')
  readonly editingId = signal<string | null>(null)
  readonly recipeName = signal('')
  readonly servings = signal(2)
  readonly durationMinutes = signal(30)
  readonly tagsText = signal('')
  readonly metadataText = signal('')
  readonly ingredientFoodItemId = signal('')
  readonly ingredientQuantity = signal(1)
  readonly ingredientUnit = signal('')

  ngOnInit(): void {
    this.loadRecipes()
    this.loadFoodItems()
  }

  get recipeNameValue(): string {
    return this.recipeName()
  }

  set recipeNameValue(value: string) {
    this.recipeName.set(value)
  }

  get servingsValue(): number {
    return this.servings()
  }

  set servingsValue(value: number) {
    this.servings.set(value)
  }

  get durationValue(): number {
    return this.durationMinutes()
  }

  set durationValue(value: number) {
    this.durationMinutes.set(value)
  }

  get tagsValue(): string {
    return this.tagsText()
  }

  set tagsValue(value: string) {
    this.tagsText.set(value)
  }

  get metadataValue(): string {
    return this.metadataText()
  }

  set metadataValue(value: string) {
    this.metadataText.set(value)
  }

  get ingredientFoodItemIdValue(): string {
    return this.ingredientFoodItemId()
  }

  set ingredientFoodItemIdValue(value: string) {
    this.ingredientFoodItemId.set(value)
  }

  get ingredientQuantityValue(): number {
    return this.ingredientQuantity()
  }

  set ingredientQuantityValue(value: number) {
    this.ingredientQuantity.set(value)
  }

  get ingredientUnitValue(): string {
    return this.ingredientUnit()
  }

  set ingredientUnitValue(value: string) {
    this.ingredientUnit.set(value)
  }

  foodItemName(id: string): string {
    return this.foodItems().find((food) => food.id === id)?.name ?? `Aliment indisponible · ${id}`
  }

  startNewRecipe(): void {
    this.editingId.set(null)
    this.selectedRecipe.set(null)
    this.recipeName.set('')
    this.servings.set(2)
    this.durationMinutes.set(30)
    this.tagsText.set('')
    this.metadataText.set('')
    this.error.set(null)
    this.feedback.set('')
  }

  selectRecipe(id: string): void {
    this.error.set(null)
    this.feedback.set('')
    this.loading.set(true)
    this.api.get<RecipeDto>(`/recipes/${encodeURIComponent(id)}`).subscribe({
      next: (recipe) => {
        this.selectedRecipe.set(recipe)
        this.editingId.set(recipe.id)
        this.recipeName.set(recipe.name)
        this.servings.set(recipe.servings)
        this.durationMinutes.set(recipe.durationMinutes)
        this.tagsText.set(recipe.tags.join(', '))
        this.metadataText.set(recipe.metadata ? JSON.stringify(recipe.metadata, null, 2) : '')
        this.loading.set(false)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.loading.set(false)
      },
    })
  }

  saveRecipe(): void {
    const name = this.recipeName().trim()
    const servings = Number(this.servings())
    const durationMinutes = Number(this.durationMinutes())
    if (!name || name.length > 200 || !Number.isFinite(servings) || servings < 1
      || !Number.isFinite(durationMinutes) || durationMinutes < 0) {
      this.error.set('Vérifiez le nom (200 caractères maximum), les portions (au moins 1) et la durée (0 minute ou plus).')
      return
    }

    let metadata: Record<string, unknown> | null = null
    const metadataText = this.metadataText().trim()
    if (metadataText) {
      try {
        const parsed: unknown = JSON.parse(metadataText)
        if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
          this.error.set('Les métadonnées doivent être un objet JSON.')
          return
        }
        metadata = parsed as Record<string, unknown>
      } catch {
        this.error.set('Les métadonnées contiennent un JSON invalide.')
        return
      }
    }

    const request: RecipeRequest = {
      name,
      servings,
      durationMinutes,
      tags: this.tagsText().split(',').map((tag) => tag.trim()).filter(Boolean),
      metadata,
    }
    const id = this.editingId()
    this.saving.set(true)
    this.error.set(null)
    this.feedback.set('')
    const request$ = id
      ? this.api.put<RecipeDto>(`/recipes/${encodeURIComponent(id)}`, request)
      : this.api.post<RecipeDto>('/recipes', request)
    request$.subscribe({
      next: (recipe) => {
        this.saving.set(false)
        this.feedback.set(id ? 'Recette mise à jour.' : 'Recette créée.')
        this.loadRecipes(recipe?.id ?? id ?? undefined)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  addIngredient(): void {
    const foodItemId = this.ingredientFoodItemId().trim()
    const quantity = Number(this.ingredientQuantity())
    const unit = this.ingredientUnit().trim()
    const recipe = this.selectedRecipe()
    if (!recipe) {
      this.error.set('Enregistrez ou sélectionnez une recette avant d’ajouter un ingrédient.')
      return
    }
    if (!this.foodItems().some((food) => food.id === foodItemId) || !unit || !Number.isFinite(quantity) || quantity <= 0) {
      this.error.set('Choisissez un aliment du catalogue, une quantité supérieure à zéro et une unité.')
      return
    }

    const request: AddRecipeIngredientRequest = { foodItemId, quantity, unit }
    this.saving.set(true)
    this.error.set(null)
    this.api.post<RecipeDto>(`/recipes/${encodeURIComponent(recipe.id)}/ingredients`, request).subscribe({
      next: () => {
        this.saving.set(false)
        this.ingredientFoodItemId.set('')
        this.ingredientQuantity.set(1)
        this.ingredientUnit.set('')
        this.feedback.set('Ingrédient ajouté.')
        this.loadRecipes(recipe.id)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  deleteRecipe(): void {
    const recipe = this.selectedRecipe()
    if (!recipe || !window.confirm(`Supprimer la recette « ${recipe.name} » ?`)) return
    this.saving.set(true)
    this.error.set(null)
    this.api.delete(`/recipes/${encodeURIComponent(recipe.id)}`).subscribe({
      next: () => {
        this.saving.set(false)
        this.startNewRecipe()
        this.feedback.set('Recette supprimée.')
        this.loadRecipes()
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  private loadRecipes(selectId?: string): void {
    this.loading.set(true)
    this.api.get<RecipeDto[]>('/recipes').subscribe({
      next: (recipes) => {
        this.recipes.set(recipes)
        this.loading.set(false)
        if (selectId) this.selectRecipe(selectId)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.loading.set(false)
      },
    })
  }

  private loadFoodItems(): void {
    this.loadingFoodItems.set(true)
    this.api.get<FoodItemDto[]>('/food-items').subscribe({
      next: (foodItems) => {
        this.foodItems.set(foodItems)
        this.loadingFoodItems.set(false)
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.loadingFoodItems.set(false)
      },
    })
  }
}
