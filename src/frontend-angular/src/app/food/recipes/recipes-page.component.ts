import { Component, OnInit, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { HouseholdReviewsComponent } from '@/app/shared/reviews/household-reviews.component'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import type { FoodItemDto, MemberProfileDto } from '@/app/core/api/api.models'
import type { RecipeDto, RecipeIngredientDto } from './recipe.models'
import { buildIngredientRequest, buildRecipeRequest, visibleRecipes } from './recipe-form'
import { RecipesApiService } from './recipes-api.service'


@Component({
  selector: 'app-recipes-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent, HouseholdReviewsComponent],
  templateUrl: './recipes-page.component.html',
  styleUrl: './recipes-page.component.css',
})
export class RecipesPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)
  private readonly recipesApi = inject(RecipesApiService)

  readonly recipes = signal<RecipeDto[]>([])
  readonly foodItems = signal<FoodItemDto[]>([])
  readonly profiles = signal<MemberProfileDto[]>([])
  search = ''
  visibleRecipes(): RecipeDto[] { return visibleRecipes(this.recipes(), this.search) }
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
    this.instructionsText.set(recipe?.instructions ?? '')
    this.showMore = !!this.tagsText()
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
    this.recipesApi.get(id).subscribe({
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
    const built = buildRecipeRequest({
      name: this.recipeName(), servings: this.servings(), durationMinutes: this.durationMinutes(),
      tagsText: this.tagsText(), instructions: this.instructionsText(),
    })
    if (!built.ok) {
      this.dialogError.set(built.error)
      return
    }
    const request = built.value
    const id = this.editingId()
    this.saving.set(true)
    this.dialogError.set('')
    this.feedback.set('')
    const request$ = id ? this.recipesApi.update(id, request) : this.recipesApi.create(request)
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
    const built = buildIngredientRequest(this.ingredientQuantity(), this.ingredientUnit())
    const recipe = this.selectedRecipe()
    if (!recipe) {
      this.dialogError.set('Enregistrez ou sélectionnez une recette avant d’ajouter un ingrédient.')
      return
    }
    if (!this.activeFoods().some((food) => food.id === foodItemId) || !built.ok) {
      this.dialogError.set('Choisissez un aliment du catalogue, une quantité supérieure à zéro et une unité.')
      return
    }
    this.saving.set(true)
    this.dialogError.set('')
    this.recipesApi.addIngredient(recipe.id, { foodItemId, ...built.value }).subscribe({
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
    if (!recipe || !window.confirm(`Archiver la recette « ${recipe.name} » ?`)) return
    this.saving.set(true)
    this.error.set(null)
    this.recipesApi.archive(recipe.id).subscribe({
      next: () => {
        this.saving.set(false)
        this.selectedRecipe.set(null)
        this.feedback.set('Recette archivée.')
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
    const built = buildIngredientRequest(edit.quantity, edit.unit)
    if (!remove && !built.ok) {
      this.dialogError.set(built.error)
      return
    }
    this.saving.set(true)
    this.dialogError.set('')
    const request = remove || !built.ok
      ? this.recipesApi.removeIngredient(recipe.id, edit.id)
      : this.recipesApi.updateIngredient(recipe.id, edit.id, built.value)
    request.subscribe({
      // Ingredient calls return the whole recipe: no reload needed.
      next: (updated) => {
        this.saving.set(false)
        this.closeDialog()
        this.feedback.set(remove ? 'Ingrédient retiré.' : 'Ingrédient modifié.')
        this.selectedRecipe.set(updated)
        this.recipes.update(items => items.map(item => item.id === updated.id ? updated : item))
      },
      error: (error: unknown) => {
        this.saving.set(false)
        this.dialogError.set(apiErrorMessage(error))
      },
    })
  }

  private loadRecipes(selectId?: string): void {
    this.loading.set(true)
    this.recipesApi.list().subscribe({
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
  }
}
