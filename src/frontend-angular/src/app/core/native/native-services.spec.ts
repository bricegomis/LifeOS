import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { describe, it } from 'node:test'
import ts from 'typescript'
import { readCookingSession, mutateCookingSession } from './cooking-contract.ts'
import { NativeStateCoordinator, SupersededNativeOperation } from './native-state-coordinator.ts'
import { parseNativeUrl } from './native-url.ts'
import { validatedReturnPath } from '../auth/auth-flow.ts'
import { hashNotificationId } from './notification-id.ts'
import { DEFAULT_REMINDER_PREFERENCES, validateReminderPreferences } from './reminder-preferences.ts'

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
  const reminderToken = Symbol('reminders')
  const routerToken = Symbol('router')
  const providers = new Map<unknown, unknown>()
  const effects: (() => void)[] = []
  const auth = { ready: signal(true), user: signal<{ id: string } | null>({ id: 'user-a' }), ensureReady: async () => {}, handleNativeCallback: async (_url: string) => '/planning' }
  const writes: string[] = []
  const reminders = { clearPreferences: () => {}, cancelAll: async () => { writes.push('cancel-reminders') }, rescheduleNotifications: async () => { writes.push('reminders') } }
  let get = async (path: string): Promise<unknown> => path === '/households/current' ? { id: 'house-a' } : []
  const api = { get: (path: string) => get(path) }
  const bridge = {
    getCookingSession: async () => {
      const raw = storage.get('lifeos.native.cookingSession')
      return { session: raw ? readCookingSession(JSON.parse(raw)) : null }
    },
    saveCookingSession: async ({ session }: { session: unknown }) => { storage.set('lifeos.native.cookingSession', JSON.stringify(session)) },
    mutateCookingSession: async ({ sessionId, operation, stepIndex }: { sessionId: string; operation: 'toggle' | 'complete'; stepIndex?: number }) => {
      const raw = storage.get('lifeos.native.cookingSession')
      const session = mutateCookingSession(raw ? readCookingSession(JSON.parse(raw)) : null, sessionId, operation, stepIndex)
      storage.set('lifeos.native.cookingSession', JSON.stringify(session))
      return { session }
    },
    clearCookingSession: async () => { storage.delete('lifeos.native.cookingSession'); writes.push('clear-cooking') },
    saveWidgetSnapshot: async ({ snapshot }: { snapshot: unknown }) => { storage.set('widget', JSON.stringify(snapshot)); writes.push('snapshot') },
    clearWidgetSnapshot: async () => { storage.delete('widget'); writes.push('clear-widget') },
    reloadWidgetTimelines: async () => {},
    endLiveActivity: async () => { writes.push('end-activity') },
    startLiveActivity: async () => ({ disabled: true }),
    updateLiveActivity: async () => {},
  }
  const router = { navigateByUrl: async (url: string) => { writes.push(url); return true } }
  providers.set(authToken, auth)
  providers.set(apiToken, api)
  providers.set(reminderToken, reminders)
  providers.set(routerToken, router)
  const capApp = { addListener: async (_name: string, _callback: unknown) => {}, getLaunchUrl: async (): Promise<{ url: string } | undefined> => undefined }
  const notifications = {
    addListener: async () => {},
    checkPermissions: async () => ({ display: 'denied' }),
    requestPermissions: async () => { writes.push('request-permission'); return { display: 'denied' } },
    getPending: async () => ({ notifications: [{ id: 7 }] }),
    cancel: async () => { writes.push('cancel-pending') },
    schedule: async () => { writes.push('schedule') },
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
    '@/app/pages/planning/calendar': { monday: () => '2026-10-12', isoDate: () => '2026-10-13', addDays: () => '2026-10-19' },
    './event-projection': { projectEventsFromWeeks: () => [] },
    './local-reminders.service': { LocalRemindersService: reminderToken },
    './native-bridge': { LifeosNativeBridge: bridge, requireNativeBridge: () => {} },
    './recipe-steps': { splitInstructionsIntoSteps: () => ['Couper', 'Cuire'] },
    './cooking-contract': { readCookingSession, COOKING_STORAGE_KEY: 'lifeos.native.cookingSession' },
    './native-state-coordinator': { nativeStateCoordinator: coordinator, SupersededNativeOperation },
    './native-url': { parseNativeUrl },
    './notification-id': { hashNotificationId },
    './reminder-preferences': { DEFAULT_REMINDER_PREFERENCES, validateReminderPreferences },
  }
  function load(name: string): Record<string, new () => Record<string, unknown>> {
    const source = readFileSync(new URL(name, import.meta.url), 'utf8')
    const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true } }).outputText
    const exports = {}
    runInNewContext(js, {
      exports, require: (id: string) => { assert.ok(id in imports, `Missing port ${id}`); return imports[id] },
      localStorage: { getItem: (key: string) => storage.get(key) ?? null, setItem: (key: string, value: string) => storage.set(key, value), removeItem: (key: string) => storage.delete(key) },
      Error, Date, Math, JSON, Set, Promise, console, crypto: { randomUUID },
    })
    return exports
  }
  const Cooking = load('cooking-session.service.ts')['CookingSessionService']!
  const cooking = new Cooking() as {
    currentSession: ReturnType<typeof signal<ReturnType<typeof readCookingSession>>>
    lastError: ReturnType<typeof signal<string | null>>
    startSession: (title: string, id: null, raw: string) => Promise<unknown>
    restoreSession: () => Promise<unknown>
    toggleStep: (index: number) => Promise<void>
  }
  providers.set(Cooking, cooking)
  imports['./cooking-session.service'] = { CookingSessionService: Cooking }
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
  const Reminders = load('local-reminders.service.ts')['LocalRemindersService']!
  const nativeReminders = new Reminders() as {
    checkPermissions: () => Promise<boolean>
    requestPermissions: () => Promise<boolean>
    rescheduleNotifications: (events: unknown[], prefs?: typeof DEFAULT_REMINDER_PREFERENCES) => Promise<void>
    savePreferences: (prefs: typeof DEFAULT_REMINDER_PREFERENCES) => void
    getPreferences: () => typeof DEFAULT_REMINDER_PREFERENCES
    clearPreferences: () => void
  }
  const settle = () => new Promise<void>(resolve => setImmediate(resolve))
  const init = async () => {
    storage.set('lifeos.native.activeIdentity', 'user-a:house-a')
    effects[0]!()
    await settle()
  }
  return { storage, coordinator, auth, writes, bridge, cooking, sync, links, capApp, effects, init, settle, nativeReminders, notifications, setGet: (fn: typeof get) => { get = fn } }
}

