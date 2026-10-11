import { Component, OnInit, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { HouseholdReviewsComponent } from '@/app/shared/reviews/household-reviews.component'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import {
  AddRecipeIngredientRequest,
  FoodItemDto,
  RecipeDto,
  RecipeRequest,
  RecipeIngredientDto,
  MemberProfileDto,
  HouseholdGroupDto,
} from '@/app/core/api/api.models'

@Component({
  selector: 'app-recipes-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent, HouseholdReviewsComponent],
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
        .recipe-field { display: grid; min-width: 0; gap: 7px; color: var(--lifeos-text); font-weight: 700; }
    .recipe-field > span { color: var(--lifeos-text-soft); font-size: .78rem; font-weight: 800; letter-spacing: .03em; text-transform: uppercase; }
    .recipe-field input, .recipe-field textarea, .recipe-field select { width: 100%; min-height: 2.8rem; padding: .65rem .75rem; border: 1px solid var(--lifeos-line); border-radius: 9px; background: #fff; color: var(--lifeos-text); font: inherit; }
    .recipe-field textarea { min-height: 6rem; resize: vertical; }
    .recipe-field-wide, .recipe-form-actions, .recipe-feedback, .recipe-ingredients { grid-column: 1 / -1; }
    .recipe-form-actions { display: flex; flex-wrap: wrap; align-items: end; gap: 10px; }
    .recipe-section { display: grid; gap: 12px; padding-top: 18px; border-top: 1px solid var(--lifeos-line); }
    .recipe-ingredient-row { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px 16px; padding: 12px 14px; border-radius: 13px; background: var(--lifeos-surface-soft); }
    .recipe-ingredient-row strong { overflow-wrap: anywhere; }
    .recipe-ingredient-row span, .recipe-muted { color: var(--lifeos-text-soft); }
    .recipe-feedback { margin: 0; color: var(--lifeos-accent-strong); font-weight: 700; }
    .recipe-error { margin: 0; padding: 12px 14px; border-radius: 12px; background: #fff0ed; color: #8f2017; font-weight: 700; }
    .recipe-empty { margin: 0; color: var(--lifeos-text-soft); line-height: 1.5; }
    .recipe-form-actions .lifeos-button { min-height: 2.65rem; }
    @media (max-width: 850px) { .recipes-workspace { grid-template-columns: minmax(0, 1fr); } }
    @media (max-width: 560px) { .recipe-editor-form { grid-template-columns: minmax(0, 1fr); } .recipe-field-wide, .recipe-form-actions, .recipe-feedback, .recipe-ingredients { grid-column: auto; } .recipes-directory, .recipe-editor { padding: 17px; } }
  `],
})
export class RecipesPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)

  readonly recipes = signal<RecipeDto[]>([])
  readonly foodItems = signal<FoodItemDto[]>([])
  readonly profiles = signal<MemberProfileDto[]>([])
  readonly groups = signal<HouseholdGroupDto[]>([])
  search = ''
  visibleRecipes(): RecipeDto[] {
    return this.recipes().filter(r => !r.isArchived && r.name.toLocaleLowerCase('fr').includes(this.search.toLocaleLowerCase('fr')))
  }
  activeFoods(): FoodItemDto[] { return this.foodItems().filter(f => !f.isArchived) }
  ingredientEdit: { id: string; foodItemId: string; quantity: number; unit: string } | null = null
  readonly dialog = signal<'recipe' | 'ingredient' | 'editIngredient' | null>(null)
  readonly dialogError = signal('')
  showMore = false
  private initialSnapshot = ''
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
  readonly instructionsText = signal('')
  readonly ingredientReviewFoodId = signal('')
  readonly ingredientFoodItemId = signal('')
  readonly ingredientQuantity = signal(1)
  readonly ingredientUnit = signal('')

  ngOnInit(): void {
    this.loadRecipes()
    this.loadFoodItems()
    this.loadHouseholdTargets()
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

  get instructionsValue(): string { return this.instructionsText() }
  set instructionsValue(value: string) { this.instructionsText.set(value) }

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

  openRecipeDialog(recipe: RecipeDto | null): void {
    this.editingId.set(recipe?.id ?? null)
    this.recipeName.set(recipe?.name ?? '')
    this.servings.set(recipe?.servings ?? 2)
    this.durationMinutes.set(recipe?.durationMinutes ?? 30)
    this.tagsText.set(recipe?.tags.join(', ') ?? '')
    this.metadataText.set(recipe?.metadata ? JSON.stringify(recipe.metadata, null, 2) : '')
    this.instructionsText.set(recipe?.instructions ?? '')
    this.showMore = !!(this.tagsText() || this.metadataText())
    this.openDialog('recipe')
  }

  openIngredientDialog(): void {
    this.ingredientFoodItemId.set('')
    this.ingredientReviewFoodId.set('')
    this.ingredientQuantity.set(1)
    this.ingredientUnit.set('')
    this.openDialog('ingredient')
  }

  isDirty(): boolean {
    return hasChanges(this.initialSnapshot, this.dialogValues())
  }

  closeDialog(): void {
    this.dialog.set(null)
    this.dialogError.set('')
    this.ingredientEdit = null
  }

  private openDialog(kind: 'recipe' | 'ingredient' | 'editIngredient'): void {
    this.dialog.set(kind)
    this.dialogError.set('')
    this.error.set(null)
    this.feedback.set('')
    this.initialSnapshot = formSnapshot(this.dialogValues())
  }

  private dialogValues(): unknown {
    switch (this.dialog()) {
      case 'recipe':
        return [this.recipeName(), this.servings(), this.durationMinutes(), this.tagsText(), this.instructionsText()]
      case 'ingredient':
        return [this.ingredientFoodItemId(), this.ingredientQuantity(), this.ingredientUnit()]
      case 'editIngredient':
        return this.ingredientEdit
      default:
        return null
    }
  }

  selectRecipe(id: string): void {
    this.error.set(null)
    this.feedback.set('')
    this.loading.set(true)
    this.api.get<RecipeDto>(`/recipes/${encodeURIComponent(id)}`).subscribe({
      next: (recipe) => {
        this.selectedRecipe.set(recipe)
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
      this.dialogError.set('Vérifiez le nom (200 caractères maximum), les portions (au moins 1) et la durée (0 minute ou plus).')
      return
    }

    const request: RecipeRequest = {
      name,
      servings,
      durationMinutes,
      tags: this.tagsText().split(',').map((tag) => tag.trim()).filter(Boolean),
      metadata: null,
      instructions: this.instructionsText().trim() || null,
    }
    const id = this.editingId()
    this.saving.set(true)
    this.dialogError.set('')
    this.feedback.set('')
    const request$ = id
      ? this.api.put<RecipeDto>(`/recipes/${encodeURIComponent(id)}`, request)
      : this.api.post<RecipeDto>('/recipes', request)
    request$.subscribe({
      next: (recipe) => {
        this.saving.set(false)
        this.closeDialog()
        this.feedback.set(id ? 'Recette mise à jour.' : 'Recette créée.')
        this.loadRecipes(recipe?.id ?? id ?? undefined)
      },
      error: (error: unknown) => {
        this.dialogError.set(apiErrorMessage(error))
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
      this.dialogError.set('Enregistrez ou sélectionnez une recette avant d’ajouter un ingrédient.')
      return
    }
    if (!this.activeFoods().some((food) => food.id === foodItemId) || !unit || !Number.isFinite(quantity) || quantity <= 0) {
      this.dialogError.set('Choisissez un aliment du catalogue, une quantité supérieure à zéro et une unité.')
      return
    }

    const request: AddRecipeIngredientRequest = { foodItemId, quantity, unit }
    this.saving.set(true)
    this.dialogError.set('')
    this.api.post<RecipeDto>(`/recipes/${encodeURIComponent(recipe.id)}/ingredients`, request).subscribe({
      next: () => {
        this.saving.set(false)
        this.ingredientFoodItemId.set('')
        this.ingredientQuantity.set(1)
        this.ingredientUnit.set('')
        this.closeDialog()
        this.feedback.set('Ingrédient ajouté.')
        this.loadRecipes(recipe.id)
      },
      error: (error: unknown) => {
        this.dialogError.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  deleteRecipe(): void {
    const recipe = this.selectedRecipe()
    if (!recipe || !window.confirm(`Archiver la recette « ${recipe.name} » ? Les repas existants restent conservés.`)) return
    this.saving.set(true)
    this.error.set(null)
    this.api.delete(`/recipes/${encodeURIComponent(recipe.id)}`).subscribe({
      next: () => {
        this.saving.set(false)
        this.selectedRecipe.set(null)
        this.feedback.set('Recette archivée. Les repas existants sont conservés.')
        this.loadRecipes()
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error))
        this.saving.set(false)
      },
    })
  }

  editIngredient(ingredient: RecipeIngredientDto): void {
    this.ingredientEdit = { id: ingredient.id, foodItemId: ingredient.foodItemId, quantity: ingredient.quantity, unit: ingredient.unit }
    this.openDialog('editIngredient')
  }

  saveIngredient(remove = false): void {
    const recipe = this.selectedRecipe()
    const edit = this.ingredientEdit
    if (!recipe || !edit || this.saving()) return
    if (!remove && (!Number.isFinite(Number(edit.quantity)) || Number(edit.quantity) <= 0 || !edit.unit.trim())) {
      this.dialogError.set('Indiquez une quantité supérieure à zéro et une unité.')
      return
    }
    this.saving.set(true)
    this.dialogError.set('')
    const path = `/recipes/${recipe.id}/ingredients/${edit.id}`
    const request = remove ? this.api.delete(path) : this.api.put(path, { id: edit.id, quantity: edit.quantity, unit: edit.unit })
    request.subscribe({
      next: () => {
        this.saving.set(false)
        this.closeDialog()
        this.feedback.set(remove ? 'Ingrédient retiré.' : 'Ingrédient modifié.')
        this.loadRecipes(recipe.id)
      },
      error: (error: unknown) => {
        this.saving.set(false)
        this.dialogError.set(apiErrorMessage(error))
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
    this.api.get<FoodItemDto[]>('/foods').subscribe({
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

  private loadHouseholdTargets(): void {
    this.api.get<{ memberProfiles: MemberProfileDto[] }>('/households/current').subscribe({
      next: household => this.profiles.set(household.memberProfiles),
      error: (error: unknown) => this.error.set(apiErrorMessage(error)),
    })
    this.api.get<HouseholdGroupDto[]>('/household-groups').subscribe({
      next: groups => this.groups.set(groups),
      error: (error: unknown) => this.error.set(apiErrorMessage(error)),
    })
  }
}
