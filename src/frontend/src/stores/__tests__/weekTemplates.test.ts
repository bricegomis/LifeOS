import assert from 'node:assert/strict'
import { after, before, describe, it } from 'node:test'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { createServer, type ViteDevServer } from 'vite'
import type { CompositeDish, DayContext, Weekday } from '../../types.ts'

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

  it('uses the chosen week type for its meal rules and day-context snapshot', () => {
    saved.clear()
    setActivePinia(createPinia())
    const contextStore = contextModule.useWeekContextStore()
    const plannerStore = plannerModule.useWeekPlannerStore()
    contextStore.setReferenceWeekStartDate('2026-09-28')
    contextStore.setReferenceWeekMode('kids')
    contextStore.updateWorkLocation('kids', 'monday', 'office')
    contextStore.updateWorkLocation('solo', 'monday', 'home')

    const testDish = (id: string): CompositeDish => ({
      id,
      kind: 'composite',
      name: id,
      icon: '🍽️',
      preparationTimeMinutes: 10,
      estimatedCalories: 500,
      estimatedProteinGrams: 25,
      estimatedCarbohydrateGrams: 50,
      estimatedFatGrams: 15,
      suitableForBreakfast: false,
      suitableForLunch: true,
      suitableForDinner: true,
      active: true,
    })
    const library = {
      ...libraryModule,
      compositeDishes: [
        ...libraryModule.compositeDishes,
        testDish('kids-only-dish'),
        testDish('solo-only-dish'),
        testDish('shared-dish'),
      ],
    }
    const scopedRules = [
      {
        id: 'kids-rule',
        weekday: 'monday' as const,
        mealType: 'lunch' as const,
        target: { kind: 'dish' as const, dishId: 'kids-only-dish' },
        weekMode: 'kids' as const,
      },
      {
        id: 'solo-rule',
        weekday: 'monday' as const,
        mealType: 'lunch' as const,
        target: { kind: 'dish' as const, dishId: 'solo-only-dish' },
        weekMode: 'solo' as const,
      },
      {
        id: 'shared-rule',
        weekday: 'tuesday' as const,
        mealType: 'lunch' as const,
        target: { kind: 'dish' as const, dishId: 'shared-dish' },
      },
      {
        id: 'global-monday-rule',
        weekday: 'monday' as const,
        mealType: 'lunch' as const,
        target: { kind: 'dish' as const, dishId: 'shared-dish' },
      },
    ]

    const kidsPlan = generatorModule.createGeneratedWeekPlan({
      library,
      planningRules: scopedRules,
      frequencyRules: [],
      weekContext: contextStore.weekContext,
      startDate: '2026-10-05',
      weekMode: 'kids',
    })
    const soloPlan = generatorModule.createGeneratedWeekPlan({
      library,
      planningRules: scopedRules,
      frequencyRules: [],
      weekContext: contextStore.weekContext,
      startDate: '2026-10-05',
      weekMode: 'solo',
    })

    assert.equal(kidsPlan.weekMode, 'kids')
    assert.equal(kidsPlan.days[0]?.lunch.mealDefinition.id, 'kids-only-dish')
    assert.equal(kidsPlan.days[1]?.lunch.mealDefinition.id, 'shared-dish')
    assert.equal(kidsPlan.dayContexts?.monday.workLocation, 'office')
    assert.equal(soloPlan.weekMode, 'solo')
    assert.equal(soloPlan.days[0]?.lunch.mealDefinition.id, 'solo-only-dish')
    assert.equal(soloPlan.days[1]?.lunch.mealDefinition.id, 'shared-dish')
    assert.equal(soloPlan.dayContexts?.monday.workLocation, 'home')

    plannerStore.generateWeek([], [], contextStore.weekContext, '2026-10-05', 'kids')
    assert.equal(plannerStore.weekPlan.weekMode, 'kids')
  })

  it('lets a type-specific frequency objective replace the shared objective for the same target', () => {
    saved.clear()
    setActivePinia(createPinia())
    const contextStore = contextModule.useWeekContextStore()
    const targetDish = {
      id: 'frequency-target-dish',
      kind: 'composite' as const,
      name: 'Plat cible',
      icon: '🍽️',
      preparationTimeMinutes: 10,
      estimatedCalories: 500,
      estimatedProteinGrams: 25,
      estimatedCarbohydrateGrams: 50,
      estimatedFatGrams: 15,
      suitableForBreakfast: false,
      suitableForLunch: false,
      suitableForDinner: false,
      active: true,
    }
    const library = {
      ...libraryModule,
      compositeDishes: [...libraryModule.compositeDishes, targetDish],
    }
    const frequencyRules = [
      {
        id: 'shared-frequency',
        target: { kind: 'dish' as const, dishId: targetDish.id },
        targetCountPerWeek: 2,
      },
      {
        id: 'kids-frequency',
        target: { kind: 'dish' as const, dishId: targetDish.id },
        targetCountPerWeek: 5,
        weekMode: 'kids' as const,
      },
    ]
    const countTargetDishes = (plan: ReturnType<typeof generatorModule.createGeneratedWeekPlan>) =>
      plan.days.flatMap((day) => [day.breakfast, day.lunch, day.dinner])
        .filter((meal) => meal.mealDefinition.id === targetDish.id).length
    const generate = (weekMode: 'kids' | 'solo') =>
      generatorModule.createGeneratedWeekPlan({
        library,
        planningRules: [],
        frequencyRules,
        weekContext: contextStore.weekContext,
        startDate: '2026-10-05',
        weekMode,
      })

    assert.equal(countTargetDishes(generate('kids')), 5)
    assert.equal(countTargetDishes(generate('solo')), 2)
  })
})