describe('production native services through deterministic ports', () => {
  it('purges JS storage, memory, notifications and native files, with no restore resurrection', async () => {
    const h = harness()
    await h.init()
    await h.cooking.startSession('Soupe', null, 'Couper\nCuire')
    assert.ok(h.cooking.currentSession())
    assert.match(h.cooking.currentSession()!.sessionId, /^cook-[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/)
    await h.sync.purgeAll()
    assert.equal(h.cooking.currentSession(), null)
    assert.equal(h.storage.has('lifeos.native.cookingSession'), false)
    assert.equal(h.storage.has('lifeos.native.activeIdentity'), false)
    assert.equal(h.storage.has('widget'), false)
    await assert.rejects(h.cooking.restoreSession(), SupersededNativeOperation)
    assert.ok(h.writes.includes('cancel-reminders'))
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
    await h.cooking.startSession('Soupe', null, 'Couper')
    h.bridge.clearWidgetSnapshot = async () => { throw new Error('App Group unavailable') }
    await assert.rejects(h.sync.purgeAll(), /Purge partielle.*App Group unavailable/)
    assert.equal(h.cooking.currentSession(), null)
    assert.equal(h.storage.has('lifeos.native.cookingSession'), false)
    assert.ok(h.writes.includes('cancel-reminders'))
    assert.equal(h.sync.syncStatus(), 'error')
  })
  it('purges after an already admitted native write and never permits a cooking write to recreate old state', async () => {
    const h = harness()
    await h.init()
    let finish!: () => void
    const gate = new Promise<void>(resolve => { finish = resolve })
    h.bridge.saveCookingSession = async ({ session }) => { await gate; h.storage.set('lifeos.native.cookingSession', JSON.stringify(session)) }
    const starting = h.cooking.startSession('Soupe', null, 'Couper')
    const rejected = assert.rejects(starting, SupersededNativeOperation)
    await Promise.resolve()
    const purging = h.sync.purgeAll()
    finish()
    await Promise.all([rejected, purging])
    assert.equal(h.cooking.currentSession(), null)
    assert.equal(h.storage.has('lifeos.native.cookingSession'), false)
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
  it('clears the old identity immediately even if the new account is offline', async () => {
    const h = harness()
    await h.init()
    await h.cooking.startSession('Soupe', null, 'Couper')
    h.setGet(async () => { throw new Error('offline') })
    h.auth.user.set({ id: 'user-b' })
    h.effects[0]!()
    assert.equal(h.cooking.currentSession(), null)
    await h.settle()
    assert.equal(h.storage.has('widget'), false)
    assert.equal(h.storage.has('lifeos.native.cookingSession'), false)
  })
  it('does not use a stale JS copy when a widget completes another step', async () => {
    const h = harness()
    await h.init()
    await h.cooking.startSession('Soupe', null, 'Couper')
    const initial = h.cooking.currentSession()!
    await h.bridge.mutateCookingSession({ sessionId: initial.sessionId, operation: 'toggle', stepIndex: 0 })
    await h.cooking.toggleStep(1)
    assert.deepEqual(h.cooking.currentSession()?.completedStepIndices, [0, 1])
    assert.equal(h.cooking.currentSession()?.revision, 2)
  })
  it('awaits listener registration failures and surfaces them instead of leaking rejected promises', async () => {
    const h = harness()
    h.capApp.addListener = async () => { throw new Error('native listener failed') }
    await h.sync.initListeners()
    await h.links.initListeners()
    assert.equal(h.sync.lastSyncError(), 'native listener failed')
    assert.equal(h.links.lastError(), 'native listener failed')
  })
  it('handles the cold auth callback despite notification registration failure and retries only the failed listener', async () => {
    const h = harness()
    let urlRegistrations = 0
    let notificationRegistrations = 0
    let exchanges = 0
    h.capApp.addListener = async () => { urlRegistrations++ }
    h.notifications.addListener = async () => { notificationRegistrations++; throw new Error('notifications unavailable') }
    h.capApp.getLaunchUrl = async () => ({ url: 'lifeos://auth/callback?code=cold' })
    h.auth.handleNativeCallback = async () => { exchanges++; return '/planning' }
    await h.links.initListeners()
    assert.ok(h.writes.includes('/planning'))
    assert.equal(exchanges, 1)
    h.notifications.addListener = async () => { notificationRegistrations++ }
    await h.links.initListeners()
    assert.equal(urlRegistrations, 1)
    assert.equal(notificationRegistrations, 2)
    assert.equal(exchanges, 1)
  })
  it('restores cuisine after verified household identity even when planner fetch fails', async () => {
    const h = harness()
    await h.init()
    await h.cooking.startSession('Soupe', null, 'Couper')
    const session = h.cooking.currentSession()
    h.cooking.currentSession.set(null)
    h.setGet(async path => {
      if (path === '/households/current') return { id: 'house-a' }
      throw new Error('planner offline')
    })
    await h.sync.syncNativeState()
    assert.deepEqual(h.cooking.currentSession(), readCookingSession(session))
    assert.equal(h.sync.syncStatus(), 'stale')
    assert.equal(h.sync.lastSyncError(), 'planner offline')
  })
  it('awaits the coalesced rerun before reporting reminder synchronization complete', async () => {
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
  it('keeps reminders opt-in, cancels pending reminders on refusal and distinguishes permission errors', async () => {
    const h = harness()
    await h.nativeReminders.rescheduleNotifications([])
    assert.equal(h.writes.includes('request-permission'), false)
    assert.equal(h.writes.includes('schedule'), false)
    await assert.rejects(h.nativeReminders.rescheduleNotifications([], { ...DEFAULT_REMINDER_PREFERENCES, enabled: true }), /Permission/)
    assert.ok(h.writes.includes('cancel-pending'))
    assert.equal(await h.nativeReminders.requestPermissions(), false)
    h.notifications.checkPermissions = async () => { throw new Error('permission bridge failed') }
    await assert.rejects(h.nativeReminders.checkPermissions(), /permission bridge failed/)
    h.nativeReminders.savePreferences({ ...DEFAULT_REMINDER_PREFERENCES, enabled: true })
    h.nativeReminders.clearPreferences()
    assert.equal(h.nativeReminders.getPreferences().enabled, false)
  })
})
