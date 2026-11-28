import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent } from '@/app/shared/ui/list-page.component'
import { FoodImageComponent } from '../shared/food-image.component'
import { costSummary } from './recipe-cost'
import { RecipeEditorComponent } from './recipe-editor.component'
import { recipeTags, visibleRecipes } from './recipe-form'
import type { RecipeCostDto, RecipeDto } from './recipe.models'
import { RecipesApiService } from './recipes-api.service'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

/** Recipe library (validated step 1 mockup): cards with their photo, filtered by name and category. */
@Component({
  selector: 'app-recipes-page',
  standalone: true,
  imports: [SkeletonComponent, FoodImageComponent, ListPageComponent, MatIconModule, RouterLink],
  template: `
    <app-list-page heading="Recettes" addLabel="Ajouter une recette" [(query)]="search" (add)="create()">
      @if (tags().length) {
        <div class="lo-chips" role="group" aria-label="Catégories">
          <button type="button" class="lo-filter-chip" [class.is-on]="!tag()" [attr.aria-pressed]="!tag()" (click)="tag.set('')">
            @if (!tag()) { <mat-icon svgIcon="check" /> }Toutes</button>
          @for (item of tags(); track item) {
            <button type="button" class="lo-filter-chip" [class.is-on]="tag() === item" [attr.aria-pressed]="tag() === item" (click)="tag.set(item)">
              @if (tag() === item) { <mat-icon svgIcon="check" /> }{{ item }}</button>
          }
        </div>
      }
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (loading()) {
        <app-skeleton />
      } @else if (!recipes().length) {
        <p class="lo-empty">Aucune recette pour le moment. Ajoute la première avec « Ajouter une recette ».</p>
      } @else if (!visible().length) {
        <p class="lo-empty">Aucune recette ne correspond à ce filtre.</p>
      } @else {
        <div class="recipe-cards">
          @for (recipe of visible(); track recipe.id) {
            <a class="lo-card recipe-card" [routerLink]="['/recipes', recipe.id]">
              <app-food-image [path]="'/recipes/' + recipe.id + '/image'" [hasImage]="recipe.hasImage" [version]="recipe.updatedAt"
                [name]="recipe.name" width="100%" height="170px" />
              <span class="recipe-card-body">
                <span class="recipe-card-title">{{ recipe.name }}</span>
                <span class="lo-muted">{{ meta(recipe) }}</span>
                @if (recipe.tags.length) {
                  <span class="lo-tags">@for (item of recipe.tags; track item) { <span class="lo-tag">{{ item }}</span> }</span>
                }
                @if (cost(recipe); as summary) { <span class="lo-muted lo-small">{{ summary }}</span> }
              </span>
            </a>
          }
        </div>
      }
    </app-list-page>
  `,
  styles: [`
    .recipe-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; }
    .recipe-card { display: flex; flex-direction: column; color: var(--mat-sys-on-surface); }
    .recipe-card:hover { box-shadow: var(--mat-sys-level2); }
    .recipe-card ::ng-deep .lo-thumb { border-radius: 0; }
    .recipe-card-body { display: flex; flex-direction: column; gap: 8px; padding: 16px; }
    .recipe-card-title { font: var(--mat-sys-title-medium); }
  `],
})
export class RecipesPageComponent implements OnInit {
  private readonly api = inject(RecipesApiService)
  private readonly dialog = inject(MatDialog)
  private readonly router = inject(Router)
  readonly recipes = signal<RecipeDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  readonly search = signal('')
  readonly tag = signal('')
  readonly tags = computed(() => recipeTags(this.recipes()))
  readonly visible = computed(() => visibleRecipes(this.recipes(), this.search(), this.tag()))
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

  async create(): Promise<void> {
    const recipe = await firstValueFrom(openEditor<RecipeEditorComponent, null, RecipeDto>(this.dialog, RecipeEditorComponent, null).afterClosed())
    if (recipe) await this.router.navigate(['/recipes', recipe.id])
  }
}
