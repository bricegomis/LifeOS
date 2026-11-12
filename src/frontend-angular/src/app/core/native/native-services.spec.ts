import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { describe, it } from 'node:test'
import ts from 'typescript'
import { NativeStateCoordinator, SupersededNativeOperation } from './native-state-coordinator.ts'
import { parseNativeUrl } from './native-url.ts'
import { validatedReturnPath } from '../auth/auth-flow.ts'

function signal<T>(initial: T) {
  let value = initial
  return Object.assign(() => value, { set: (next: T) => { value = next } })
}

// Execute the production service code with deterministic ports; Angular's DI/rendering
// are covered by the Angular build, not reimplemented by these node:test checks.
function harness() {
  const storage = new Map<string, string>()
  const coordinator = new NativeStateCoordinator()
  const authToken = Symbol('auth')
  const apiToken = Symbol('api')
  const routerToken = Symbol('router')
  const providers = new Map<unknown, unknown>()
  const effects: (() => void)[] = []
  const auth = { ready: signal(true), user: signal<{ id: string } | null>({ id: 'user-a' }), ensureReady: async () => {}, handleNativeCallback: async (_url: string) => '/recipes' }
  const writes: string[] = []
  let get = async (path: string): Promise<unknown> => path === '/households/current' ? { id: 'house-a' } : []
  const api = { get: (path: string) => get(path) }
  const bridge = {
    clearCookingSession: async () => { storage.delete('lifeos.native.cookingSession'); writes.push('clear-cooking') },
    saveWidgetSnapshot: async ({ snapshot }: { snapshot: unknown }) => { storage.set('widget', JSON.stringify(snapshot)); writes.push('snapshot') },
    clearWidgetSnapshot: async () => { storage.delete('widget'); writes.push('clear-widget') },
    reloadWidgetTimelines: async () => {},
    endLiveActivity: async () => { writes.push('end-activity') },
  }
  const router = { navigateByUrl: async (url: string) => { writes.push(url); return true } }
  providers.set(authToken, auth)
  providers.set(apiToken, api)
  providers.set(routerToken, router)
  const capApp = { addListener: async (_name: string, _callback: unknown) => {}, getLaunchUrl: async (): Promise<{ url: string } | undefined> => undefined }
  const notifications = {
    getPending: async () => ({ notifications: [{ id: 7 }] }),
    cancel: async () => { writes.push('cancel-pending') },
  }
  const imports: Record<string, unknown> = {
    '@angular/core': { Injectable: () => (target: unknown) => target, signal, computed: (fn: () => unknown) => fn, inject: (token: unknown) => providers.get(token), effect: (fn: () => void) => { effects.push(fn) } },
    '@angular/router': { Router: routerToken },
    '@capacitor/core': { Capacitor: { isNativePlatform: () => true } },
    '@capacitor/app': { App: capApp },
    '@capacitor/local-notifications': { LocalNotifications: notifications },
    'rxjs': { firstValueFrom: (value: unknown) => Promise.resolve(value) },
    '@/app/core/api/lifeos-api.service': { LifeosApiService: apiToken, apiErrorMessage: (error: unknown) => error instanceof Error ? error.message : String(error) },
    '@/app/core/auth/auth.service': { AuthService: authToken },
    '@/app/core/auth/auth-flow': { validatedReturnPath },
    './native-bridge': { LifeosNativeBridge: bridge, requireNativeBridge: () => {} },
    './native-state-coordinator': { nativeStateCoordinator: coordinator, SupersededNativeOperation },
    './native-url': { parseNativeUrl },
  }
  function load(name: string): Record<string, new () => Record<string, unknown>> {
    const source = readFileSync(new URL(name, import.meta.url), 'utf8')
    const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true } }).outputText
    const exports = {}
    runInNewContext(js, {
      exports, require: (id: string) => { assert.ok(id in imports, `Missing port ${id}`); return imports[id] },
      localStorage: { getItem: (key: string) => storage.get(key) ?? null, setItem: (key: string, value: string) => storage.set(key, value), removeItem: (key: string) => storage.delete(key) },
      Error, Date, Math, JSON, Set, Promise, Intl, console,
    })
    return exports
  }
  const Sync = load('native-sync.service.ts')['NativeSyncService']!
  const sync = new Sync() as {
    syncNativeState: () => Promise<void>; purgeAll: () => Promise<void>; initListeners: () => Promise<void>
    lastSyncError: ReturnType<typeof signal<string | null>>; syncStatus: ReturnType<typeof signal<string>>
  }
  const DeepLink = load('deep-link.service.ts')['DeepLinkService']!
  const links = new DeepLink() as {
    initListeners: () => Promise<void>; handleIncomingUrl: (url: string) => Promise<void>
    lastError: ReturnType<typeof signal<string | null>>
  }
  const settle = () => new Promise<void>(resolve => setImmediate(resolve))
  const init = async () => {
    storage.set('lifeos.native.activeIdentity', 'user-a:house-a')
    effects[0]!()
    await settle()
  }
  return { storage, coordinator, auth, writes, bridge, sync, links, capApp, effects, init, settle, notifications, setGet: (fn: typeof get) => { get = fn } }
}

