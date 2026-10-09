import type { FoodItemDto, FoodItemRequest, GroceryItemUnit } from '../../core/api/api.models.ts'

export type ProductEditor = {
  name: string
  description: string
  unit: GroceryItemUnit | ''
  referenceUnit: string
  caloriesPerUnit: number | null
  proteinsPerUnit: number | null
  carbsPerUnit: number | null
  fatsPerUnit: number | null
}

export const newProductEditor = (): ProductEditor => ({
  name: '', description: '', unit: '', referenceUnit: '',
  caloriesPerUnit: null, proteinsPerUnit: null, carbsPerUnit: null, fatsPerUnit: null,
})

export function productEditorFromItem(item: FoodItemDto): ProductEditor {
  return {
    name: item.name, description: item.description,
    unit: item.purchaseUnitConfirmed ? item.unit : '', referenceUnit: item.referenceUnit,
    caloriesPerUnit: item.nutrition?.caloriesPerUnit ?? null,
    proteinsPerUnit: item.nutrition?.proteinsPerUnit ?? null,
    carbsPerUnit: item.nutrition?.carbsPerUnit ?? null,
    fatsPerUnit: item.nutrition?.fatsPerUnit ?? null,
  }
}

export function productRequest(editor: ProductEditor): FoodItemRequest {
  const hasNutrition = [editor.caloriesPerUnit, editor.proteinsPerUnit, editor.carbsPerUnit, editor.fatsPerUnit]
    .some(value => value !== null && value !== undefined)
  return {
    name: editor.name.trim(), description: editor.description.trim(), unit: editor.unit || null,
    referenceUnit: editor.referenceUnit.trim(),
    nutrition: hasNutrition ? {
      caloriesPerUnit: editor.caloriesPerUnit, proteinsPerUnit: editor.proteinsPerUnit,
      carbsPerUnit: editor.carbsPerUnit, fatsPerUnit: editor.fatsPerUnit,
    } : null,
  }
}

export function productDraftProblem(request: FoodItemRequest): string {
  if (!request.name || !request.unit || (request.nutrition && !request.referenceUnit))
    return 'Indiquez un nom, une unité d’achat et, si des valeurs nutritionnelles sont saisies, leur quantité de référence.'
  if (request.nutrition && Object.values(request.nutrition).some(value => value !== null && (!Number.isFinite(value) || value < 0)))
    return 'Les valeurs nutritionnelles doivent être positives ou nulles. Laissez vide une valeur inconnue.'
  return ''
}
