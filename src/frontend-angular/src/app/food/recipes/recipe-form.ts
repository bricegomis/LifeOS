import type { RecipeDto, RecipeIngredientRequest, RecipeRequest } from './recipe.models'

export const MAX_RECIPE_NAME_LENGTH = 200
export const MAX_TAGS = 20
export const MAX_TAG_LENGTH = 50

export interface RecipeFormValues {
  name: string
  servings: number
  durationMinutes: number
  tagsText: string
  instructions: string
}

export type FormResult<T> = { ok: true; value: T } | { ok: false; error: string }

/** Comma-separated categories: trimmed, blank ones dropped, duplicates removed ignoring case. */
export function parseTags(text: string): string[] {
  const seen = new Set<string>()
  return text.split(',').map(tag => tag.trim()).filter(tag => {
    const key = tag.toLocaleLowerCase('fr')
    if (!tag || seen.has(key)) return false
    seen.add(key)
    return true
  })
}

/** Mirrors the rules of the Recipe entity so the user gets the message before the round trip. */
export function buildRecipeRequest(values: RecipeFormValues): FormResult<RecipeRequest> {
  const name = values.name.trim()
  const servings = Number(values.servings)
  const durationMinutes = Number(values.durationMinutes)
  if (!name || name.length > MAX_RECIPE_NAME_LENGTH || !Number.isInteger(servings) || servings < 1
    || !Number.isInteger(durationMinutes) || durationMinutes < 0) {
    return { ok: false, error: 'Vérifiez le nom (200 caractères maximum), les portions (au moins 1) et la durée (0 minute ou plus).' }
  }
  const tags = parseTags(values.tagsText)
  if (tags.length > MAX_TAGS || tags.some(tag => tag.length > MAX_TAG_LENGTH || tag.includes('|'))) {
    return { ok: false, error: 'Vingt catégories au plus, de 50 caractères maximum, sans le caractère « | ».' }
  }
  // An empty string clears the instructions; the API reads null as "unchanged".
  return { ok: true, value: { name, servings, durationMinutes, tags, instructions: values.instructions.trim() } }
}

export function buildIngredientRequest(quantity: number, unit: string): FormResult<RecipeIngredientRequest> {
  const value = Number(quantity)
  const cleanUnit = unit.trim()
  if (!Number.isFinite(value) || value <= 0 || !cleanUnit || cleanUnit.length > 50) {
    return { ok: false, error: 'Indiquez une quantité supérieure à zéro et une unité.' }
  }
  return { ok: true, value: { quantity: value, unit: cleanUnit } }
}

/** Active recipes whose name contains the search text, ignoring case. */
export function visibleRecipes(recipes: readonly RecipeDto[], search: string): RecipeDto[] {
  const needle = search.trim().toLocaleLowerCase('fr')
  return recipes.filter(recipe => !recipe.isArchived && recipe.name.toLocaleLowerCase('fr').includes(needle))
}
