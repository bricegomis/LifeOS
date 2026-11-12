import type { Fodmap, PackageUnit } from '../food.models'

const euro = new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR' })

export function formatEuro(value: number): string {
  return euro.format(value).replace(/ | /g, ' ')
}

/** French decimal number without trailing zeros, at most `digits` decimals. */
export function formatNumber(value: number, digits = 1): string {
  return new Intl.NumberFormat('fr-FR', { maximumFractionDigits: digits }).format(value).replace(/ | /g, ' ')
}

/** yyyy-MM-dd (or an ISO timestamp) → dd/MM/yyyy. */
export function formatDate(value: string): string {
  const [year, month, day] = value.slice(0, 10).split('-')
  return year && month && day ? `${day}/${month}/${year}` : value
}

/** Today in the browser's time zone, as yyyy-MM-dd. */
export function today(now = new Date()): string {
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}

export const FODMAP_LABELS: Record<Fodmap, string> = { low: 'Bas', moderate: 'Modéré', high: 'Élevé' }
export const NOVA_LABELS: Record<number, string> = {
  1: '1 — brut', 2: '2 — ingrédient culinaire', 3: '3 — transformé', 4: '4 — ultra-transformé',
}

export function fodmapLabel(value: Fodmap | null): string { return value ? FODMAP_LABELS[value] : '—' }

/** "400 g", "1,5 L", "6 pièces". */
export function formatQuantity(quantity: number, unit: string): string {
  const plural = unit === 'pièce' && quantity > 1 ? 'pièces' : unit
  return `${formatNumber(quantity, 3)} ${plural}`
}

export function packageLabel(quantity: number, unit: PackageUnit): string {
  return (unit === 'kg' || unit === 'L') && quantity === 1 ? `Au ${unit === 'kg' ? 'kilo' : 'litre'}` : formatQuantity(quantity, unit)
}

/** Case- and accent-insensitive "contains". */
export function matches(text: string, search: string): boolean {
  const normalize = (value: string) => value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase('fr')
  return normalize(text).includes(normalize(search.trim()))
}
