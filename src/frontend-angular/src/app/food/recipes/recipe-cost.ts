import type { FoodDto, StoreDto } from '../food.models'
import { formatDate, formatEuro, formatQuantity } from '../shared/format.ts'
import type { PriceCandidateDto, RecipeCostDto } from './recipe.models'

export interface CostLineView {
  name: string
  amount: string
  details: string[]
  unknown: boolean
}

export interface RecipeCostView {
  min: string
  max: string
  minPerServing: string
  maxPerServing: string
  /** Shown while some ingredients have no comparable price. */
  notice: string
  lines: CostLineView[]
}

/** Recipe card line: the range per portion, or why there is none. */
export function costSummary(cost: RecipeCostDto | undefined): string {
  if (!cost) return ''
  if (!cost.lines.length) return ''
  if (!cost.isComplete) return 'Coût incomplet'
  return cost.minPerServing === cost.maxPerServing
    ? `${formatEuro(cost.minPerServing)} par portion`
    : `${formatEuro(cost.minPerServing)} à ${formatEuro(cost.maxPerServing)} par portion`
}

/** Recipe sheet: totals, a partial notice and, per ingredient, the product, store, price and date of the min and max. */
export function costView(cost: RecipeCostDto, foods: readonly FoodDto[], stores: readonly StoreDto[]): RecipeCostView {
  const storeName = (id: string) => stores.find(store => store.id === id)?.name ?? 'Magasin supprimé'
  const describe = (price: PriceCandidateDto) =>
    `${price.productName} · ${storeName(price.storeId)} · ${formatEuro(price.unitPrice)}/${price.unitLabel} · ${formatDate(price.observedOn)}`
  const missing = cost.missingIngredients
  const partial = !cost.isComplete
  return {
    min: formatEuro(cost.minTotal),
    max: formatEuro(cost.maxTotal),
    minPerServing: partial ? 'total partiel' : `${formatEuro(cost.minPerServing)} par portion`,
    maxPerServing: partial ? 'total partiel' : `${formatEuro(cost.maxPerServing)} par portion`,
    notice: missing ? `Coût incomplet : ${missing} ingrédient${missing > 1 ? 's' : ''} sans prix comparable.` : '',
    lines: cost.lines.map(line => {
      const name = `${foods.find(food => food.id === line.foodItemId)?.name ?? 'Aliment indisponible'} — ${formatQuantity(line.quantity, line.unit)}`
      if (line.minAmount === null || line.maxAmount === null || !line.cheapest || !line.dearest) {
        return { name, amount: 'Inconnu', details: ['Aucun prix relevé sur une base comparable (kg, L ou pièce selon l’unité)'], unknown: true }
      }
      if (line.comparablePrices === 1) {
        return { name, amount: formatEuro(line.minAmount), details: [`Un seul prix : ${describe(line.cheapest)}`], unknown: false }
      }
      return {
        name,
        amount: `${formatEuro(line.minAmount)} à ${formatEuro(line.maxAmount)}`,
        details: [`Mini : ${describe(line.cheapest)}`, `Maxi : ${describe(line.dearest)}`],
        unknown: false,
      }
    }),
  }
}
