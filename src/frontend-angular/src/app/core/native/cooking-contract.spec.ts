import assert from 'node:assert/strict'
import { spawnSync, spawn } from 'node:child_process'
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { COOKING_MAX_AGE_MS, readCookingSession, mutateCookingSession } from './cooking-contract.ts'
import { NativeStateCoordinator, SupersededNativeOperation } from './native-state-coordinator.ts'
import { nativeAuthReturnPath, parseNativeUrl } from './native-url.ts'

const now = Date.now()
const legacy = {
  sessionId: 'cook-test', recipeId: null, recipeTitle: 'Soupe', steps: ['Couper', 'Cuire'],
  startedAt: new Date(now - 1000).toISOString(), updatedAt: new Date(now).toISOString(),
  completedStepIndices: [0, 0, -1, 99], isCompleted: true,
}

describe('cooking contract', () => {
  it('migrates old TS and Swift payloads without currentStepIndex and bounds counters', () => {
    const current = readCookingSession(legacy, now)!
    assert.equal(current.version, 2)
    assert.equal(current.revision, 0)
    assert.equal(current.updatedAt, legacy.updatedAt)
    assert.equal(current.isCompleted, false)
    assert.deepEqual(current.completedStepIndices, [0])
    const { startedAt, updatedAt: _updatedAt, ...oldSwift } = legacy
    assert.equal(readCookingSession({ ...oldSwift, startedAtIso: startedAt }, now)?.startedAt, startedAt)
    assert.equal(readCookingSession({ ...oldSwift, startedAtIso: startedAt }, now)?.updatedAt, startedAt)
  })

  it('expires at exactly eight hours, rejects future/invalid dates and unsupported versions', () => {
    assert.equal(readCookingSession(legacy, now - 2000), null)
    assert.equal(readCookingSession(legacy, now - 1000 + COOKING_MAX_AGE_MS), null)
    assert.ok(readCookingSession(legacy, now - 1001 + COOKING_MAX_AGE_MS))
    assert.throws(() => readCookingSession({ ...legacy, startedAt: 'invalid' }, now), /invalide/)
    assert.throws(() => readCookingSession({ ...legacy, version: 99 }, now), /Version/)
    assert.throws(() => readCookingSession({ ...legacy, revision: -1 }, now), /invalide/)
  })

  it('mutates the latest state instead of overwriting an Angular snapshot', () => {
    const initial = readCookingSession({ ...legacy, completedStepIndices: [] }, now)!
    const widgetUpdate = mutateCookingSession(initial, initial.sessionId, 'toggle', 0, now)
    const appUpdate = mutateCookingSession(widgetUpdate, initial.sessionId, 'toggle', 1, now)
    assert.deepEqual(appUpdate.completedStepIndices, [0, 1])
    assert.equal(appUpdate.revision, 2)
    assert.equal(appUpdate.isCompleted, true)
    assert.throws(() => mutateCookingSession(appUpdate, 'old-id', 'toggle', 0, now), /remplacée/)
    assert.throws(() => mutateCookingSession(appUpdate, initial.sessionId, 'toggle', 2, now), /Index/)
    assert.throws(() => mutateCookingSession(initial, initial.sessionId, 'complete', undefined, now + COOKING_MAX_AGE_MS), /expirée/)
  })
})

describe('identity write barrier', () => {
  it('invalidates in-flight and queued work, purges after the admitted write, and admits only the new identity', async () => {
    const coordinator = new NativeStateCoordinator()
    const token = coordinator.token()
    const writes: string[] = []
    let finish!: () => void
    const gate = new Promise<void>(resolve => { finish = resolve })
    const inFlight = coordinator.run(token, async () => { await gate; writes.push('old-write') })
    const rejected = assert.rejects(inFlight, SupersededNativeOperation)
    await Promise.resolve()
    const queued = assert.rejects(coordinator.run(token, async () => { writes.push('old-queued') }), SupersededNativeOperation)
    coordinator.pause()
    const purge = coordinator.cleanup(async () => { writes.push('purge') })
    finish()
    await Promise.all([rejected, queued, purge])
    await assert.rejects(coordinator.run(coordinator.token(), async () => {}), SupersededNativeOperation)
    coordinator.resume()
    await coordinator.run(coordinator.token(), async () => { writes.push('new-write') })
    assert.deepEqual(writes, ['old-write', 'purge', 'new-write'])
  })
})

