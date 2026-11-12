import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { FoodImageComponent } from '../shared/food-image.component'
import { costSummary } from './recipe-cost'
import { RecipeEditorComponent } from './recipe-editor.component'
import { recipeTags, visibleRecipes } from './recipe-form'
import type { RecipeCostDto, RecipeDto } from './recipe.models'
import { RecipesApiService } from './recipes-api.service'

@Component({
  selector: 'app-recipes-page',
  standalone: true,
  imports: [FormsModule, RouterLink, FoodImageComponent, RecipeEditorComponent],
  templateUrl: './recipes-page.component.html',
  styleUrl: './recipes.css',
})
export class RecipesPageComponent implements OnInit {
  private readonly api = inject(RecipesApiService)
  private readonly router = inject(Router)
  readonly recipes = signal<RecipeDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  readonly search = signal('')
  readonly tag = signal('')
  readonly tags = computed(() => recipeTags(this.recipes()))
  readonly visible = computed(() => visibleRecipes(this.recipes(), this.search(), this.tag()))
  readonly creating = signal(false)
  readonly costs = signal<Map<string, RecipeCostDto>>(new Map())

  async ngOnInit(): Promise<void> {
    try {
      this.recipes.set(await firstValueFrom(this.api.list()))
      void this.loadCosts()
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  /** Costs are secondary: a failure only hides them. */
  private async loadCosts(): Promise<void> {
    try {
      this.costs.set(new Map((await firstValueFrom(this.api.costs())).map(cost => [cost.recipeId, cost])))
    } catch {
      this.costs.set(new Map())
    }
  }

  cost(recipe: RecipeDto): string { return costSummary(this.costs().get(recipe.id)) }

  meta(recipe: RecipeDto): string {
    return `${recipe.servings} portion${recipe.servings > 1 ? 's' : ''} · ${recipe.durationMinutes} min`
  }

  async created(recipe: RecipeDto): Promise<void> {
    this.creating.set(false)
    await this.router.navigate(['/recipes', recipe.id])
  }
}
