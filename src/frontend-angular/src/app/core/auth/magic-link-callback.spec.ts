import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { readMagicLinkCallback, removeMagicLinkParams } from './magic-link-callback.ts'

const origin = 'https://lifeos.example'

describe('readMagicLinkCallback', () => {
  it('ignores regular app URLs', () => {
    assert.deepEqual(readMagicLinkCallback(`${origin}/#/login?redirect=%2Fplanner`), { kind: 'none' })
    assert.deepEqual(readMagicLinkCallback(`${origin}/#/planner`), { kind: 'none' })
  })

  it('detects the PKCE code Supabase appends to the query string', () => {
    assert.deepEqual(readMagicLinkCallback(`${origin}/?code=abc#/login?redirect=%2F`), { kind: 'pkce-code', code: 'abc' })
  })

  it('detects Supabase error redirects that overwrote the hash route', () => {
    const href = `${origin}/?error=access_denied&error_code=otp_expired&error_description=Email+link+is+invalid+or+has+expired#error=access_denied&error_code=otp_expired&error_description=Email+link+is+invalid+or+has+expired&sb=`

    assert.deepEqual(readMagicLinkCallback(href), {
      kind: 'error',
      errorCode: 'otp_expired',
      description: 'Email link is invalid or has expired',
    })
  })

  it('detects token_hash links in the query string or in the hash route query', () => {
    assert.deepEqual(readMagicLinkCallback(`${origin}/?token_hash=th&type=email#/login`), {
      kind: 'token-hash',
      tokenHash: 'th',
      otpType: 'email',
    })
    assert.deepEqual(readMagicLinkCallback(`${origin}/#/login?redirect=%2F&token_hash=th&type=magiclink`), {
      kind: 'token-hash',
      tokenHash: 'th',
      otpType: 'magiclink',
    })
  })

  it('rejects token_hash links with an unsupported type', () => {
    assert.deepEqual(readMagicLinkCallback(`${origin}/?token_hash=th&type=recovery#/login`), { kind: 'none' })
  })
})

describe('removeMagicLinkParams', () => {
  it('keeps the PKCE code for supabase-js when requested', () => {
    const href = `${origin}/?code=abc#/login?redirect=%2F`

    assert.equal(removeMagicLinkParams(href, { keepCode: true }), href)
    assert.equal(removeMagicLinkParams(href, { keepCode: false }), `${origin}/#/login?redirect=%2F`)
  })

  it('restores the login route when Supabase replaced the hash with error params', () => {
    const href = `${origin}/LifeOS/?error=access_denied&error_code=otp_expired#error=access_denied&error_code=otp_expired&sb=`

    assert.equal(removeMagicLinkParams(href, { keepCode: true }), `${origin}/LifeOS/#/login`)
  })

  it('strips token_hash params but keeps the redirect target', () => {
    assert.equal(
      removeMagicLinkParams(`${origin}/#/login?redirect=%2Fplanner&token_hash=th&type=email`, { keepCode: true }),
      `${origin}/#/login?redirect=%2Fplanner`,
    )
    assert.equal(
      removeMagicLinkParams(`${origin}/?token_hash=th&type=email#/login`, { keepCode: true }),
      `${origin}/#/login`,
    )
  })

  it('leaves unrelated URLs untouched', () => {
    const href = `${origin}/#/planner?type=week`

    assert.equal(removeMagicLinkParams(href, { keepCode: false }), href)
  })

  it('routes bare PKCE callbacks to login and removes codes on failure as well as success', () => {
    assert.equal(removeMagicLinkParams(`${origin}/LifeOS/?code=abc`, { keepCode: false }), `${origin}/LifeOS/#/login`)
  })
})
