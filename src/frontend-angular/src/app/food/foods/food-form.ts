import type { Conservation, Fodmap, FoodDto, FoodFamily, FoodRequest, FoodRole, NutritionBase, ProductDto, StoreDto } from '../food.models'
import { FAMILY_LABELS, MAX_TAG_LENGTH, MAX_TAGS } from '../shared/classification.ts'
import { formatDate, formatEuro } from '../shared/format.ts'

export const NUTRITION_FIELDS = [
  { key: 'caloriesPerUnit', label: 'Calories (kcal)' },
  { key: 'proteinsPerUnit', label: 'Protéines (g)' },
  { key: 'carbsPerUnit', label: 'Glucides (g)' },
  { key: 'fatsPerUnit', label: 'Lipides (g)' },
  { key: 'fibersPerUnit', label: 'Fibres (g)' },
] as const

export type NutritionKey = (typeof NUTRITION_FIELDS)[number]['key']

/** Text fields as typed: numbers may use a comma, an empty field means "unknown". */
export interface FoodFormValues {
  name: string
  referenceUnit: NutritionBase | ''
  nutrition: Record<NutritionKey, string>
  fodmap: Fodmap | ''
  nova: string
  digestiveNotes: string
  family: FoodFamily | ''
  roles: FoodRole[]
  conservation: Conservation | ''
  seasonMonths: number[]
  tags: string[]
}

export type FormResult<T> = { ok: true; value: T } | { ok: false; error: string }

export function emptyFoodForm(name = ''): FoodFormValues {
  return {
    name, referenceUnit: '', fodmap: '', nova: '', digestiveNotes: '',
    family: '', roles: [], conservation: '', seasonMonths: [], tags: [],
    nutrition: { caloriesPerUnit: '', proteinsPerUnit: '', carbsPerUnit: '', fatsPerUnit: '', fibersPerUnit: '' },
  }
}

export function foodFormFrom(food: FoodDto): FoodFormValues {
  const text = (value: number | null | undefined) => value === null || value === undefined ? '' : String(value).replace('.', ',')
  const n = food.nutrition
  return {
    name: food.name,
    referenceUnit: food.referenceUnit,
    nutrition: {
      caloriesPerUnit: text(n?.caloriesPerUnit), proteinsPerUnit: text(n?.proteinsPerUnit), carbsPerUnit: text(n?.carbsPerUnit),
      fatsPerUnit: text(n?.fatsPerUnit), fibersPerUnit: text(n?.fibersPerUnit),
    },
    fodmap: food.fodmap ?? '',
    nova: food.nova === null ? '' : String(food.nova),
    digestiveNotes: food.digestiveNotes ?? '',
    family: food.family ?? '',
    roles: [...(food.roles ?? [])],
    conservation: food.conservation ?? '',
    seasonMonths: [...(food.seasonMonths ?? [])],
    tags: [...(food.tags ?? [])],
  }
}

/** "" → null (unknown); "12,5" → 12.5; anything else → NaN. Zero is a real value. */
export function parseOptionalNumber(text: string): number | null {
  const clean = text.trim().replace(',', '.')
  if (!clean) return null
  return /^\d+(\.\d+)?$/.test(clean) ? Number(clean) : Number.NaN
}

/** Mirrors the FoodItem rules so the user gets the message before the round trip. */
export function buildFoodRequest(values: FoodFormValues): FormResult<FoodRequest> {
  const name = values.name.trim()
  if (!name || name.length > 200) return { ok: false, error: 'Nom requis (200 caractères maximum).' }
  if (!values.family || !(values.family in FAMILY_LABELS)) return { ok: false, error: 'Choisissez la famille de l’aliment.' }
  const tags = values.tags.map(tag => tag.trim()).filter(Boolean)
  if (tags.length > MAX_TAGS) return { ok: false, error: `${MAX_TAGS} étiquettes au plus.` }
  if (tags.some(tag => tag.length > MAX_TAG_LENGTH)) return { ok: false, error: `Une étiquette fait au plus ${MAX_TAG_LENGTH} caractères.` }
  const nutrition = {} as Record<NutritionKey, number | null>
  for (const field of NUTRITION_FIELDS) {
    const value = parseOptionalNumber(values.nutrition[field.key])
    if (Number.isNaN(value)) return { ok: false, error: `${field.label} : nombre positif ou vide.` }
    nutrition[field.key] = value
  }
  const nova = values.nova ? Number(values.nova) : null
  if (values.digestiveNotes.length > 2000) return { ok: false, error: 'Notes digestives limitées à 2000 caractères.' }
  return {
    ok: true,
    value: {
      name, referenceUnit: values.referenceUnit, nutrition, fodmap: values.fodmap || null, nova,
      digestiveNotes: values.digestiveNotes.trim() || null,
      family: values.family, roles: [...values.roles], conservation: values.conservation || null,
      seasonMonths: [...values.seasonMonths].sort((a, b) => a - b), tags,
    },
  }
}

export interface FoodPriceLine {
  product: string
  detail: string
  unitPrice: string
}

/** Food sheet summary: price range per comparison unit, then the latest price of each product and store. */
export function foodPriceSummary(products: readonly ProductDto[], stores: readonly StoreDto[]): { range: string; lines: FoodPriceLine[] } {
  const storeName = (id: string) => stores.find(store => store.id === id)?.name ?? 'Magasin supprimé'
  const latest = products.filter(product => !product.isArchived)
    .flatMap(product => product.latestPrices.map(price => ({ product, price })))
  const ranges = (['kg', 'L', 'pièce'] as const).flatMap(unit => {
    const values = latest.filter(entry => entry.price.unitLabel === unit).map(entry => entry.price.unitPrice)
    if (!values.length) return []
    const low = Math.min(...values)
    const high = Math.max(...values)
    return [low === high ? `${formatEuro(low)}/${unit}` : `De ${formatEuro(low)}/${unit} à ${formatEuro(high)}/${unit}`]
  })
  return {
    range: ranges.length ? ranges.join(' · ') : 'Aucun prix relevé',
    lines: latest
      .sort((a, b) => a.price.unitPrice - b.price.unitPrice)
      .map(({ product, price }) => ({
        product: product.name,
        detail: `${storeName(price.storeId)} · ${formatEuro(price.price)} · ${formatDate(price.observedOn)}`,
        unitPrice: `${formatEuro(price.unitPrice)}/${price.unitLabel}`,
      })),
  }
}
