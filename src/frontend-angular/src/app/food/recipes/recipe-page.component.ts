import { Component, computed, inject, input, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import type { MemberProfileDto } from '@/app/core/api/api.models'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { HouseholdReviewsComponent } from '@/app/shared/reviews/household-reviews.component'
import { ReviewDraftsService } from '@/app/shared/reviews/review-drafts.service'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, IngredientUnit, StoreDto } from '../food.models'
import { FoodImageComponent } from '../shared/food-image.component'
import { fodmapLabel, formatNumber, formatQuantity } from '../shared/format'
import { costView } from './recipe-cost'
import { RecipeEditorComponent } from './recipe-editor.component'
import { buildIngredientRequest, INGREDIENT_UNITS, recipeSteps } from './recipe-form'
import { recipeNutrition } from './recipe-nutrition'
import type { RecipeCostDto, RecipeDto, RecipeIngredientDto } from './recipe.models'
import { RecipesApiService } from './recipes-api.service'

interface IngredientDraft { id: string; quantity: string; unit: IngredientUnit }

@Component({
  selector: 'app-recipe-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent, FoodImageComponent, HouseholdReviewsComponent, RecipeEditorComponent],
  templateUrl: './recipe-page.component.html',
  styleUrl: './recipes.css',
})
export class RecipePageComponent implements OnInit {
  private readonly recipesApi = inject(RecipesApiService)
  private readonly foodApi = inject(FoodApiService)
  private readonly router = inject(Router)
  private readonly reviewDrafts = inject(ReviewDraftsService)
  /** Route parameter. */
  readonly id = input.required<string>()

  readonly recipe = signal<RecipeDto | null>(null)
  readonly foods = signal<FoodDto[]>([])
  readonly profiles = signal<MemberProfileDto[]>([])
  readonly stores = signal<StoreDto[]>([])
  readonly cost = signal<RecipeCostDto | null>(null)
  readonly costError = signal('')
  readonly loading = signal(true)
  readonly error = signal('')
  readonly actionError = signal('')
  readonly busy = signal(false)
  readonly editing = signal(false)
  readonly editingIngredients = signal(false)
  readonly ingredientError = signal('')
  drafts: IngredientDraft[] = []
  newIngredient = { foodItemId: '', quantity: '', unit: 'g' as IngredientUnit }

  readonly units = INGREDIENT_UNITS
  readonly fodmapLabel = fodmapLabel
  readonly steps = computed(() => recipeSteps(this.recipe()?.instructions ?? null))
  readonly nutrition = computed(() => {
    const recipe = this.recipe()
    return recipe ? recipeNutrition(recipe, this.foods()) : null
  })
  readonly activeFoods = computed(() => this.foods().filter(food => !food.isArchived))
  readonly costLines = computed(() => {
    const cost = this.cost()
    return cost ? costView(cost, this.foods(), this.stores()) : null
  })

