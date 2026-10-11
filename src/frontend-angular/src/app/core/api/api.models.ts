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

export interface NutritionPerUnitDto {
  caloriesPerUnit: number | null
  proteinsPerUnit: number | null
  carbsPerUnit: number | null
  fatsPerUnit: number | null
}
export interface FoodItemRequest {
  name: string
  referenceUnit: string
  nutrition: NutritionPerUnitDto | null
  fodmap: 'low' | 'moderate' | 'high' | null
  nova: number | null
  digestiveNotes: string | null
}
export interface FoodItemDto extends FoodItemRequest {
  id: string
  householdId: string
  isArchived: boolean
  missingFields: string[]
  source: string
  createdAt: string
  updatedAt: string
}
export interface ProductRequest {
  foodItemId: string
  name: string
  description: string
  purchaseUnit: string
  brand: string | null
  barcode: string | null
  packageQuantity: number | null
  packageUnit: string | null
  packageConfirmed: boolean
  piecesComparable: boolean
}
export interface ProductDto extends Omit<ProductRequest, 'foodItemId'> {
  id: string
  foodItemId: string | null
  householdId: string
  isArchived: boolean
  priceHistory: PriceObservationDto[]
  purchaseUnitConfirmed: boolean
  legacyPurchaseName: string | null
  migrationOrigin: string | null
}
export interface PriceObservationDto {
  id: string
  storeId: string
  price: number
  unit: string
  observedAt: string
  provenance: string
  receiptLineId: string | null
  packageQuantity: number | null
  packageUnit: string | null
  packageConfirmed: boolean
  piecesComparable: boolean
}
export interface ComparablePriceDto {
  productId: string
  productName: string
  storeId: string
  price: number
  unit: string
  observedAt: string
  provenance: string
}
export interface FoodPricesDto { foodItemId: string; prices: ComparablePriceDto[] }
export interface ReceiptSummaryDto {
  id: string
  storeName: string
  storeId: string | null
  date: string | null
  totalLines: number
  completedLines: number
}
export interface ReceiptDto extends Omit<ReceiptSummaryDto, 'totalLines'> {
  rawDocument: string
  declaredLines: number | null
  detectedLines: number | null
  diagnostic: string | null
  lines: ReceiptLineDto[]
}
export interface ReceiptLineDto {
  id: string
  position: number
  sourceText: string
  excludedMention: boolean
  label: string
  total: number | null
  quantity: number | null
  unit: string | null
  statedUnitPrice: number | null
  priceMismatchConfirmed: boolean
  status: 'pending' | 'validated' | 'ignored'
  productId: string | null
  diagnostics: string[]
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
  key: 'nutrition' | 'cost' | 'diversity' | 'reuse'
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
