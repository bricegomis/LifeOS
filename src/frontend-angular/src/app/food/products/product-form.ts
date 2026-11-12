import type { FoodDto, PackageUnit, PriceRequest, ProductDto, ProductRequest, StoreDto } from '../food.models'
import { formatDate, formatEuro, matches, packageLabel } from '../shared/format.ts'

export const PACKAGE_UNITS: PackageUnit[] = ['g', 'kg', 'ml', 'L', 'pièce']

export type FormResult<T> = { ok: true; value: T } | { ok: false; error: string }

export interface ProductFormValues {
  foodItemId: string
  name: string
  packageQuantity: string
  packageUnit: PackageUnit
  brand: string
  barcode: string
}

export interface PriceFormValues {
  productId: string
  storeId: string
  price: string
  observedOn: string
}

const decimal = (text: string) => Number(text.trim().replace(',', '.'))

/** A product only exists complete: food, name and package contents are required (ADR 0010). */
export function buildProductRequest(values: ProductFormValues): FormResult<ProductRequest> {
  const name = values.name.trim()
  const quantity = decimal(values.packageQuantity)
  if (!values.foodItemId) return { ok: false, error: 'Choisissez l’aliment de ce produit.' }
  if (!name || name.length > 200) return { ok: false, error: 'Nom du produit requis (200 caractères maximum).' }
  if (!values.packageQuantity.trim() || !Number.isFinite(quantity) || quantity <= 0 || !PACKAGE_UNITS.includes(values.packageUnit)) {
    return { ok: false, error: 'Indiquez la contenance : une quantité supérieure à zéro en g, kg, ml, L ou pièce.' }
  }
  return {
    ok: true,
    value: {
      foodItemId: values.foodItemId, name, packageQuantity: quantity, packageUnit: values.packageUnit,
      brand: values.brand.trim() || null, barcode: values.barcode.trim() || null,
    },
  }
}

/** The price paid for one package, in euros and cents, on a past or present date. */
export function buildPriceRequest(values: PriceFormValues, todayIso: string): FormResult<PriceRequest> {
  const price = decimal(values.price)
  if (!values.productId) return { ok: false, error: 'Choisissez un produit.' }
  if (!values.storeId) return { ok: false, error: 'Choisissez le magasin.' }
  if (!Number.isFinite(price) || price <= 0 || Math.abs(price * 100 - Math.round(price * 100)) > 1e-6) {
    return { ok: false, error: 'Indiquez le prix payé, en euros et centimes.' }
  }
  if (!/^\d{4}-\d{2}-\d{2}$/.test(values.observedOn) || values.observedOn > todayIso) {
    return { ok: false, error: 'La date du relevé ne peut pas être dans le futur.' }
  }
  return { ok: true, value: { storeId: values.storeId, price: Math.round(price * 100) / 100, observedOn: values.observedOn } }
}

export interface PriceCell { price: string; detail: string }
export interface ProductRow {
  product: ProductDto
  foodName: string
  pack: string
  /** One cell per store column, null when no price was observed there. */
  cells: (PriceCell | null)[]
}

/**
 * Products and prices table: active products matching the search on product or food name and,
 * when a store is chosen, having a price there. One column per store with its latest price.
 */
export function productRows(products: readonly ProductDto[], foods: readonly FoodDto[], stores: readonly StoreDto[],
  search: string, storeId: string): ProductRow[] {
  const foodName = (id: string) => foods.find(food => food.id === id)?.name ?? 'Aliment indisponible'
  return products
    .filter(product => !product.isArchived)
    .filter(product => matches(`${product.name} ${foodName(product.foodItemId)} ${product.brand ?? ''}`, search))
    .filter(product => !storeId || product.latestPrices.some(price => price.storeId === storeId))
    .map(product => ({
      product,
      foodName: foodName(product.foodItemId),
      pack: packageLabel(product.packageQuantity, product.packageUnit),
      cells: stores.map(store => {
        const price = product.latestPrices.find(entry => entry.storeId === store.id)
        return price ? {
          price: formatEuro(price.price),
          detail: `${formatEuro(price.unitPrice)}/${price.unitLabel} · ${formatDate(price.observedOn)}`,
        } : null
      }),
    }))
    .sort((a, b) => a.foodName.localeCompare(b.foodName, 'fr') || a.product.name.localeCompare(b.product.name, 'fr'))
}
