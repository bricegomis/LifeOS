export type MealType = 'breakfast' | 'lunch' | 'dinner'

export type ComponentType = 'protein' | 'starch' | 'vegetable' | 'optional'

export interface Nutrition {
  estimatedCalories: number
  estimatedProteinGrams: number
  estimatedCarbohydrateGrams: number
  estimatedFatGrams: number
}

export interface MealComponent extends Nutrition {
  id: string
  name: string
  icon: string
  componentType: ComponentType
  defaultPortionQuantity: number
  unit: string
  active: boolean
}

export interface AssembledMeal {
  id: string
  kind: 'assembled'
  components: MealComponent[]
  preparationTimeMinutes: number
}

export interface CompositeDish extends Nutrition {
  id: string
  kind: 'composite'
  name: string
  icon: string
  preparationTimeMinutes: number
  suitableForBreakfast: boolean
  suitableForLunch: boolean
  suitableForDinner: boolean
  active: boolean
}

export type MealDefinition = AssembledMeal | CompositeDish

export interface MealSlot {
  id: string
  mealType: MealType
  mealDefinition: MealDefinition
  estimatedCalories: number
  estimatedProteinGrams: number
  preparationTimeMinutes: number
}

export interface Activity {
  id: string
  name: string
}

export interface DayPlan {
  id: string
  dateLabel: string
  shortDateLabel: string
  breakfast: MealSlot
  lunch: MealSlot
  dinner: MealSlot
  activity: Activity
}

export interface WeekPlan {
  id: string
  startDate: string
  status: 'Draft' | 'Generated' | 'Validated' | 'Archived'
  days: DayPlan[]
}
