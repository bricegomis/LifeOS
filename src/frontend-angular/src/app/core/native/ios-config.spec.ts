import assert from 'node:assert/strict'
import { it } from 'node:test'
import { validateIosConfig } from '../../../../scripts/ios-config.mjs'

it('rejects a missing, HTTP, credentialed or duplicated-path API URL', () => {
  assert.doesNotThrow(() => validateIosConfig({ apiBaseUrl: 'https://api.lifeos.example' }))
  for (const apiBaseUrl of ['', 'http://localhost', 'https://api.lifeos.example/api', 'https://user:pw@api.lifeos.example']) {
    assert.throws(() => validateIosConfig({ apiBaseUrl }), /iOS/)
  }
})
