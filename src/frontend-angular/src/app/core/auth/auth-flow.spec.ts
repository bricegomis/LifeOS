import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { buildLoginRedirectPath, validatedReturnPath } from './auth-flow.ts'

describe('internal return route', () => {
  it('accepts current routes and encodes them inside the login callback', () => {
    for (const path of ['/', '/planning', '/sports', '/settings', '/planner', '/recipes?search=soupe', '/recipes/3f2b9c1e-0000-4000-8000-000000000001', '/foods/abc?from=products']) {
      assert.equal(validatedReturnPath(path), path)
      const url = new URL(`https://lifeos.example/#${buildLoginRedirectPath(path)}`)
      assert.equal(new URLSearchParams(url.hash.split('?')[1]).get('redirect'), path)
    }
  })

  it('rejects external, protocol-relative, encoded, auxiliary, login and unknown routes', () => {
    for (const path of [null, '', 'https://evil.example', '//evil.example', '/\\evil', '/%2f%2fevil', '/login', '/planning(other:login)', '/unknown', '/planning#x', '/planning\n', '/recipes/a/b', '/login/x', '/recipes/%2f']) {
      assert.equal(validatedReturnPath(path), '/')
    }
  })
})
