export interface StoreDto {
  id: string
  name: string
  address: string | null
  isOrganic: boolean
  isLocal: boolean
  createdAt: string
  updatedAt: string
}

export interface StoreRequest {
  name: string
  address: string | null
  isOrganic: boolean
  isLocal: boolean
}

export type GroceryItemUnit = 'kilogram' | 'liter' | 'unit'

export interface ArticlePriceEntryDto {
  id: string
  storeId: string
  price: number
  observedAt: string
  createdAt: string
}

export interface ArticleDto {
  id: string
  name: string
  description: string
  unit: GroceryItemUnit
  priceHistory: ArticlePriceEntryDto[]
  createdAt: string
  updatedAt: string
}

export interface ArticleRequest {
  name: string
  description: string
  unit: GroceryItemUnit
}

export interface AddPriceEntryRequest {
  storeId: string
  price: number
  observedAt: string
}

export interface NutritionPerUnitDto {
  caloriesPerUnit: number | null
  proteinsPerUnit: number | null
  carbsPerUnit: number | null
  fatsPerUnit: number | null
}

export interface FoodItemDto {
  id: string
  householdId: string
  name: string
  referenceUnit: string
  nutrition: NutritionPerUnitDto | null
  source: string
  offBarcode: string | null
  isCorrectionOf: string | null
  createdAt: string
  updatedAt: string
}

export interface FoodItemRequest {
  name: string
  referenceUnit: string
  nutrition: NutritionPerUnitDto | null
}

export interface RecipeIngredientDto {
  id: string
  foodItemId: string
  quantity: number
  unit: string
}

export interface RecipeDto {
  id: string
  name: string
  servings: number
  durationMinutes: number
  tags: string[]
  metadata: Record<string, unknown> | null
  ingredients: RecipeIngredientDto[]
  createdAt: string
  updatedAt: string
}

export interface RecipeRequest {
  name: string
  servings: number
  durationMinutes: number
  tags: string[]
  metadata: Record<string, unknown> | null
}

export interface AddRecipeIngredientRequest {
  foodItemId: string
  quantity: number
  unit: string
}

export interface ComposedMealPartDto {
  id: string
  recipeId: string
  quantityFactor: number
}

export interface ComposedMealDto {
  id: string
  name: string
  parts: ComposedMealPartDto[]
  createdAt: string
  updatedAt: string
}

export interface ComposedMealRequest {
  name: string
}

export interface AddComposedMealPartRequest {
  recipeId: string
  quantityFactor: number
}

export interface WeekDto {
  id: string
  startsOn: string
  status: string
  dayPlans: DayPlanDto[]
  createdAt: string
  updatedAt: string
}

export interface DayPlanDto {
  id: string
  date: string
  workContext: string
  bikeCommute: boolean
  plannedMeals: PlannedMealDto[]
}

export interface PlannedMealDto {
  id: string
  mealType: string
  status: string
  composedMealId: string | null
  recipeId: string | null
  parts: PlannedMealPartDto[]
  createdAt: string
  updatedAt: string
}

export interface PlannedMealPartDto {
  id: string
  memberProfileId: string
  portionMultiplier: number
}

export interface WeekScenarioDto {
  id: string
  weekId: string
  rankingObjective: string
  explanation: string | ScenarioExplanation
  applied: boolean
  createdAt: string
  updatedAt: string
}

export interface ScenarioExplanation {
  [key: string]: unknown
}

export interface GeneratedScenarioDto extends Omit<WeekScenarioDto, 'explanation'> {
  explanation: ScenarioExplanation
}

export interface ScenarioRequest {
  objectives: string[]
}

export interface LibraryMealComponentDto {
  id: string
  name: string
  icon: string
  componentType: string
  defaultPortionQuantity: number
  unit: string
  estimatedCalories: number
  estimatedProteinGrams: number
  estimatedCarbohydrateGrams: number
  estimatedFatGrams: number
  active: boolean
}

export interface LibraryCompositeDishDto {
  id: string
  name: string
  icon: string
  estimatedCalories: number
  estimatedProteinGrams: number
  preparationTimeMinutes: number
  suitableForBreakfast: boolean
  suitableForLunch: boolean
  suitableForDinner: boolean
  active: boolean
}

export interface ActivityDto {
  id: string
  name: string
  icon: string
  defaultDurationMinutes?: number
}

export interface StockItemDto {
  id: string
  householdId: string
  groceryItemId: string
  quantity: number
  unit: string
}

export interface StockItemRequest {
  groceryItemId: string
  quantity: number
  unit: string
}

export interface ShoppingListItemDto {
  id: string
  householdId: string
  weekId: string | null
  groceryItemId: string
  quantityNeeded: number
  quantityFromStock: number
  checked: boolean
}

export interface UserConfigurationDto {
  id: string
  householdId: string
  dailyBaseEnergyKcal: number
  targetNetDeficitKcal: number
  targetProteinG: number
  targetCarbsG: number
  targetFatsG: number
  createdAt: string
  updatedAt: string
}

export interface UserConfigurationRequest {
  dailyBaseEnergyKcal: number
  targetNetDeficitKcal: number
  targetProteinG: number
  targetCarbsG: number
  targetFatsG: number
}

export interface ActivitySessionDto {
  id: string
  dayPlanId: string
  type: string
  intensity: string
  durationMinutes: number
  estimatedEnergyKcal: number
  createdAt: string
  updatedAt: string
}

export interface ActivitySessionRequest {
  type: string
  intensity: string
  durationMinutes: number
}

export interface DailyNutritionTargetDto {
  dayPlanId: string
  dailyBaseEnergyKcal: number
  activityEnergyKcal: number
  targetNetDeficitKcal: number
  dailyFoodTargetKcal: number
  targetProteinG: number
  targetCarbsG: number
  targetFatsG: number
}

export interface PlanningRuleTarget {
  kind: string
  [key: string]: string
}

export interface PlanningRuleDto {
  id: string
  weekday: string
  mealType: string
  target: PlanningRuleTarget
}

export interface PlanningRuleRequest {
  weekday: string
  mealType: string
  target: PlanningRuleTarget
}

export interface FrequencyRuleDto {
  id: string
  target: Record<string, string>
  targetCountPerWeek: number
}

export interface FrequencyRuleRequest {
  target: Record<string, string>
  targetCountPerWeek: number
}

export interface WeekContextDto {
  id?: string
  householdId?: string
  referenceWeekStartDate?: string
  referenceWeekMode?: string
  weekModeOverrides?: Array<{ weekStartDate: string; mode: string }>
  days?: Record<string, { workLocation?: string; bikeCommute?: boolean }>
  [key: string]: unknown
}
