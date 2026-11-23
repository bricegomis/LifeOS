import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { cleanCode, garminNotice, waitsForCode, whenText } from './garmin-rules.ts'

describe('Garmin connection', () => {
  it('asks for the code only while Garmin waits for one', () => {
    assert.equal(garminNotice({ state: 'ok' }), null)
    assert.equal(garminNotice(null), null)
    assert.equal(garminNotice({ state: 'mfa_requested' })?.action, 'code')
    assert.equal(garminNotice({ state: 'code_expired' })?.action, 'reconnect')
    assert.equal(garminNotice({ state: 'credentials_refused' })?.tone, 'off')
    assert.equal(waitsForCode({ state: 'code_refused' }), true)
    assert.equal(waitsForCode({ state: 'code_expired' }), false)
  })

  it('accepts a six digit code typed with spaces', () => {
    assert.equal(cleanCode(' 123 456 '), '123456')
    assert.equal(cleanCode('12345'), null)
    assert.equal(cleanCode('12a456'), null)
  })

  it('says when in Paris time', () => {
    const now = new Date('2026-11-23T12:30:00Z')
    assert.equal(whenText('2026-11-23T12:02:00Z', now), 'aujourd’hui à 13 h 02')
    assert.equal(whenText('2026-11-22T06:00:00Z', now), 'hier à 7 h 00')
    assert.equal(whenText(null, now), 'jamais')
  })
})
