export interface RecipeIngredientDto {
  id: string
  foodItemId: string
  quantity: number
  unit: string
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
  unit: string
}

export interface AddRecipeIngredientRequest extends RecipeIngredientRequest {
  foodItemId: string
}