describe('production native services through deterministic ports', () => {
  it('purges JS storage, notifications and native files, including a session left by an older version', async () => {
    const h = harness()
    await h.init()
    h.storage.set('lifeos.native.cookingSession', '{"sessionId":"legacy"}')
    await h.sync.purgeAll()
    assert.equal(h.storage.has('lifeos.native.cookingSession'), false)
    assert.equal(h.storage.has('lifeos.native.activeIdentity'), false)
    assert.equal(h.storage.has('widget'), false)
    assert.ok(h.writes.includes('end-activity'))
    assert.ok(h.writes.includes('cancel-pending'))
  })
  it('invalidates a sync waiting on the API before logout; it cannot republish', async () => {
    const h = harness()
    await h.init()
    let finish!: (value: unknown) => void
    const pending = new Promise(resolve => { finish = resolve })
    h.setGet(path => path === '/households/current' ? pending : Promise.resolve([]))
    const syncing = h.sync.syncNativeState()
    await h.sync.purgeAll()
    finish({ id: 'house-a' })
    await syncing
    assert.equal(h.storage.has('widget'), false)
    assert.equal(h.sync.syncStatus(), 'idle')
  })
  it('continues every cleanup surface after a failure and reports partial purge explicitly', async () => {
    const h = harness()
    await h.init()
    h.storage.set('lifeos.native.cookingSession', '{"sessionId":"legacy"}')
    h.writes.length = 0
    h.bridge.clearWidgetSnapshot = async () => { throw new Error('App Group unavailable') }
    await assert.rejects(h.sync.purgeAll(), /Purge partielle.*App Group unavailable/)
    assert.equal(h.storage.has('lifeos.native.cookingSession'), false)
    assert.ok(h.writes.includes('cancel-pending'))
    assert.equal(h.sync.syncStatus(), 'error')
  })
  it('keeps an existing same-user widget snapshot when the network fails, without claiming Angular offline support', async () => {
    const h = harness()
    await h.init()
    const original = h.storage.get('widget')
    h.setGet(async () => { throw new Error('offline') })
    await h.sync.syncNativeState()
    assert.equal(h.storage.get('widget'), original)
    assert.equal(h.sync.syncStatus(), 'stale')
    assert.equal(h.sync.lastSyncError(), 'offline')
  })
  it('clears the old identity even if the new account is offline', async () => {
    const h = harness()
    await h.init()
    h.setGet(async () => { throw new Error('offline') })
    h.auth.user.set({ id: 'user-b' })
    h.effects[0]!()
    await h.settle()
    assert.equal(h.storage.has('widget'), false)
    assert.equal(h.storage.has('lifeos.native.activeIdentity'), false)
  })
  it('awaits listener registration failures and surfaces them instead of leaking rejected promises', async () => {
    const h = harness()
    h.capApp.addListener = async () => { throw new Error('native listener failed') }
    await h.sync.initListeners()
    await h.links.initListeners()
    assert.equal(h.sync.lastSyncError(), 'native listener failed')
    assert.equal(h.links.lastError(), 'native listener failed')
  })
  it('handles the cold auth callback once and does not register the URL listener twice', async () => {
    const h = harness()
    let urlRegistrations = 0
    let exchanges = 0
    h.capApp.addListener = async () => { urlRegistrations++ }
    h.capApp.getLaunchUrl = async () => ({ url: 'lifeos://auth/callback?code=cold' })
    h.auth.handleNativeCallback = async () => { exchanges++; return '/recipes' }
    await h.links.initListeners()
    assert.ok(h.writes.includes('/recipes'))
    assert.equal(exchanges, 1)
    await h.links.initListeners()
    assert.equal(urlRegistrations, 1)
    assert.equal(exchanges, 1)
  })
  it('reports a failed widget write as stale after a first successful sync', async () => {
    const h = harness()
    await h.init()
    h.bridge.saveWidgetSnapshot = async () => { throw new Error('App Group unavailable') }
    await h.sync.syncNativeState()
    assert.equal(h.sync.syncStatus(), 'stale')
    assert.equal(h.sync.lastSyncError(), 'App Group unavailable')
  })
  it('awaits the coalesced rerun before reporting synchronization complete', async () => {
    const h = harness()
    await h.init()
    let finish!: (value: unknown) => void
    const gate = new Promise(resolve => { finish = resolve })
    let requests = 0
    h.setGet(async path => {
      if (path !== '/households/current') return []
      requests++
      return requests === 1 ? gate : { id: 'house-a' }
    })
    const first = h.sync.syncNativeState()
    let completed = false
    const coalesced = h.sync.syncNativeState().then(() => { completed = true })
    await Promise.resolve()
    assert.equal(completed, false)
    finish({ id: 'house-a' })
    await Promise.all([first, coalesced])
    assert.equal(requests, 2)
    assert.equal(h.sync.syncStatus(), 'synced')
    assert.equal(completed, true)
  })
  it('deduplicates a cold/warm callback and reports auth failures before returning to login', async () => {
    const h = harness()
    let calls = 0
    h.auth.handleNativeCallback = async () => { calls++; throw new Error('PKCE verifier missing') }
    await Promise.all([
      h.links.handleIncomingUrl('lifeos://auth/callback?code=same'),
      h.links.handleIncomingUrl('lifeos://auth/callback?code=same'),
    ])
    assert.equal(calls, 1)
    assert.equal(h.links.lastError(), 'PKCE verifier missing')
    assert.equal(h.writes.filter(x => x === '/login').length, 1)
  })
  it('publishes an empty widget snapshot and clears reminders, cooking session and Live Activity left by older versions', async () => {
    const h = harness()
    h.storage.set('lifeos.native.remindersPreferences', '{"enabled":true}')
    h.storage.set('lifeos.native.cookingSession', '{"sessionId":"legacy"}')
    await h.init()
    assert.equal(h.storage.has('lifeos.native.cookingSession'), false)
    assert.ok(h.writes.includes('end-activity'))
    const snapshot = JSON.parse(h.storage.get('widget')!) as { householdId: string; events: unknown[] }
    assert.equal(snapshot.householdId, 'house-a')
    assert.deepEqual(snapshot.events, [])
    assert.ok(h.writes.includes('cancel-pending'))
    await h.sync.purgeAll()
    assert.equal(h.storage.has('lifeos.native.remindersPreferences'), false)
  })
})
