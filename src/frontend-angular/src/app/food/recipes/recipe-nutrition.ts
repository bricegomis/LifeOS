import type { FoodDto, IngredientUnit, NutritionBase, NutritionDto } from '../food.models'
import type { RecipeDto } from './recipe.models'

type Dimension = 'mass' | 'volume' | 'count'

const BASES: Record<NutritionBase, { dimension: Dimension; amount: number }> = {
  '100 g': { dimension: 'mass', amount: 100 },
  '100 ml': { dimension: 'volume', amount: 100 },
  '1 pièce': { dimension: 'count', amount: 1 },
}
const UNITS: Record<IngredientUnit, Dimension> = { g: 'mass', ml: 'volume', pièce: 'count' }

export const NUTRITION_CELLS = [
  { key: 'caloriesPerUnit', label: 'Calories', unit: 'kcal' },
  { key: 'proteinsPerUnit', label: 'Protéines', unit: 'g' },
  { key: 'carbsPerUnit', label: 'Glucides', unit: 'g' },
  { key: 'fatsPerUnit', label: 'Lipides', unit: 'g' },
  { key: 'fibersPerUnit', label: 'Fibres', unit: 'g' },
] as const satisfies readonly { key: keyof NutritionDto; label: string; unit: string }[]

export interface NutritionCell { label: string; unit: string; perPortion: number; partial: boolean }

/**
 * How many nutrition bases an ingredient holds: 250 g of a food described per 100 g is 2.5.
 * Null when the base is unknown or of another dimension (no density or piece weight invented).
 */
export function nutritionFactor(quantity: number, unit: IngredientUnit, base: NutritionBase | ''): number | null {
  if (!base) return null
  const reference = BASES[base]
  return UNITS[unit] === reference.dimension ? quantity / reference.amount : null
}

/**
 * Nutrition per portion (ingredient quantity × factor / yield). An unknown value stays unknown:
 * the cell is marked partial and the food is named as "to complete".
 */
export function recipeNutrition(recipe: RecipeDto, foods: readonly FoodDto[]): { cells: NutritionCell[]; incompleteFoods: string[] } {
  const incomplete = new Set<string>()
  const cells = NUTRITION_CELLS.map(cell => {
    let total = 0
    let partial = false
    for (const ingredient of recipe.ingredients) {
      const food = foods.find(candidate => candidate.id === ingredient.foodItemId)
      const value = food?.nutrition?.[cell.key]
      const factor = food ? nutritionFactor(ingredient.quantity, ingredient.unit, food.referenceUnit) : null
      if (value === null || value === undefined || factor === null) {
        partial = true
        incomplete.add(food?.name ?? 'Aliment indisponible')
      } else {
        total += value * factor
      }
    }
    return { label: cell.label, unit: cell.unit, perPortion: total / recipe.servings, partial }
  })
  return { cells, incompleteFoods: [...incomplete] }
}
