import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { FoodDto, StoreDto } from '../food.models.ts'
import { costSummary, costView } from './recipe-cost.ts'
import type { RecipeCostDto } from './recipe.models.ts'

const stores = [{ id: 's1', name: 'Leclerc' }, { id: 's2', name: 'Biocoop' }] as StoreDto[]
const foods = [{ id: 'lentilles', name: 'Lentilles corail' }, { id: 'curry', name: 'Curry' }] as FoodDto[]
const price = (storeId: string, productName: string, unitPrice: number) =>
  ({ productId: productName, productName, storeId, packagePrice: 1, unitPrice, unitLabel: 'kg' as const, observedOn: '2026-11-02' })

const cost: RecipeCostDto = {
  recipeId: 'dhal', servings: 4, minTotal: 0.9, maxTotal: 1.48, minPerServing: 0.23, maxPerServing: 0.37,
  missingIngredients: 1, isComplete: false,
  lines: [
    { ingredientId: 'i1', foodItemId: 'lentilles', quantity: 250, unit: 'g', minAmount: 0.9, maxAmount: 1.48,
      cheapest: price('s1', 'Lentilles 500 g', 3.6), dearest: price('s2', 'Lentilles vrac', 5.9), comparablePrices: 2 },
    { ingredientId: 'i2', foodItemId: 'curry', quantity: 10, unit: 'g', minAmount: null, maxAmount: null,
      cheapest: null, dearest: null, comparablePrices: 0 },
  ],
}

describe('recipe cost view', () => {
  it('shows partial totals with the number of ingredients without a comparable price', () => {
    const view = costView(cost, foods, stores)
    assert.equal(view.min, '0,90 €')
    assert.equal(view.minPerServing, 'total partiel')
    assert.equal(view.notice, 'Coût incomplet : 1 ingrédient sans prix comparable.')
    assert.deepEqual(view.lines[0], {
      name: 'Lentilles corail — 250 g', amount: '0,90 € à 1,48 €', unknown: false,
      details: ['Mini : Lentilles 500 g · Leclerc · 3,60 €/kg · 02/11/2026', 'Maxi : Lentilles vrac · Biocoop · 5,90 €/kg · 02/11/2026'],
    })
    assert.equal(view.lines[1]!.amount, 'Inconnu')
    assert.equal(costSummary(cost), 'Coût incomplet')
  })

  it('gives a range per portion once every ingredient is priced', () => {
    const complete = { ...cost, missingIngredients: 0, isComplete: true, lines: [cost.lines[0]!] }
    assert.equal(costSummary(complete), '0,23 € à 0,37 € par portion')
    assert.equal(costView(complete, foods, stores).maxPerServing, '0,37 € par portion')
    assert.equal(costSummary({ ...complete, maxPerServing: 0.23 }), '0,23 € par portion')
    assert.equal(costSummary({ ...complete, lines: [] }), '')
    assert.equal(costSummary(undefined), '')
  })

  it('names the single price when only one is comparable', () => {
    const single = { ...cost, lines: [{ ...cost.lines[0]!, comparablePrices: 1, maxAmount: 0.9, dearest: cost.lines[0]!.cheapest }] }
    assert.deepEqual(costView(single, foods, stores).lines[0]!.details, ['Un seul prix : Lentilles 500 g · Leclerc · 3,60 €/kg · 02/11/2026'])
  })
})
