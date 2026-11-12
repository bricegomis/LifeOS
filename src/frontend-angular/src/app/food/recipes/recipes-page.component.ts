import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { FoodImageComponent } from '../shared/food-image.component'
import { RecipeEditorComponent } from './recipe-editor.component'
import { recipeTags, visibleRecipes } from './recipe-form'
import type { RecipeDto } from './recipe.models'
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

  async ngOnInit(): Promise<void> {
    try {
      this.recipes.set(await firstValueFrom(this.api.list()))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  meta(recipe: RecipeDto): string {
    return `${recipe.servings} portion${recipe.servings > 1 ? 's' : ''} · ${recipe.durationMinutes} min`
  }

  async created(recipe: RecipeDto): Promise<void> {
    this.creating.set(false)
    await this.router.navigate(['/recipes', recipe.id])
  }
}
