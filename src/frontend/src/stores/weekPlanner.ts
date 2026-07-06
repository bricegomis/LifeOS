import { defineStore } from 'pinia'
import { ref } from 'vue'
import { activities, componentsByType, dishesForMealType } from '@/data/mockLibrary'
import { createDemoWeekPlan, generateDemoWeekPlan, totalsForMeal } from '@/data/demoWeek'
import type { ComponentType, DayPlan, MealSlot, MealType, WeekPlan } from '@/types'

export const useWeekPlannerStore = defineStore('weekPlanner', () => {
  const weekPlan = ref<WeekPlan>(createDemoWeekPlan())
  const generationCount = ref(0)

  function mealSlot(dayId: string, mealType: MealType): MealSlot | null {
    const day = weekPlan.value.days.find((item) => item.id === dayId)

    return day ? day[mealType] : null
  }

  function refreshSlotTotals(slot: MealSlot): void {
    Object.assign(slot, totalsForMeal(slot.mealDefinition))
  }

  function generateWeek(): void {
    generationCount.value += 1
    weekPlan.value = generateDemoWeekPlan(generationCount.value)
  }

  function replaceComponent(
    dayId: string,
    mealType: MealType,
    componentType: ComponentType,
    componentId: string,
  ): void {
    const slot = mealSlot(dayId, mealType)
    const replacement = componentsByType(componentType).find((component) => component.id === componentId)

    if (!slot || !replacement || slot.mealDefinition.kind !== 'assembled') {
      return
    }

    const componentIndex = slot.mealDefinition.components.findIndex(
      (component) => component.componentType === componentType,
    )

    if (componentIndex >= 0) {
      slot.mealDefinition.components.splice(componentIndex, 1, replacement)
      refreshSlotTotals(slot)
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
  }

  function updateActivity(dayPlan: DayPlan, activityId: string): void {
    const replacement = activities.find((activity) => activity.id === activityId)

    if (replacement) {
      dayPlan.activity = replacement
    }
  }

  return {
    weekPlan,
    generateWeek,
    replaceComponent,
    replaceDish,
    updateActivity,
  }
})
