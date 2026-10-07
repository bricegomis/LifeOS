export interface PersonalNutrition {
  calories: number | null
  protein: number | null
  carbs: number | null
  fat: number | null
  isComplete: boolean
  warnings: string[]
}

export interface MealLine {
  id: string
  foodItemId: string | null
  name: string
  quantity: number
  unit: string
  personalQuantity: number
  preparationQuantity: number
  referenceUnit: string
  calories: number | null
  protein: number | null
  carbs: number | null
  fat: number | null
}

export interface ManualMeal {
  id: string
  dayPlanId: string
  name: string
  startMinute: number | null
  endMinute: number | null
  recipeId: string | null
  composedMealId: string | null
  personalPortion: number
  childrenCount: number
  hasSnapshot: boolean
  lines: MealLine[]
  nutrition: PersonalNutrition
}

export interface ManualSport {
  id: string
  dayPlanId: string
  sportTemplateId: string | null
  name: string
  sport: string
  intensity: string
  durationMinutes: number
  distanceKm: number | null
  calories: number
  startMinute: number | null
  endMinute: number | null
}

export interface ManualDay {
  id: string
  date: string
  meals: ManualMeal[]
  sports: ManualSport[]
  nutrition: PersonalNutrition
}

export interface ManualWeekSummary {
  id: string
  startsOn: string
  timeZoneId: string
  isManual: boolean
}

export interface ManualWeek extends ManualWeekSummary {
  days: ManualDay[]
}

export interface MealLinePayload {
  foodItemId: string | null
  quantity: number
  unit: string
  snapshotLineId?: string
}

export interface MealPayload {
  dayPlanId: string
  startMinute: number | null
  endMinute: number | null
  personalPortion: number
  childrenCount: number
  recipeId: string | null
  lines: MealLinePayload[] | null
  replaceContent: boolean
}

export interface SportPayload {
  dayPlanId: string
  startMinute: number | null
  endMinute: number | null
  sportTemplateId: string | null
  name: string
  sport: string
  intensity: string
  durationMinutes: number
  distanceKm: number | null
  calories: number
  replaceContent: boolean
}
