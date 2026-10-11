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
  instructions: string | null
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
  instructions: string | null
}

export interface AddRecipeIngredientRequest {
  foodItemId: string
  quantity: number
  unit: string
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

export interface MemberProfileDto {
  id: string
  displayName: string
  portionCoefficient: number
  createdAt: string
  updatedAt: string
}

export interface HouseholdGroupDto {
  id: string
  name: string
}

export interface ReviewDto {
  id: string
  foodItemId: string | null
  recipeId: string | null
  plannedRecipeLineId: string | null
  memberProfileId: string | null
  householdGroupId: string | null
  targetName: string | null
  score: number | null
  comment: string | null
  createdByUserId: string
  updatedAt: string
}

export interface SaveReviewRequest {
  memberProfileId: string | null
  householdGroupId: string | null
  score: number | null
  comment: string | null
}
