import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { FoodDto } from '../food.models.ts'
import type { RecipeDto } from './recipe.models.ts'
import { nutritionFactor, recipeNutrition } from './recipe-nutrition.ts'

const food = (id: string, referenceUnit: string, calories: number | null, fibers: number | null = 1) => ({
  id, name: id, referenceUnit,
  nutrition: { caloriesPerUnit: calories, proteinsPerUnit: 1, carbsPerUnit: 1, fatsPerUnit: 1, fibersPerUnit: fibers },
}) as unknown as FoodDto

describe('recipe nutrition', () => {
  it('counts nutrition bases only within the same dimension', () => {
    assert.equal(nutritionFactor(250, 'g', '100 g'), 2.5)
    assert.equal(nutritionFactor(400, 'ml', '100 ml'), 4)
    assert.equal(nutritionFactor(2, 'pièce', '1 pièce'), 2)
    assert.equal(nutritionFactor(100, 'ml', '100 g'), null)
    assert.equal(nutritionFactor(1, 'pièce', '100 g'), null)
    assert.equal(nutritionFactor(100, 'g', ''), null)
  })

  it('sums per portion and marks partial cells with the foods to complete', () => {
    const recipe = {
      servings: 2,
      ingredients: [
        { foodItemId: 'lentilles', quantity: 200, unit: 'g' },
        { foodItemId: 'oeuf', quantity: 2, unit: 'pièce' },
        { foodItemId: 'curry', quantity: 10, unit: 'g' },
      ],
    } as unknown as RecipeDto
    const foods = [food('lentilles', '100 g', 337), food('oeuf', '1 pièce', 78, 0), food('curry', '', null)]
    const { cells, incompleteFoods } = recipeNutrition(recipe, foods)
    const calories = cells.find(cell => cell.label === 'Calories')!
    assert.equal(calories.perPortion, (337 * 2 + 78 * 2) / 2)
    assert.equal(calories.partial, true)
    assert.deepEqual(incompleteFoods, ['curry'])
  })

  it('is complete when every food is described on a matching base, zero included', () => {
    const recipe = { servings: 1, ingredients: [{ foodItemId: 'oeuf', quantity: 1, unit: 'pièce' }] } as unknown as RecipeDto
    const { cells, incompleteFoods } = recipeNutrition(recipe, [food('oeuf', '1 pièce', 78, 0)])
    assert.ok(cells.every(cell => !cell.partial))
    assert.deepEqual(incompleteFoods, [])
  })
})
