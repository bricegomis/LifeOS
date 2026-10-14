import assert from 'node:assert/strict'
import { it } from 'node:test'
import { validateIosConfig } from '../../../../scripts/ios-config.mjs'

it('rejects missing config, HTTP, duplicated API path and server keys without displaying them', () => {
  const config = {
    apiBaseUrl: 'https://api.lifeos.example', supabaseUrl: 'https://supabase.lifeos.example',
    supabaseAnonKey: 'sb_publishable_test_only',
  }
  assert.doesNotThrow(() => validateIosConfig(config))
  for (const override of [
    { apiBaseUrl: '' }, { apiBaseUrl: 'http://localhost' }, { apiBaseUrl: 'https://api.lifeos.example/api' },
    { supabaseUrl: 'https://user:password@supabase.lifeos.example' }, { supabaseAnonKey: '' },
    { supabaseAnonKey: 'sb_secret_do_not_bundle' },
    { supabaseAnonKey: `header.${Buffer.from('{"role":"service_role"}').toString('base64url')}.signature` },
  ]) {
    assert.throws(() => validateIosConfig({ ...config, ...override }), /iOS/)
  }
  assert.doesNotThrow(() => validateIosConfig({
    ...config, supabaseAnonKey: `header.${Buffer.from('{"role":"anon"}').toString('base64url')}.signature`,
  }))
})
