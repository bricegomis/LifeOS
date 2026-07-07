import { defineStore } from 'pinia'
import { ref } from 'vue'
import { createGeneratedWeekPlan } from '@/data/weekGenerator'
import {
  activities,
  componentsByType,
  compositeDishes,
  dishesForMealType,
  mealComponents,
} from '@/data/localLibrary'
import { createDemoWeekPlan, totalsForMeal } from '@/data/demoWeek'
import type {
  ComponentType,
  FrequencyRule,
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

    return storedWeekPlan.data
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
    saveWeekPlan(weekPlan.value)
  }

  function generateWeek(
    planningRules: PlanningRule[],
    frequencyRules: FrequencyRule[],
    weekContext: WeekContext,
  ): void {
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
    saveWeekPlan(weekPlan.value)
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
      saveWeekPlan(weekPlan.value)
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
    saveWeekPlan(weekPlan.value)
  }

  function updateActivity(dayId: string, activityId: string): void {
    const dayPlan = weekPlan.value.days.find((day) => day.id === dayId)
    const replacement = activities.find((activity) => activity.id === activityId)

    if (dayPlan && replacement) {
      dayPlan.activity = replacement
      saveWeekPlan(weekPlan.value)
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
