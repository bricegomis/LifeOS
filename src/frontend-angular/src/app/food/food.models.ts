/** API shapes of the Food module (ADR 0010). */

export type Fodmap = 'low' | 'moderate' | 'high'
export type NutritionBase = '100 g' | '100 ml' | '1 pièce'
export type IngredientUnit = 'g' | 'ml' | 'pièce'
export type PackageUnit = 'g' | 'kg' | 'ml' | 'L' | 'pièce'
/** Food classification (ADR 0012); labels in `shared/classification.ts`. */
export type FoodFamily = 'vegetable' | 'fruit' | 'grain' | 'legume' | 'meat' | 'fish' | 'egg' | 'dairy' | 'fat'
  | 'nut_seed' | 'herb_spice' | 'condiment' | 'sweet' | 'drink' | 'other'
export type FoodRole = 'protein' | 'starch' | 'vegetable' | 'fat' | 'seasoning' | 'sweet'
export type Conservation = 'fresh' | 'frozen' | 'canned' | 'dry'

export interface NutritionDto {
  caloriesPerUnit: number | null
  proteinsPerUnit: number | null
  carbsPerUnit: number | null
  fatsPerUnit: number | null
  fibersPerUnit: number | null
}

export interface FoodRequest {
  name: string
  /** Empty while unknown. */
  referenceUnit: NutritionBase | ''
  nutrition: NutritionDto | null
  fodmap: Fodmap | null
  nova: number | null
  digestiveNotes: string | null
  /** Required. */
  family: FoodFamily
  roles: FoodRole[]
  conservation: Conservation | null
  /** 1..12, sorted; empty means "not seasonal". */
  seasonMonths: number[]
  tags: string[]
}

export interface FoodDto extends FoodRequest {
  id: string
  isArchived: boolean
  missingFields: string[]
  hasImage: boolean
  createdAt: string
  updatedAt: string
}

export interface StoreRequest {
  name: string
  address: string
  isOrganic: boolean
  isLocal: boolean
}

export interface StoreDto extends StoreRequest {
  id: string
  createdAt: string
  updatedAt: string
}

export interface ProductRequest {
  foodItemId: string
  name: string
  packageQuantity: number
  packageUnit: PackageUnit
  brand: string | null
  barcode: string | null
}

export interface ProductPriceDto {
  id: string
  storeId: string
  /** Price paid for one package, in euros. */
  price: number
  /** yyyy-MM-dd */
  observedOn: string
  /** Package contents frozen when the price was observed. */
  packageQuantity: number
  packageUnit: PackageUnit
  /** Price per kilogram, litre or piece. */
  unitPrice: number
  unitLabel: 'kg' | 'L' | 'pièce'
}

export interface ProductDto extends ProductRequest {
  id: string
  isArchived: boolean
  /** The most recent price of each store. */
  latestPrices: ProductPriceDto[]
  /** Whole history, most recent first. */
  prices: ProductPriceDto[]
  createdAt: string
  updatedAt: string
}

export interface PriceRequest {
  storeId: string
  price: number
  observedOn: string
}

export interface ReceiptDto {
  id: string
  storeId: string | null
  /** yyyy-MM-dd, optional. */
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
