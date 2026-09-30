import assert from 'node:assert/strict'
import { after, before, describe, it } from 'node:test'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { createServer, type ViteDevServer } from 'vite'
import type { DayContext, Weekday } from '../../types.ts'

let server: ViteDevServer
let contextModule: typeof import('../weekContext.ts')
let generatorModule: typeof import('../../data/weekGenerator.ts')
let libraryModule: typeof import('../../data/localLibrary.ts')
let plannerModule: typeof import('../weekPlanner.ts')
const saved = new Map<string, string>()

before(async () => {
  Object.defineProperty(globalThis, 'window', {
    configurable: true,
    value: {
      location: { href: 'http://localhost/#/planner', origin: 'http://localhost' },
      localStorage: {
        getItem: (key: string) => saved.get(key) ?? null,
        setItem: (key: string, value: string) => { saved.set(key, value) },
      },
    },
  })
  server = await createServer({ server: { middlewareMode: true }, appType: 'custom' })
  contextModule = await server.ssrLoadModule('/src/stores/weekContext.ts')
  generatorModule = await server.ssrLoadModule('/src/data/weekGenerator.ts')
  libraryModule = await server.ssrLoadModule('/src/data/localLibrary.ts')
  plannerModule = await server.ssrLoadModule('/src/stores/weekPlanner.ts')
})

after(async () => {
  await server?.close()
  delete (globalThis as { window?: unknown }).window
})

function legacyDays(): Record<Weekday, DayContext> {
  return Object.fromEntries(
    contextModule.weekdays.map((weekday) => [
      weekday,
      { workLocation: weekday === 'monday' ? 'office' : 'off', bikeCommute: weekday === 'monday' },
    ]),
  ) as Record<Weekday, DayContext>
}

describe('week templates', () => {
  it('migrates legacy reference days to independent models without losing alternation', async () => {
    saved.clear()
    saved.set('lifeos.context.v1', JSON.stringify({
      schemaVersion: 2,
      data: {
        alternatingWeekConfig: { referenceWeekStartDate: '2026-09-28', referenceWeekMode: 'kids' },
        weekModeOverrides: [{ weekStartDate: '2026-10-05', mode: 'kids' }],
        days: legacyDays(),
      },
    }))
    setActivePinia(createPinia())
    const store = contextModule.useWeekContextStore()
    assert.deepEqual(store.weekContext.templates.kids, legacyDays())
    assert.deepEqual(store.weekContext.templates.solo, legacyDays())
    assert.notEqual(store.weekContext.templates.kids, store.weekContext.templates.solo)
    assert.equal(contextModule.getWeekMode('2026-10-05', store.weekContext.alternatingWeekConfig, store.weekContext.weekModeOverrides), 'kids')
    store.updateWorkLocation('solo', 'monday', 'home')
    await nextTick()
    assert.equal(store.weekContext.templates.kids.monday.workLocation, 'office')
    assert.equal(store.weekContext.templates.solo.monday.bikeCommute, false)
    assert.equal(JSON.parse(saved.get('lifeos.context.v1')!).schemaVersion, 3)
  })

  it('selects each model by anchored calendar week, applies exceptions, and snapshots it on generation', () => {
    saved.clear()
    setActivePinia(createPinia())
    const store = contextModule.useWeekContextStore()
    store.setReferenceWeekStartDate('2026-09-28')
    store.setReferenceWeekMode('kids')
    store.updateWorkLocation('kids', 'monday', 'office')
    store.updateBikeCommute('kids', 'monday', true)
    store.updateWorkLocation('solo', 'monday', 'home')
    const generate = (startDate: string) => generatorModule.createGeneratedWeekPlan({
      library: libraryModule,
      planningRules: [],
      frequencyRules: [],
      weekContext: store.weekContext,
      startDate,
    })
    const kids = generate('2026-09-28')
    const solo = generate('2026-10-05')
    assert.equal(kids.weekMode, 'kids')
    assert.deepEqual(kids.dayContexts?.monday, { workLocation: 'office', bikeCommute: true })
    assert.equal(solo.weekMode, 'solo')
    assert.deepEqual(solo.dayContexts?.monday, { workLocation: 'home', bikeCommute: false })
    assert.equal(generate('2026-10-12').weekMode, 'kids')
    assert.equal(generate('2026-10-05').weekMode, 'solo')
    store.upsertWeekModeOverride({ weekStartDate: '2026-10-05', mode: 'kids' })
    assert.equal(generate('2026-10-05').weekMode, 'kids')
    store.updateWorkLocation('kids', 'monday', 'off')
    assert.equal(kids.dayContexts?.monday.workLocation, 'office')
  })

  it('generates the requested next week and restores its selected model and days on reload', () => {
    saved.clear()
    setActivePinia(createPinia())
    const contextStore = contextModule.useWeekContextStore()
    const plannerStore = plannerModule.useWeekPlannerStore()
    const previousPlan = plannerStore.weekPlan
    contextStore.setReferenceWeekStartDate('2026-09-28')
    contextStore.setReferenceWeekMode('kids')
    contextStore.updateWorkLocation('solo', 'tuesday', 'office')
    contextStore.updateBikeCommute('solo', 'tuesday', true)
    plannerStore.generateWeek([], [], contextStore.weekContext, '2026-10-05')
    assert.equal(plannerStore.weekPlan.startDate, '2026-10-05')
    assert.equal(plannerStore.weekPlan.weekMode, 'solo')
    assert.deepEqual(plannerStore.weekPlan.dayContexts?.tuesday, { workLocation: 'office', bikeCommute: true })
    assert.equal(previousPlan.startDate !== plannerStore.weekPlan.startDate, true)
    assert.throws(() => plannerStore.generateWeek([], [], contextStore.weekContext, 'invalid-date'))
    assert.equal(plannerStore.weekPlan.startDate, '2026-10-05')
    setActivePinia(createPinia())
    const restored = plannerModule.useWeekPlannerStore().weekPlan
    assert.equal(restored.weekMode, 'solo')
    assert.deepEqual(restored.dayContexts?.tuesday, { workLocation: 'office', bikeCommute: true })
    assert.deepEqual(restored.days.map((day) => day.activity.id), plannerStore.weekPlan.days.map((day) => day.activity.id))
    assert.deepEqual(restored.days.map((day) => day.lunch.mealDefinition.id), plannerStore.weekPlan.days.map((day) => day.lunch.mealDefinition.id))
  })
})
