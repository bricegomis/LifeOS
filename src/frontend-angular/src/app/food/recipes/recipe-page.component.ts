import { Component, computed, inject, input, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import type { MemberProfileDto } from '@/app/core/api/api.models'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HouseholdReviewsComponent } from '@/app/shared/reviews/household-reviews.component'
import { ReviewDraftsService } from '@/app/shared/reviews/review-drafts.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, StoreDto } from '../food.models'
import { currentMonth, offSeasonNotice, seasonStatus, seasonTagClass } from '../shared/classification'
import { FoodImageComponent } from '../shared/food-image.component'
import { fodmapLabel, formatNumber, formatQuantity } from '../shared/format'
import { costView } from './recipe-cost'
import { RecipeEditorComponent } from './recipe-editor.component'
import { recipeSteps } from './recipe-form'
import { type RecipeIngredientsData, RecipeIngredientsDialogComponent } from './recipe-ingredients-dialog.component'
import { recipeNutrition } from './recipe-nutrition'
import type { RecipeCostDto, RecipeDto, RecipeIngredientDto } from './recipe.models'
import { RecipesApiService } from './recipes-api.service'
import { ConfirmService } from '@/app/shared/ui/confirm'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

/** A recipe: photo, ingredients with their season, preparation, reviews, nutrition per portion and cost range. */
@Component({
  selector: 'app-recipe-page',
  standalone: true,
  imports: [SkeletonComponent, FoodImageComponent, HouseholdReviewsComponent, MatButtonModule, MatIconModule, RouterLink],
  templateUrl: './recipe-page.component.html',
  styleUrl: './recipe-page.css',
})
export class RecipePageComponent implements OnInit {
  private readonly recipesApi = inject(RecipesApiService)
  private readonly confirm = inject(ConfirmService)
  private readonly foodApi = inject(FoodApiService)
  private readonly dialog = inject(MatDialog)
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

  readonly fodmapLabel = fodmapLabel
  readonly seasonTagClass = seasonTagClass
  readonly steps = computed(() => recipeSteps(this.recipe()?.instructions ?? null))
  readonly nutrition = computed(() => {
    const recipe = this.recipe()
    return recipe ? recipeNutrition(recipe, this.foods()) : null
  })

  /** Season of the ingredients for the current month (ADR 0012), computed on read. */
  readonly month = currentMonth()
  readonly seasonNotice = computed(() => {
    const recipe = this.recipe()
    if (!recipe) return null
    const foods = recipe.ingredients.map(i => this.food(i.foodItemId)).filter((food): food is FoodDto => !!food)
    return offSeasonNotice(foods, this.month)
  })
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

  async canLeave(): Promise<boolean> { return !this.busy() && await this.reviewDrafts.canLeave() }

  meta(recipe: RecipeDto): string {
    return `${recipe.servings} portion${recipe.servings > 1 ? 's' : ''} · ${recipe.durationMinutes} min`
  }

  season(food: FoodDto | undefined) { return seasonStatus(food?.seasonMonths ?? [], this.month) }
  food(id: string): FoodDto | undefined { return this.foods().find(food => food.id === id) }
  quantity(ingredient: RecipeIngredientDto): string { return formatQuantity(ingredient.quantity, ingredient.unit) }
  number(value: number): string { return formatNumber(value, value < 10 ? 1 : 0) }

  async edit(): Promise<void> {
    const recipe = this.recipe()
    if (!recipe) return
    const saved = await firstValueFrom(openEditor<RecipeEditorComponent, RecipeDto, RecipeDto>(this.dialog, RecipeEditorComponent, recipe).afterClosed())
    if (!saved) return
    this.recipe.set(saved)
    void this.loadCost()
  }

  /** Each ingredient row saves itself; the page follows every change. */
  openIngredients(): void {
    const recipe = this.recipe()
    if (!recipe) return
    openEditor<RecipeIngredientsDialogComponent, RecipeIngredientsData>(this.dialog, RecipeIngredientsDialogComponent, {
      recipe, foods: this.foods(), changed: updated => { this.recipe.set(updated); void this.loadCost() },
    })
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
    if (!recipe || !await this.confirm.ask({ message: 'Retirer la photo de cette recette ?', confirmLabel: 'Retirer', danger: true })) return
    await this.run(async () => {
      await firstValueFrom(this.foodApi.removeImage('recipes', recipe.id))
      this.recipe.set({ ...recipe, hasImage: false, updatedAt: new Date().toISOString() })
    })
  }

  async archive(): Promise<void> {
    const recipe = this.recipe()
    if (!recipe || !await this.confirm.ask({ message: `Archiver « ${recipe.name} » ? Elle disparaît de la liste des recettes.`, confirmLabel: 'Archiver', danger: true })) return
    await this.run(async () => {
      await firstValueFrom(this.recipesApi.archive(recipe.id))
      await this.router.navigate(['/recipes'])
    })
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true)
    this.actionError.set('')
    try { await action() } catch (error) { this.actionError.set(apiErrorMessage(error)) } finally { this.busy.set(false) }
  }
}