describe('native URLs', () => {
  it('preserves the nested magic-link return route while rejecting external redirects', () => {
    assert.equal(nativeAuthReturnPath('/login?redirect=%2Fplanning%3Fdate%3D2026-10-13'), '/planning?date=2026-10-13')
    assert.equal(nativeAuthReturnPath('/login?redirect=https%3A%2F%2Fevil.example'), '/')
    assert.equal(nativeAuthReturnPath('/recipes'), '/recipes')
  })
  it('accepts exact cold/warm auth callbacks and internal planning links', () => {
    assert.deepEqual(parseNativeUrl('lifeos://auth/callback?code=x'), { kind: 'auth' })
    assert.deepEqual(parseNativeUrl('lifeos://login?error=access_denied'), { kind: 'auth' })
    assert.deepEqual(parseNativeUrl('lifeos://auth/callback#error=access_denied&error_description=User+denied'), { kind: 'auth' })
    assert.deepEqual(parseNativeUrl('lifeos:///planning?date=2026-10-13'), { kind: 'route', path: '/planning?date=2026-10-13' })
    assert.deepEqual(parseNativeUrl('lifeos://recipes'), { kind: 'route', path: '/recipes' })
  })
  it('rejects prefix lookalikes, credentials and external routes', () => {
    for (const url of ['lifeos://auth/callback-evil?code=x', 'lifeos://login-evil', 'https://example.org', 'lifeos://x@planning', 'lifeos://planning:42', 'lifeos:///planning#code=x']) {
      assert.throws(() => parseNativeUrl(url))
    }
  })
})

const binary = process.env['LIFEOS_SWIFT_CONTRACT_EXECUTABLE']
describe('real Swift ↔ TS contract', { skip: !binary }, () => {
  it('decodes actual TS JSON and re-encodes canonical names, dates and revision', () => {
    const { startedAt, updatedAt: _updatedAt, ...oldSwift } = legacy
    for (const payload of [legacy, { ...oldSwift, startedAtIso: startedAt }, readCookingSession(legacy, now)]) {
      const result = spawnSync(binary!, [], { input: JSON.stringify(payload), encoding: 'utf8' })
      assert.equal(result.status, 0, result.stderr)
      const decoded = JSON.parse(result.stdout)
      assert.equal(decoded.startedAtIso, undefined)
      assert.equal(decoded.currentStepIndex, undefined)
      assert.deepEqual(readCookingSession(decoded, now), readCookingSession(payload, now))
    }
  })
  it('applies the same eight-hour boundary and invalid-contract rejection in native code', () => {
    const started = Date.parse(legacy.startedAt)
    for (const [at, expected] of [
      [started - 1, true], [started + COOKING_MAX_AGE_MS - 1, false], [started + COOKING_MAX_AGE_MS, true],
    ] as const) {
      const result = spawnSync(binary!, ['--expired-at', new Date(at).toISOString()], { input: JSON.stringify(legacy), encoding: 'utf8' })
      assert.equal(result.status, 0, result.stderr)
      assert.equal(result.stdout, String(expected))
    }
    for (const invalid of [{ ...legacy, version: 99 }, { ...legacy, revision: -1 }, { ...legacy, startedAt: 'invalid' }]) {
      const result = spawnSync(binary!, [], { input: JSON.stringify(invalid), encoding: 'utf8' })
      assert.equal(result.status, 1)
    }
  })
  it('coordinates concurrent mutations from separate native processes', async () => {
    const dir = mkdtempSync(join(tmpdir(), 'lifeos-contract-'))
    const file = join(dir, 'cooking.json')
    writeFileSync(file, JSON.stringify(readCookingSession({ ...legacy, completedStepIndices: [] }, now)))
    try {
      await Promise.all(Array.from({ length: 12 }, (_, i) => new Promise<void>((resolve, reject) => {
        const child = spawn(binary!, ['--toggle', file, String(i % 2)])
        let error = ''
        child.stderr.on('data', data => { error += data })
        child.on('error', reject)
        child.on('exit', code => code === 0 ? resolve() : reject(new Error(error)))
      })))
      const session = readCookingSession(JSON.parse(readFileSync(file, 'utf8')), now)!
      assert.equal(session.revision, 12)
      assert.deepEqual(session.completedStepIndices, [])
    } finally {
      rmSync(dir, { recursive: true })
    }
  })
  it('distinguishes an absent or expired native file from a corrupt one', () => {
    const dir = mkdtempSync(join(tmpdir(), 'lifeos-read-contract-'))
    const file = join(dir, 'cooking.json')
    try {
      const missing = spawnSync(binary!, ['--read', file], { encoding: 'utf8' })
      assert.equal(missing.status, 0, missing.stderr)
      assert.equal(missing.stdout, 'null')
      writeFileSync(file, JSON.stringify({ ...legacy, startedAt: new Date(now - COOKING_MAX_AGE_MS - 1000).toISOString() }))
      const expired = spawnSync(binary!, ['--read', file], { encoding: 'utf8' })
      assert.equal(expired.status, 0, expired.stderr)
      assert.equal(expired.stdout, 'null')
      writeFileSync(file, '{"broken"')
      const corrupt = spawnSync(binary!, ['--read', file], { encoding: 'utf8' })
      assert.equal(corrupt.status, 1)
      assert.ok(corrupt.stderr)
    } finally {
      rmSync(dir, { recursive: true })
    }
  })
})
