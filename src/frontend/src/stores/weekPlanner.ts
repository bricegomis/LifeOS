import { defineStore } from 'pinia'
import { ref } from 'vue'
import { activities, componentsByType, dishesForMealType } from '@/data/mockLibrary'
import { createDemoWeekPlan, totalsForMeal } from '@/data/demoWeek'
import type { ComponentType, MealSlot, MealType, WeekPlan } from '@/types'

export const useWeekPlannerStore = defineStore('weekPlanner', () => {
  const weekPlan = ref<WeekPlan>(createDemoWeekPlan())

  function mealSlot(dayId: string, mealType: MealType): MealSlot | null {
    const day = weekPlan.value.days.find((item) => item.id === dayId)

    return day ? day[mealType] : null
  }

  function refreshSlotTotals(slot: MealSlot): void {
    Object.assign(slot, totalsForMeal(slot.mealDefinition))
  }

  function resetDemoWeek(): void {
    weekPlan.value = createDemoWeekPlan()
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

  function updateActivity(dayId: string, activityId: string): void {
    const dayPlan = weekPlan.value.days.find((day) => day.id === dayId)
    const replacement = activities.find((activity) => activity.id === activityId)

    if (dayPlan && replacement) {
      dayPlan.activity = replacement
    }
  }

  return {
    weekPlan,
    resetDemoWeek,
    replaceComponent,
    replaceDish,
    updateActivity,
  }
})
