import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { missingFoodFields, newFoodDraft } from './food-draft.ts'

describe('manual food completeness', () => {
  it('distinguishes absent, zero, partial macros, base and digestive fields', () => {
    const food = newFoodDraft()
    assert.equal(missingFoodFields(food).length, 7)
    food.nutrition = { caloriesPerUnit: 0, proteinsPerUnit: 0, carbsPerUnit: 0, fatsPerUnit: 0 }
    assert.deepEqual(missingFoodFields(food), ['Base nutritionnelle', 'FODMAP', 'NOVA'])
    food.referenceUnit = '100g'
    food.fodmap = 'low'
    food.nova = 1
    assert.deepEqual(missingFoodFields(food), [])
    assert.equal(food.digestiveNotes, null)
    food.nutrition.proteinsPerUnit = null
    assert.deepEqual(missingFoodFields(food), ['Protéines'])
  })
})
