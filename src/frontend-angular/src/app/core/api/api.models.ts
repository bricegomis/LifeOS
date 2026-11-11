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
  hasImage: boolean
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
}
export interface FoodPricesDto { foodItemId: string; prices: ComparablePriceDto[] }
export interface ReceiptDto {
  id: string
  storeId: string | null
  /** Purchase date as yyyy-MM-dd, optional. */
  purchaseDate: string | null
  status: 'pending' | 'processed'
  /** Upload timestamp. */
  createdAt: string
}
export interface UpdateReceiptRequest {
  storeId: string | null
  purchaseDate: string | null
  processed: boolean
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

export interface MemberProfileDto {
  id: string
  displayName: string
  portionCoefficient: number
  createdAt: string
  updatedAt: string
}

export interface ReviewDto {
  id: string
  foodItemId: string | null
  recipeId: string | null
  memberProfileId: string
  profileName: string | null
  score: number | null
  comment: string | null
  createdByUserId: string
  updatedAt: string
}

export interface SaveReviewRequest {
  memberProfileId: string
  score: number | null
  comment: string | null
}
