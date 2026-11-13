import type { Conservation, FoodDto, FoodFamily, FoodRole } from '../food.models'
import { matches } from './format.ts'

/** Fixed lists of ADR 0012, in display order. Codes mirror `FoodClassification` in the API. */
export const FAMILY_LABELS: Record<FoodFamily, string> = {
  vegetable: 'Légume',
  fruit: 'Fruit',
  grain: 'Céréale et féculent',
  legume: 'Légumineuse',
  meat: 'Viande',
  fish: 'Poisson et fruits de mer',
  egg: 'Œuf',
  dairy: 'Produit laitier',
  fat: 'Matière grasse',
  nut_seed: 'Fruit à coque et graine',
  herb_spice: 'Herbe et épice',
  condiment: 'Condiment et sauce',
  sweet: 'Produit sucré',
  drink: 'Boisson',
  other: 'Autre',
}

export const ROLE_LABELS: Record<FoodRole, string> = {
  protein: 'Protéine',
  starch: 'Féculent',
  vegetable: 'Légume',
  fat: 'Matière grasse',
  seasoning: 'Assaisonnement',
  sweet: 'Sucré',
}

export const CONSERVATION_LABELS: Record<Conservation, string> = {
  fresh: 'Frais',
  frozen: 'Surgelé',
  canned: 'Conserve',
  dry: 'Sec',
}

export const MONTH_NAMES = ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre',
  'octobre', 'novembre', 'décembre'] as const
export const MONTH_SHORT = ['Janv', 'Févr', 'Mars', 'Avr', 'Mai', 'Juin', 'Juil', 'Août', 'Sept', 'Oct', 'Nov', 'Déc'] as const

export const MAX_TAGS = 20
export const MAX_TAG_LENGTH = 50

export const familyOptions = Object.entries(FAMILY_LABELS) as [FoodFamily, string][]
export const roleOptions = Object.entries(ROLE_LABELS) as [FoodRole, string][]
export const conservationOptions = Object.entries(CONSERVATION_LABELS) as [Conservation, string][]

export function familyLabel(family: FoodFamily | '' | null | undefined): string {
  return family ? FAMILY_LABELS[family] ?? family : '—'
}

/** Current month, 1..12, in the browser's time zone. */
export function currentMonth(now = new Date()): number { return now.getMonth() + 1 }

export type SeasonKind = 'in' | 'all' | 'out'

export interface SeasonStatus {
  kind: SeasonKind
  label: string
}

/** No month means "not seasonal": available all year. */
export function seasonStatus(months: readonly number[], month: number): SeasonStatus {
  if (!months.length) return { kind: 'all', label: 'Toute l’année' }
  return months.includes(month) ? { kind: 'in', label: 'De saison' } : { kind: 'out', label: 'Hors saison' }
}

const seasonRank: Record<SeasonKind, number> = { in: 0, all: 1, out: 2 }

/** In season first, then all year, then out of season; by name inside each group. Nothing is hidden. */
export function sortBySeason<T extends Pick<FoodDto, 'name' | 'seasonMonths'>>(foods: readonly T[], month: number): T[] {
  return [...foods].sort((a, b) => seasonRank[seasonStatus(a.seasonMonths, month).kind]
    - seasonRank[seasonStatus(b.seasonMonths, month).kind] || a.name.localeCompare(b.name, 'fr'))
}

/** "En décembre, 1 ingrédient est hors saison : Poivron." or null when every food is fine. */
export function offSeasonNotice(foods: readonly Pick<FoodDto, 'name' | 'seasonMonths'>[], month: number): string | null {
  const names = [...new Set(foods.filter(food => seasonStatus(food.seasonMonths, month).kind === 'out').map(food => food.name))]
  if (!names.length) return null
  const verb = names.length > 1 ? `${names.length} ingrédients sont hors saison` : '1 ingrédient est hors saison'
  return `En ${MONTH_NAMES[month - 1]}, ${verb} : ${names.join(', ')}.`
}

/** "janvier à avril, septembre à décembre" for read-only sheets. */
export function seasonRangeLabel(months: readonly number[]): string {
  if (!months.length) return 'Pas saisonnier'
  const sorted = [...new Set(months)].sort((a, b) => a - b)
  const runs: [number, number][] = []
  for (const month of sorted) {
    const last = runs.at(-1)
    if (last && month === last[1] + 1) last[1] = month
    else runs.push([month, month])
  }
  // December to January is one season: join the last run to the first one.
  const first = runs[0]
  const last = runs.at(-1)
  if (runs.length > 1 && first && last && first[0] === 1 && last[1] === 12) {
    last[1] = first[1]
    runs.shift()
  }
  return runs.map(([from, to]) => from === to ? MONTH_NAMES[from - 1] : `${MONTH_NAMES[from - 1]} à ${MONTH_NAMES[to - 1]}`).join(', ')
}

/** Every tag of the household, sorted, one spelling per tag ignoring case. */
export function householdTags(foods: readonly Pick<FoodDto, 'tags'>[]): string[] {
  const seen = new Map<string, string>()
  for (const tag of foods.flatMap(food => food.tags)) {
    const key = tag.toLocaleLowerCase('fr')
    if (!seen.has(key)) seen.set(key, tag)
  }
  return [...seen.values()].sort((a, b) => a.localeCompare(b, 'fr'))
}

/** Adds a typed tag, reusing the household's spelling ("Apéro" becomes "apéro" when it exists). */
export function addTag(tags: readonly string[], typed: string, known: readonly string[]): string[] {
  const clean = typed.trim()
  if (!clean) return [...tags]
  const key = clean.toLocaleLowerCase('fr')
  if (tags.some(tag => tag.toLocaleLowerCase('fr') === key)) return [...tags]
  return [...tags, known.find(tag => tag.toLocaleLowerCase('fr') === key) ?? clean]
}

export interface FoodListFilter {
  search: string
  todo: boolean
  family: FoodFamily | ''
  role: FoodRole | ''
  conservation: Conservation | ''
  season: SeasonKind | ''
  tag: string
  month: number
}

export function filterFoods(foods: readonly FoodDto[], filter: FoodListFilter): FoodDto[] {
  const tag = filter.tag.toLocaleLowerCase('fr')
  return foods.filter(food => !food.isArchived && matches(food.name, filter.search)
    && (!filter.todo || food.missingFields.length > 0)
    && (!filter.family || food.family === filter.family)
    && (!filter.role || food.roles.includes(filter.role))
    && (!filter.conservation || food.conservation === filter.conservation)
    && (!filter.season || seasonStatus(food.seasonMonths, filter.month).kind === filter.season)
    && (!tag || food.tags.some(t => t.toLocaleLowerCase('fr') === tag)))
}
