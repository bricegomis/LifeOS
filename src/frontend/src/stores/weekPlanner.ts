import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getCurrentUserId } from '@/services/supabase/auth'
import { createGeneratedWeekPlan } from '@/data/weekGenerator'
import {
  activities,
  componentsByType,
  compositeDishes,
  dishesForMealType,
  mealComponents,
} from '@/data/localLibrary'
import { createDemoWeekPlan, createMealSlot, totalsForMeal } from '@/data/demoWeek'
import { weekdays } from '@/stores/weekContext'
import { upsertWeekPlan } from '@/services/supabase/lifeosRepository'
import type {
  ComponentType,
  FrequencyRule,
  Activity,
  CompositeDish,
  DayPlan,
  MealComponent,
  MealDefinition,
  MealSlot,
  MealType,
  PlanningRule,
  WeekContext,
  WeekPlan,
} from '@/types'

interface StoredWeekPlan {
  schemaVersion: 1
  data: WeekPlan
}

const STORAGE_KEY = 'lifeos.weekPlan.v1'
const SCHEMA_VERSION = 1

const demoWeekPlan = createDemoWeekPlan()
const componentById = new Map(mealComponents.map((component) => [component.id, component]))
const dishById = new Map(compositeDishes.map((dish) => [dish.id, dish]))
const activityById = new Map(activities.map((activity) => [activity.id, activity]))

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isWeekPlanStatus(value: unknown): value is WeekPlan['status'] {
  return value === 'Draft' || value === 'Generated' || value === 'Validated' || value === 'Archived'
}

function isMealType(value: unknown): value is MealType {
  return value === 'breakfast' || value === 'lunch' || value === 'dinner'
}

function isValidDateString(value: string): boolean {
  return !Number.isNaN(new Date(`${value}T00:00:00`).getTime())
}

export function toLocalDateKey(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')

  return `${year}-${month}-${day}`
}

function parseLocalDateKey(dateKey: string): Date | null {
  const date = new Date(`${dateKey}T00:00:00`)

  return Number.isNaN(date.getTime()) ? null : date
}

function weekPlanDayKey(startDate: string, dayIndex: number): string | null {
  const weekStart = parseLocalDateKey(startDate)

  if (!weekStart) {
    return null
  }

  const day = new Date(weekStart)
  day.setDate(day.getDate() + dayIndex)

  return toLocalDateKey(day)
}

export function resolveDayPlanForDate(
  weekPlan: WeekPlan,
  referenceDate: Date = new Date(),
): { dayIndex: number; dayPlan: DayPlan } | null {
  const targetKey = toLocalDateKey(referenceDate)

  for (const [dayIndex, dayPlan] of weekPlan.days.entries()) {
    const dayKey = weekPlanDayKey(weekPlan.startDate, dayIndex)

    if (dayKey === targetKey) {
      return { dayIndex, dayPlan }
    }
  }

  return null
}

function isDishCompatible(dish: CompositeDish, mealType: MealType): boolean {
  if (mealType === 'breakfast') {
    return dish.suitableForBreakfast
  }

  if (mealType === 'lunch') {
    return dish.suitableForLunch
  }

  return dish.suitableForDinner
}

function resolveMealDefinition(value: unknown, mealType: MealType): MealDefinition | null {
  if (!isRecord(value) || typeof value.kind !== 'string') {
    return null
  }

  if (value.kind === 'composite' && typeof value.id === 'string') {
    const dish = dishById.get(value.id)

    return dish && isDishCompatible(dish, mealType) ? dish : null
  }

  if (value.kind !== 'assembled' || !Array.isArray(value.components)) {
    return null
  }

  const resolvedComponents = value.components
    .map((component) => {
      if (!isRecord(component) || typeof component.id !== 'string') {
        return null
      }

      return componentById.get(component.id) ?? null
    })
    .filter((component): component is MealComponent => Boolean(component))

  if (resolvedComponents.length === 0) {
    return null
  }

  return {
    id: typeof value.id === 'string' ? value.id : `restored-${mealType}`,
    kind: 'assembled',
    components: resolvedComponents,
    preparationTimeMinutes:
      typeof value.preparationTimeMinutes === 'number' && Number.isFinite(value.preparationTimeMinutes)
        ? value.preparationTimeMinutes
        : 0,
  }
}

function cloneMealDefinition(mealDefinition: MealDefinition): MealDefinition {
  if (mealDefinition.kind === 'composite') {
    return { ...mealDefinition }
  }

  return {
    ...mealDefinition,
    components: [...mealDefinition.components],
  }
}

function cloneMealSlot(slot: MealSlot): MealSlot {
  return {
    ...slot,
    mealDefinition: cloneMealDefinition(slot.mealDefinition),
  }
}

function cloneDayPlan(day: DayPlan): DayPlan {
  return {
    ...day,
    breakfast: cloneMealSlot(day.breakfast),
    lunch: cloneMealSlot(day.lunch),
    dinner: cloneMealSlot(day.dinner),
    activity: { ...day.activity },
  }
}

function sanitizeMealSlot(value: unknown, mealType: MealType, fallback: MealSlot): MealSlot {
  if (!isRecord(value) || !isMealType(value.mealType) || value.mealType !== mealType) {
    return cloneMealSlot(fallback)
  }

  const mealDefinition = resolveMealDefinition(value.mealDefinition, mealType)

  if (!mealDefinition) {
    return cloneMealSlot(fallback)
  }

  return createMealSlot(
    typeof value.id === 'string' ? value.id : fallback.id,
    mealType,
    mealDefinition,
  )
}