  async ngOnInit(): Promise<void> {
    try {
      const [recipe, foods, profiles, stores] = await Promise.all([
        firstValueFrom(this.recipesApi.get(this.id())),
        firstValueFrom(this.foodApi.foods()),
        firstValueFrom(this.foodApi.profiles()),
        firstValueFrom(this.foodApi.stores()),
      ])
      this.recipe.set(recipe)
      this.foods.set(foods)
      this.profiles.set(profiles)
      this.stores.set(stores)
      void this.loadCost()
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  canLeave(): boolean { return !this.busy() && this.reviewDrafts.canLeave() }

  meta(recipe: RecipeDto): string {
    return `${recipe.servings} portion${recipe.servings > 1 ? 's' : ''} · ${recipe.durationMinutes} min`
  }

  food(id: string): FoodDto | undefined { return this.foods().find(food => food.id === id) }
  quantity(ingredient: RecipeIngredientDto): string { return formatQuantity(ingredient.quantity, ingredient.unit) }
  number(value: number): string { return formatNumber(value, value < 10 ? 1 : 0) }

  edited(recipe: RecipeDto): void {
    this.recipe.set(recipe)
    this.editing.set(false)
    void this.loadCost()
  }

  /** The cost is computed on read; it is reloaded after each change of ingredients or yield. */
  async loadCost(): Promise<void> {
    const recipe = this.recipe()
    if (!recipe || recipe.isArchived) return
    try {
      this.cost.set(await firstValueFrom(this.recipesApi.cost(recipe.id)))
      this.costError.set('')
    } catch (error) {
      this.costError.set(apiErrorMessage(error))
    }
  }

  async changeImage(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    const recipe = this.recipe()
    input.value = ''
    if (!file || !recipe) return
    await this.run(async () => this.recipe.set(await firstValueFrom(this.foodApi.uploadImage<RecipeDto>('recipes', recipe.id, file))))
  }

  async removeImage(): Promise<void> {
    const recipe = this.recipe()
    if (!recipe || !window.confirm('Retirer la photo de cette recette ?')) return
    await this.run(async () => {
      await firstValueFrom(this.foodApi.removeImage('recipes', recipe.id))
      this.recipe.set({ ...recipe, hasImage: false, updatedAt: new Date().toISOString() })
    })
  }

  async archive(): Promise<void> {
    const recipe = this.recipe()
    if (!recipe || !window.confirm(`Archiver « ${recipe.name} » ? Elle disparaît de la liste des recettes.`)) return
    await this.run(async () => {
      await firstValueFrom(this.recipesApi.archive(recipe.id))
      await this.router.navigate(['/recipes'])
    })
  }

  openIngredients(): void {
    const recipe = this.recipe()
    if (!recipe) return
    this.drafts = recipe.ingredients.map(i => ({ id: i.id, quantity: String(i.quantity).replace('.', ','), unit: i.unit }))
    this.newIngredient = { foodItemId: '', quantity: '', unit: 'g' }
    this.ingredientError.set('')
    this.editingIngredients.set(true)
  }

  async addIngredient(): Promise<void> {
    const recipe = this.recipe()
    const request = buildIngredientRequest(this.newIngredient.quantity, this.newIngredient.unit)
    if (!recipe) return
    if (!this.newIngredient.foodItemId) { this.ingredientError.set('Choisissez un aliment.'); return }
    if (!request.ok) { this.ingredientError.set(request.error); return }
    await this.ingredientAction(async () => {
      const updated = await firstValueFrom(this.recipesApi.addIngredient(recipe.id, { foodItemId: this.newIngredient.foodItemId, ...request.value }))
      const added = updated.ingredients.find(i => !this.drafts.some(d => d.id === i.id))
      if (added) this.drafts.push({ id: added.id, quantity: String(added.quantity).replace('.', ','), unit: added.unit })
      this.newIngredient = { foodItemId: '', quantity: '', unit: 'g' }
      return updated
    })
  }

  async updateIngredient(draft: IngredientDraft): Promise<void> {
    const recipe = this.recipe()
    const request = buildIngredientRequest(draft.quantity, draft.unit)
    if (!recipe) return
    if (!request.ok) { this.ingredientError.set(request.error); return }
    await this.ingredientAction(() => firstValueFrom(this.recipesApi.updateIngredient(recipe.id, draft.id, request.value)))
  }

  async removeIngredient(draft: IngredientDraft): Promise<void> {
    const recipe = this.recipe()
    if (!recipe) return
    await this.ingredientAction(async () => {
      const updated = await firstValueFrom(this.recipesApi.removeIngredient(recipe.id, draft.id))
      this.drafts = this.drafts.filter(d => d.id !== draft.id)
      return updated
    })
  }

  ingredientName(id: string): string {
    const ingredient = this.recipe()?.ingredients.find(i => i.id === id)
    return ingredient ? this.food(ingredient.foodItemId)?.name ?? 'Aliment indisponible' : ''
  }

  private async ingredientAction(action: () => Promise<RecipeDto>): Promise<void> {
    this.busy.set(true)
    this.ingredientError.set('')
    try {
      this.recipe.set(await action())
      void this.loadCost()
    } catch (error) {
      this.ingredientError.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true)
    this.actionError.set('')
    try { await action() } catch (error) { this.actionError.set(apiErrorMessage(error)) } finally { this.busy.set(false) }
  }
}
