import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

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
