import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { isLaunchHash, LAST_ROUTE_MAX_AGE_MS, restorableRoute, serializeLastRoute } from './last-route.ts'

const now = Date.UTC(2026, 10, 26, 8, 0)

describe('last visited page', () => {
  it('recognises the start address only', () => {
    for (const hash of ['', '#', '#/']) assert.equal(isLaunchHash(hash), true)
    for (const hash of ['#/today', '#/sports/routine/abc', '#/login']) assert.equal(isLaunchHash(hash), false)
  })

  it('keeps internal pages and reopens them while recent', () => {
    const raw = serializeLastRoute('/sports/routine/abc-1', now)
    assert.ok(raw)
    assert.equal(restorableRoute(raw, now + 60_000), '/sports/routine/abc-1')
    assert.equal(restorableRoute(raw, now + LAST_ROUTE_MAX_AGE_MS), '/sports/routine/abc-1')
  })

  it('forgets old, future, root, login, unknown and malformed entries', () => {
    assert.equal(restorableRoute(serializeLastRoute('/tasks', now), now + LAST_ROUTE_MAX_AGE_MS + 1), null)
    assert.equal(restorableRoute(serializeLastRoute('/tasks', now + 60_000), now), null)
    for (const url of ['/', '/login', '/login?redirect=%2Ftasks', '/unknown', 'https://evil.example']) {
      assert.equal(serializeLastRoute(url, now), null)
    }
    for (const raw of [null, '', 'nope', '[]', '{"url":"/tasks"}', '{"url":"//evil.example","at":0}', JSON.stringify({ url: '/unknown', at: now })]) {
      assert.equal(restorableRoute(raw, now), null)
    }
  })
})
