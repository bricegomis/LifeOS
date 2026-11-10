import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { splitInstructionsIntoSteps } from './recipe-steps.ts'

describe('recipe-steps instruction splitting', () => {
  it('returns empty array on empty or null instructions', () => {
    assert.deepEqual(splitInstructionsIntoSteps(''), [])
    assert.deepEqual(splitInstructionsIntoSteps('   '), [])
    assert.deepEqual(splitInstructionsIntoSteps(null), [])
    assert.deepEqual(splitInstructionsIntoSteps(undefined), [])
  })

  it('splits newline-separated steps and strips bullet numbers', () => {
    const raw = `1. Préchauffer le four à 180°C.
2. Émincer les oignons.
- Faire revenir dans l'huile d'olive.
* Laisser mijoter 20 minutes.`

    const steps = splitInstructionsIntoSteps(raw)
    assert.equal(steps.length, 4)
    assert.equal(steps[0], 'Préchauffer le four à 180°C.')
    assert.equal(steps[1], 'Émincer les oignons.')
    assert.equal(steps[2], "Faire revenir dans l'huile d'olive.")
    assert.equal(steps[3], 'Laisser mijoter 20 minutes.')
  })

  it('splits single-line instructions with inline numbering deterministically without inventing steps', () => {
    const raw = '1. Laver les légumes. 2. Couper en dés. 3. Servir frais.'
    const steps = splitInstructionsIntoSteps(raw)
    assert.equal(steps.length, 3)
    assert.equal(steps[0], 'Laver les légumes.')
    assert.equal(steps[1], 'Couper en dés.')
    assert.equal(steps[2], 'Servir frais.')
  })

  it('preserves single un-numbered paragraph as a single step', () => {
    const raw = 'Mélanger tous les ingrédients dans un grand saladier.'
    const steps = splitInstructionsIntoSteps(raw)
    assert.deepEqual(steps, ['Mélanger tous les ingrédients dans un grand saladier.'])
  })
})

describe('native URL and deep link resolution', () => {
  it('extracts path and safe redirect from native callback URLs', () => {
    const authUrl = new URL('lifeos://auth/callback?code=oauth123&redirect=%2Frecipes')
    assert.equal(authUrl.searchParams.get('code'), 'oauth123')
    assert.equal(authUrl.searchParams.get('redirect'), '/recipes')

    const recipesDeepLink = 'lifeos:///recipes'
    const matchRecipes = recipesDeepLink.match(/^lifeos:\/\/?\/?(.*)$/)
    assert.ok(matchRecipes)
    assert.equal('/' + matchRecipes[1], '/recipes')
  })
})