function sanitizeActivity(value: unknown, fallback: Activity): Activity {
  if (!isRecord(value) || typeof value.id !== 'string') {
    return fallback
  }

  return activityById.get(value.id) ?? fallback
}

function sanitizeDayPlan(value: unknown, fallback: DayPlan): DayPlan {
  if (!isRecord(value)) {
    return cloneDayPlan(fallback)
  }

  return {
    id: typeof value.id === 'string' ? value.id : fallback.id,
    dateLabel: typeof value.dateLabel === 'string' ? value.dateLabel : fallback.dateLabel,
    shortDateLabel:
      typeof value.shortDateLabel === 'string' ? value.shortDateLabel : fallback.shortDateLabel,
    breakfast: sanitizeMealSlot(value.breakfast, 'breakfast', fallback.breakfast),
    lunch: sanitizeMealSlot(value.lunch, 'lunch', fallback.lunch),
    dinner: sanitizeMealSlot(value.dinner, 'dinner', fallback.dinner),
    activity: sanitizeActivity(value.activity, fallback.activity),
  }
}

function sanitizeWeekPlan(value: unknown): WeekPlan | null {
  if (!isRecord(value) || typeof value.startDate !== 'string') {
    return null
  }

  return {
    id: typeof value.id === 'string' ? value.id : demoWeekPlan.id,
    startDate: isValidDateString(value.startDate) ? value.startDate : demoWeekPlan.startDate,
    status: isWeekPlanStatus(value.status) ? value.status : demoWeekPlan.status,
    days: weekdays.map((weekday, index) => {
      const fallbackDay = demoWeekPlan.days[index] ?? demoWeekPlan.days[0]!
      const dayValue = isRecord(value.days) ? value.days[weekday] : undefined

      return sanitizeDayPlan(dayValue, fallbackDay)
    }),
  }
}

function loadWeekPlan(): WeekPlan {
  if (typeof window === 'undefined') {
    return createDemoWeekPlan()
  }

  try {
    const rawState = window.localStorage.getItem(STORAGE_KEY)

    if (!rawState) {
      return createDemoWeekPlan()
    }

    const storedWeekPlan = JSON.parse(rawState) as Partial<StoredWeekPlan>

    if (storedWeekPlan.schemaVersion !== SCHEMA_VERSION || !storedWeekPlan.data) {
      return createDemoWeekPlan()
    }

    return sanitizeWeekPlan(storedWeekPlan.data) ?? createDemoWeekPlan()
  } catch {
    return createDemoWeekPlan()
  }
}

function saveWeekPlan(weekPlan: WeekPlan): void {
  if (typeof window === 'undefined') {
    return
  }

  const state: StoredWeekPlan = {
    schemaVersion: SCHEMA_VERSION,
    data: weekPlan,
  }

  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

function persistWeekPlan(weekPlan: WeekPlan): void {
  saveWeekPlan(weekPlan)

  const userId = getCurrentUserId()

  if (userId) {
    void upsertWeekPlan(userId, weekPlan)
  }
}

export const useWeekPlannerStore = defineStore('weekPlanner', () => {
  const weekPlan = ref<WeekPlan>(loadWeekPlan())

  function mealSlot(dayId: string, mealType: MealType): MealSlot | null {
    const day = weekPlan.value.days.find((item) => item.id === dayId)

    return day ? day[mealType] : null
  }

  function refreshSlotTotals(slot: MealSlot): void {
    Object.assign(slot, totalsForMeal(slot.mealDefinition))
  }

  function resetDemoWeek(): void {
    weekPlan.value = createDemoWeekPlan()
    persistWeekPlan(weekPlan.value)
  }

  function generateWeek(
    planningRules: PlanningRule[],
    frequencyRules: FrequencyRule[],
    weekContext: WeekContext,
  ): void {
    try {
      weekPlan.value = createGeneratedWeekPlan({
        library: {
          mealComponents,
          compositeDishes,
          activities,
        },
        planningRules,
        frequencyRules,
        weekContext,
        startDate: weekPlan.value.startDate,
      })
    } catch {
      weekPlan.value = createDemoWeekPlan()
    }

    persistWeekPlan(weekPlan.value)
  }

  function replaceComponent(
    dayId: string,
    mealType: MealType,
    componentIndex: number,
    componentType: ComponentType,
    componentId: string,
  ): void {
    const slot = mealSlot(dayId, mealType)
    const replacement = componentsByType(componentType).find((component) => component.id === componentId)

    if (!slot || !replacement || slot.mealDefinition.kind !== 'assembled') {
      return
    }

    const currentComponent = slot.mealDefinition.components[componentIndex]

    if (currentComponent?.componentType === componentType) {
      slot.mealDefinition.components.splice(componentIndex, 1, replacement)
      refreshSlotTotals(slot)
      persistWeekPlan(weekPlan.value)
    }
  }

  function replaceDish(dayId: string, mealType: MealType, dishId: string): void {
    const slot = mealSlot(dayId, mealType)
    const replacement = dishesForMealType(mealType).find((dish) => dish.id === dishId)

    if (!slot || !replacement) {
      return
    }

    slot.mealDefinition = { ...replacement }
    refreshSlotTotals(slot)
    persistWeekPlan(weekPlan.value)
  }

  function updateActivity(dayId: string, activityId: string): void {
    const dayPlan = weekPlan.value.days.find((day) => day.id === dayId)
    const replacement = activities.find((activity) => activity.id === activityId)

    if (dayPlan && replacement) {
      dayPlan.activity = replacement
      persistWeekPlan(weekPlan.value)
    }
  }

  return {
    weekPlan,
    resetDemoWeek,
    generateWeek,
    replaceComponent,
    replaceDish,
    updateActivity,
  }
})
