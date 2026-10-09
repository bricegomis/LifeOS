import type { BuildInfo } from '../build-info'

export interface StoreDto {
  id: string
  name: string
  address: string | null
  isOrganic: boolean
  isLocal: boolean
  createdAt: string
  updatedAt: string
}

export interface BuildInfoDto extends BuildInfo {
  component: 'api'
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
  articleId: string | null
  isArchived: boolean
  name: string
  referenceUnit: string
  description: string
  unit: GroceryItemUnit
  purchaseUnitConfirmed: boolean
  legacyPurchaseName: string | null
  migrationOrigin: 'linked' | 'food-only' | 'article-only' | null
  priceHistory: ArticlePriceEntryDto[]
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
  description: string
  unit: GroceryItemUnit | null
  nutrition: NutritionPerUnitDto | null
}

export interface ProductUsageDto {
  recipes: { id: string; name: string; isArchived: boolean }[]
  stock: { quantity: number; unit: string }[]
  mealOccurrences: number
  historicalShoppingLines: number
}

export interface RecipeIngredientDto {
  id: string
  foodItemId: string
  quantity: number
  unit: string
}

export interface RecipeDto {
  id: string
  isArchived: boolean
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
  weekMode: 'kids' | 'solo'
  isManual: boolean
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

/**
 * One dimension of the compromise. `score` is null when the household data does not allow
 * computing it: the dimension is then excluded from the composite score and the reason is
 * listed in `limitations`.
 */
export interface BalancedPlanDimension {
  key: 'nutrition' | 'cost' | 'diversity' | 'waste'
  score: number | null
  weight: number
  dataCoverage: number
  summary: string
  metrics: Record<string, number>
}

export interface BalancedPlanExplanation {
  method: string
  overallScore: number | null
  nutritionConstraintMet: boolean
  dimensions: BalancedPlanDimension[]
  tradeoffs: string[]
  limitations: string[]
  textExplanation: string
}

/** A persisted plan: its explanation is returned as raw JSON. */
export interface BalancedPlanDto {
  id: string
  weekId: string
  method: string
  explanation: string
  applied: boolean
  createdAt: string
  updatedAt: string
}

/** A freshly computed plan: its explanation is returned structured. */
export interface ComputedBalancedPlanDto extends Omit<BalancedPlanDto, 'explanation'> {
  explanation: BalancedPlanExplanation
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

export interface SportTemplateRequest {
  name: string
  sport: string
  durationMinutes: number
  distanceKm: number | null
  intensity: 'low' | 'moderate' | 'high'
  calories: number
}

export interface SportTemplateDto extends SportTemplateRequest {
  id: string
  householdId: string
  isArchived: boolean
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
  weekMode?: 'kids' | 'solo' | null
}

export interface PlanningRuleRequest {
  weekday: string
  mealType: string
  target: PlanningRuleTarget
  weekMode?: 'kids' | 'solo' | 'all'
}

export interface FrequencyRuleDto {
  id: string
  target: Record<string, string>
  targetCountPerWeek: number
  weekMode?: 'kids' | 'solo' | null
}

export interface FrequencyRuleRequest {
  target: Record<string, string>
  targetCountPerWeek: number
  weekMode?: 'kids' | 'solo' | 'all'
}

export interface WeekContextDto {
  id?: string
  householdId?: string
  referenceWeekStartDate?: string
  referenceWeekMode?: string
  weekModeOverrides?: Array<{ weekStartDate: string; mode: string }>
  days?: Record<string, { workLocation?: string; bikeCommute?: boolean }>
  templates?: Record<
    'kids' | 'solo',
    Record<string, { workLocation?: string; bikeCommute?: boolean }>
  >
  [key: string]: unknown
}
