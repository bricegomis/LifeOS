export type MealType = 'breakfast' | 'lunch' | 'dinner'

export type ComponentType = 'protein' | 'starch' | 'vegetable' | 'optional'

export type Weekday =
  | 'monday'
  | 'tuesday'
  | 'wednesday'
  | 'thursday'
  | 'friday'
  | 'saturday'
  | 'sunday'

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
  icon: string
  defaultDurationMinutes?: number
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

export type PlanningRuleTarget =
  | {
      kind: 'component'
      componentId: string
      componentType: ComponentType
    }
  | {
      kind: 'dish'
      dishId: string
    }

export interface PlanningRule {
  id: string
  weekday: Weekday
  mealType: MealType
  target: PlanningRuleTarget
}

export type FrequencyRuleTarget =
  | {
      kind: 'component'
      componentId: string
    }
  | {
      kind: 'dish'
      dishId: string
    }
  | {
      kind: 'category'
      categoryId: string
      label: string
    }

export interface FrequencyRule {
  id: string
  target: FrequencyRuleTarget
  targetCountPerWeek: number
}
