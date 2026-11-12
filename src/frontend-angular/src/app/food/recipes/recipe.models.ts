import type { IngredientUnit } from '../food.models'

export interface RecipeIngredientDto {
  id: string
  foodItemId: string
  quantity: number
  unit: IngredientUnit
}

export interface RecipeDto {
  id: string
  isArchived: boolean
  name: string
  servings: number
  durationMinutes: number
  tags: string[]
  instructions: string | null
  ingredients: RecipeIngredientDto[]
  hasImage: boolean
  createdAt: string
  updatedAt: string
}

export interface RecipeRequest {
  name: string
  servings: number
  durationMinutes: number
  tags: string[]
  instructions: string | null
}

export interface RecipeIngredientRequest {
  quantity: number
  unit: IngredientUnit
}

export interface AddRecipeIngredientRequest extends RecipeIngredientRequest {
  foodItemId: string
}
