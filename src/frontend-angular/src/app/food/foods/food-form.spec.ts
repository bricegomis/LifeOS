import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { FoodDto, ProductDto, StoreDto } from '../food.models.ts'
import { buildFoodRequest, emptyFoodForm, foodFormFrom, foodPriceSummary, parseOptionalNumber, visibleFoods } from './food-form.ts'

const food = (name: string, missing: string[] = [], archived = false) =>
  ({ id: name, name, missingFields: missing, isArchived: archived }) as unknown as FoodDto

describe('food form', () => {
  it('reads empty as unknown, zero as a value and accepts a decimal comma', () => {
    assert.equal(parseOptionalNumber(''), null)
    assert.equal(parseOptionalNumber('0'), 0)
    assert.equal(parseOptionalNumber('12,5'), 12.5)
    assert.ok(Number.isNaN(parseOptionalNumber('-1')!))
    assert.ok(Number.isNaN(parseOptionalNumber('beaucoup')!))
  })

  it('builds a request with fibres, digestion and blank notes cleared', () => {
    const values = emptyFoodForm(' Lentilles ')
    values.referenceUnit = '100 g'
    values.nutrition.fibersPerUnit = '11'
    values.nutrition.caloriesPerUnit = '0'
    values.fodmap = 'moderate'
    values.nova = '1'
    values.digestiveNotes = '  '
    const result = buildFoodRequest(values)
    assert.ok(result.ok)
    assert.deepEqual(result.value, {
      name: 'Lentilles', referenceUnit: '100 g', fodmap: 'moderate', nova: 1, digestiveNotes: null,
      nutrition: { caloriesPerUnit: 0, proteinsPerUnit: null, carbsPerUnit: null, fatsPerUnit: null, fibersPerUnit: 11 },
    })
  })

  it('refuses a missing name or an invalid number', () => {
    assert.equal(buildFoodRequest(emptyFoodForm(' ')).ok, false)
    const values = emptyFoodForm('Riz')
    values.nutrition.proteinsPerUnit = 'abc'
    const result = buildFoodRequest(values)
    assert.ok(!result.ok && result.error.startsWith('Protéines'))
  })

  it('round-trips an existing food through the form', () => {
    const existing = {
      name: 'Riz', referenceUnit: '100 g', fodmap: 'low', nova: 1, digestiveNotes: null,
      nutrition: { caloriesPerUnit: 351, proteinsPerUnit: 8, carbsPerUnit: 78, fatsPerUnit: 0.6, fibersPerUnit: 1.4 },
    } as unknown as FoodDto
    const result = buildFoodRequest(foodFormFrom(existing))
    assert.ok(result.ok)
    assert.deepEqual(result.value.nutrition, existing.nutrition)
  })

  it('filters by name and by incompleteness, never showing archived foods', () => {
    const foods = [food('Carotte'), food('Curry', ['Fibres']), food('Ancien', [], true)]
    assert.deepEqual(visibleFoods(foods, '', 'all').map(f => f.name), ['Carotte', 'Curry'])
    assert.deepEqual(visibleFoods(foods, '', 'todo').map(f => f.name), ['Curry'])
    assert.deepEqual(visibleFoods(foods, 'car', 'all').map(f => f.name), ['Carotte'])
  })

  it('summarises the latest prices as a range per comparison unit', () => {
    const stores = [{ id: 's1', name: 'Leclerc' }, { id: 's2', name: 'Biocoop' }] as StoreDto[]
    const price = (storeId: string, value: number, unitPrice: number) =>
      ({ id: storeId, storeId, price: value, unitPrice, unitLabel: 'kg', observedOn: '2026-11-02' })
    const products = [
      { name: 'Emmental 400 g', isArchived: false, latestPrices: [price('s1', 5.95, 14.875)] },
      { name: 'Emmental bio 150 g', isArchived: false, latestPrices: [price('s2', 2.85, 19)] },
      { name: 'Ancien', isArchived: true, latestPrices: [price('s1', 1, 1)] },
    ] as unknown as ProductDto[]
    const summary = foodPriceSummary(products, stores)
    assert.equal(summary.range, 'De 14,88 €/kg à 19,00 €/kg')
    assert.deepEqual(summary.lines.map(line => line.detail), ['Leclerc · 5,95 € · 02/11/2026', 'Biocoop · 2,85 € · 02/11/2026'])
    assert.equal(foodPriceSummary([], stores).range, 'Aucun prix relevé')
  })
})
