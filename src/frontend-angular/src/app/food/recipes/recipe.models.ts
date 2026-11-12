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

export interface PriceCandidateDto {
  productId: string
  productName: string
  storeId: string
  packagePrice: number
  unitPrice: number
  unitLabel: 'kg' | 'L' | 'pièce'
  observedOn: string
}

export interface IngredientCostDto {
  ingredientId: string
  foodItemId: string
  quantity: number
  unit: IngredientUnit
  /** Null when no price is comparable with the ingredient's unit. */
  minAmount: number | null
  maxAmount: number | null
  cheapest: PriceCandidateDto | null
  dearest: PriceCandidateDto | null
  comparablePrices: number
}

/** Min–max cost, all stores together; totals are partial while `missingIngredients` > 0. */
export interface RecipeCostDto {
  recipeId: string
  servings: number
  minTotal: number
  maxTotal: number
  minPerServing: number
  maxPerServing: number
  missingIngredients: number
  isComplete: boolean
  lines: IngredientCostDto[]
}
