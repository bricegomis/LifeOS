import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { AddRecipeIngredientRequest, RecipeDto, RecipeIngredientRequest, RecipeRequest } from './recipe.models'

/** Every HTTP call of the recipes screens. Ingredient calls return the whole recipe. */
@Injectable({ providedIn: 'root' })
export class RecipesApiService {
  private readonly api = inject(LifeosApiService)

  list(): Observable<RecipeDto[]> { return this.api.get<RecipeDto[]>('/recipes') }
  get(id: string): Observable<RecipeDto> { return this.api.get<RecipeDto>(this.path(id)) }
  create(request: RecipeRequest): Observable<RecipeDto> { return this.api.post<RecipeDto>('/recipes', request) }
  update(id: string, request: RecipeRequest): Observable<RecipeDto> { return this.api.put<RecipeDto>(this.path(id), request) }
  archive(id: string): Observable<void> { return this.api.delete(this.path(id)) }

  addIngredient(recipeId: string, request: AddRecipeIngredientRequest): Observable<RecipeDto> {
    return this.api.post<RecipeDto>(`${this.path(recipeId)}/ingredients`, request)
  }
  updateIngredient(recipeId: string, ingredientId: string, request: RecipeIngredientRequest): Observable<RecipeDto> {
    return this.api.put<RecipeDto>(this.ingredientPath(recipeId, ingredientId), request)
  }
  removeIngredient(recipeId: string, ingredientId: string): Observable<RecipeDto> {
    return this.api.delete<RecipeDto>(this.ingredientPath(recipeId, ingredientId))
  }

  private path(id: string): string { return `/recipes/${encodeURIComponent(id)}` }
  private ingredientPath(recipeId: string, ingredientId: string): string {
    return `${this.path(recipeId)}/ingredients/${encodeURIComponent(ingredientId)}`
  }
}
