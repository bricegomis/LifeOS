import type { FoodItemDto, FoodItemRequest } from '../../core/api/api.models.ts'

export const newFoodDraft = (): FoodItemRequest => ({
  name: '', referenceUnit: '', nutrition: { caloriesPerUnit: null, proteinsPerUnit: null, carbsPerUnit: null, fatsPerUnit: null },
  fodmap: null, nova: null, digestiveNotes: null,
})

export function missingFoodFields(food: Pick<FoodItemDto, 'referenceUnit' | 'nutrition' | 'fodmap' | 'nova'>): string[] {
  return [
    !food.referenceUnit.trim() ? 'Base nutritionnelle' : '',
    food.nutrition?.caloriesPerUnit == null ? 'Calories' : '',
    food.nutrition?.proteinsPerUnit == null ? 'Protéines' : '',
    food.nutrition?.carbsPerUnit == null ? 'Glucides' : '',
    food.nutrition?.fatsPerUnit == null ? 'Lipides' : '',
    food.fodmap == null ? 'FODMAP' : '',
    food.nova == null ? 'NOVA' : '',
  ].filter(Boolean)
}
