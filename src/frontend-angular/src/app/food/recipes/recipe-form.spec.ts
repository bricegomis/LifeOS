import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { RecipeDto } from './recipe.models.ts'
import { buildIngredientRequest, buildRecipeRequest, parseTags, recipeSteps, recipeTags, visibleRecipes } from './recipe-form.ts'

const values = { name: ' Dhal ', servings: 4, durationMinutes: 35, tagsText: 'plat, Plat, , batch', instructions: ' Cuire. ' }

describe('recipe form rules', () => {
  it('trims, deduplicates and drops blank categories', () => {
    assert.deepEqual(parseTags('plat, Plat, , batch'), ['plat', 'batch'])
    assert.deepEqual(parseTags(''), [])
  })

  it('builds a clean request and sends an empty string to clear the instructions', () => {
    assert.deepEqual(buildRecipeRequest(values), {
      ok: true, value: { name: 'Dhal', servings: 4, durationMinutes: 35, tags: ['plat', 'batch'], instructions: 'Cuire.' },
    })
    const cleared = buildRecipeRequest({ ...values, instructions: '   ' })
    assert.equal(cleared.ok && cleared.value.instructions, '')
  })

  it('accepts a zero-minute recipe and rejects invalid name, yield and duration', () => {
    assert.equal(buildRecipeRequest({ ...values, durationMinutes: 0 }).ok, true)
    for (const invalid of [{ name: '  ' }, { name: 'x'.repeat(201) }, { servings: 0 }, { servings: 1.5 }, { durationMinutes: -1 }]) {
      assert.equal(buildRecipeRequest({ ...values, ...invalid }).ok, false, JSON.stringify(invalid))
    }
  })

  it('rejects categories the API cannot store', () => {
    assert.equal(buildRecipeRequest({ ...values, tagsText: 'plat|four' }).ok, false)
    assert.equal(buildRecipeRequest({ ...values, tagsText: 'x'.repeat(51) }).ok, false)
    assert.equal(buildRecipeRequest({ ...values, tagsText: Array.from({ length: 21 }, (_, i) => `t${i}`).join(',') }).ok, false)
  })

  it('accepts g, ml or pièce with a decimal comma and converts nothing', () => {
    assert.deepEqual(buildIngredientRequest('12,5', 'g'), { ok: true, value: { quantity: 12.5, unit: 'g' } })
    assert.deepEqual(buildIngredientRequest(2, 'pièce'), { ok: true, value: { quantity: 2, unit: 'pièce' } })
    assert.equal(buildIngredientRequest(0.3, 'kg').ok, false)
    assert.equal(buildIngredientRequest(0, 'g').ok, false)
    assert.equal(buildIngredientRequest('beaucoup', 'g').ok, false)
  })

  it('lists active recipes matching the search', () => {
    const recipe = (name: string, isArchived = false) => ({ name, isArchived }) as RecipeDto
    const list = [recipe('Dhal'), recipe('Gratin'), recipe('Dhal ancien', true)].map(item => ({ ...item, tags: [] }))
    assert.deepEqual(visibleRecipes(list, ' dh ').map(item => item.name), ['Dhal'])
    assert.equal(visibleRecipes(list, '').length, 2)
  })

  it('filters by category and lists the categories in use once', () => {
    const recipe = (name: string, tags: string[], isArchived = false) => ({ name, tags, isArchived }) as RecipeDto
    const list = [recipe('Dhal', ['plat', 'végétarien']), recipe('Porridge', ['Petit-déjeuner']), recipe('Vieux', ['oublié'], true)]
    assert.deepEqual(recipeTags(list), ['Petit-déjeuner', 'plat', 'végétarien'])
    assert.deepEqual(visibleRecipes(list, '', 'PLAT').map(item => item.name), ['Dhal'])
    assert.deepEqual(visibleRecipes(list, 'éta', '').map(item => item.name), [])
  })

  it('reads each non-empty instruction line as a step', () => {
    assert.deepEqual(recipeSteps('1. Rincer.\n\n2) Cuire 20 min.\nServir'), ['Rincer.', 'Cuire 20 min.', 'Servir'])
    assert.deepEqual(recipeSteps(null), [])
  })
})
