import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { FoodDto } from '../food.models.ts'
import {
  addTag, filterFoods, type FoodListFilter, householdTags, offSeasonNotice, seasonRangeLabel, seasonStatus, sortBySeason,
} from './classification.ts'

const food = (name: string, extra: Partial<FoodDto> = {}) => ({
  id: name, name, missingFields: [], isArchived: false, family: 'vegetable', roles: [], conservation: null,
  seasonMonths: [], tags: [], ...extra,
}) as unknown as FoodDto

const pepper = food('Poivron', { seasonMonths: [7, 8, 9, 10], roles: ['vegetable'], conservation: 'fresh', tags: ['wok'] })
const frozenPepper = food('Poivron surgelé', { conservation: 'frozen', tags: ['Wok'] })
const leek = food('Poireau', { seasonMonths: [1, 2, 3, 4, 9, 10, 11, 12] })
const tofu = food('Tofu', { family: 'legume', roles: ['protein'], missingFields: ['Fibres'] })

describe('food classification', () => {
  it('reads no month as all year, otherwise in or out of season', () => {
    assert.equal(seasonStatus([], 12).kind, 'all')
    assert.equal(seasonStatus([7, 8], 12).kind, 'out')
    assert.equal(seasonStatus([12, 1], 12).kind, 'in')
  })

  it('sorts in season first, then all year, then out of season, without hiding anything', () => {
    assert.deepEqual(sortBySeason([pepper, tofu, frozenPepper, leek], 12).map(f => f.name),
      ['Poireau', 'Poivron surgelé', 'Tofu', 'Poivron'])
  })

  it('names the off-season ingredients of a recipe for the month', () => {
    assert.equal(offSeasonNotice([pepper, leek, frozenPepper], 12), 'En décembre, 1 ingrédient est hors saison : Poivron.')
    assert.equal(offSeasonNotice([pepper, food('Tomate', { seasonMonths: [6] })], 1),
      'En janvier, 2 ingrédients sont hors saison : Poivron, Tomate.')
    assert.equal(offSeasonNotice([leek], 12), null)
  })

  it('writes the season as month ranges, joining december to january', () => {
    assert.equal(seasonRangeLabel([]), 'Pas saisonnier')
    assert.equal(seasonRangeLabel([7, 8, 9, 10]), 'juillet à octobre')
    assert.equal(seasonRangeLabel(leek.seasonMonths), 'septembre à avril')
    assert.equal(seasonRangeLabel([1, 3, 12]), 'mars, décembre à janvier')
  })

  it('keeps one spelling per tag and reuses it when a tag is typed again', () => {
    const known = householdTags([pepper, frozenPepper, food('Pomme', { tags: ['goûter'] })])
    assert.deepEqual(known, ['goûter', 'wok'])
    assert.deepEqual(addTag([], ' WOK ', known), ['wok'])
    assert.deepEqual(addTag(['wok'], 'Wok', known), ['wok'])
    assert.deepEqual(addTag(['wok'], 'apéro', known), ['wok', 'apéro'])
    assert.deepEqual(addTag(['wok'], '  ', known), ['wok'])
  })

  it('filters by family, role, conservation, season, tag and incompleteness, never showing archived foods', () => {
    const all = [pepper, frozenPepper, leek, tofu, food('Ancien', { isArchived: true })]
    const base: FoodListFilter = { search: '', todo: false, family: '', role: '', conservation: '', season: '', tag: '', month: 12 }
    const names = (filter: Partial<FoodListFilter>) => filterFoods(all, { ...base, ...filter }).map(f => f.name)
    assert.deepEqual(names({}), ['Poivron', 'Poivron surgelé', 'Poireau', 'Tofu'])
    assert.deepEqual(names({ family: 'legume' }), ['Tofu'])
    assert.deepEqual(names({ role: 'protein' }), ['Tofu'])
    assert.deepEqual(names({ conservation: 'frozen' }), ['Poivron surgelé'])
    assert.deepEqual(names({ season: 'in' }), ['Poireau'])
    assert.deepEqual(names({ season: 'out' }), ['Poivron'])
    assert.deepEqual(names({ tag: 'WOK' }), ['Poivron', 'Poivron surgelé'])
    assert.deepEqual(names({ todo: true }), ['Tofu'])
    assert.deepEqual(names({ search: 'poiv' }), ['Poivron', 'Poivron surgelé'])
  })
})
