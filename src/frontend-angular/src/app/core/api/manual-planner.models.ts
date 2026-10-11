export interface PersonalNutrition {
  calories: number | null
  protein: number | null
  carbs: number | null
  fat: number | null
  isComplete: boolean
  warnings: string[]
}

export interface PlannedLineIngredient {
  id: string
  foodItemId: string
  name: string
  quantity: number
  unit: string
  requiredQuantity: number
  referenceUnit: string
  calories: number | null
  protein: number | null
  carbs: number | null
  fat: number | null
}

export interface PlannedLinePortion {
  memberProfileId: string
  memberName: string
  servings: number
}

export interface PlannedRecipeLine {
  id: string
  recipeId: string | null
  name: string | null
  instructions: string | null
  yieldServings: number
  plannedServings: number
  isAdHoc: boolean
  portions: PlannedLinePortion[]
  ingredients: PlannedLineIngredient[]
  nutrition: PersonalNutrition
}

export interface ManualMeal {
  id: string
  dayPlanId: string
  name: string
  startMinute: number | null
  endMinute: number | null
  recipeLines: PlannedRecipeLine[]
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

export interface PlannedLineIngredientRequest {
  foodItemId: string
  quantity: number
  unit: string
}

export interface PlannedLinePortionRequest {
  memberProfileId: string
  servings: number
}

export interface PlannedRecipeLineRequest {
  recipeId: string | null
  name: string | null
  instructions: string | null
  yieldServings: number | null
  plannedServings: number | null
  ingredients: PlannedLineIngredientRequest[] | null
  portions: PlannedLinePortionRequest[] | null
}

export interface MealPayload {
  dayPlanId: string
  startMinute: number | null
  endMinute: number | null
  recipeLines: PlannedRecipeLineRequest[]
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
