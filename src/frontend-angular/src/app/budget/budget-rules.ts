import type { AmountPeriod, BudgetLineDto, BudgetLineRequest, BudgetOperationDto, LineKind, RuleDirection } from './budget.models'

const MONTHS = ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre']
const SHORT = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']

export const KINDS: { value: LineKind, label: string, help: string, icon: string }[] = [
  { value: 'fixed', label: 'Fixe', help: 'Montant attendu : loyer, crédit, abonnement.', icon: 'event_repeat' },
  { value: 'envelope', label: 'Enveloppe', help: 'Plafond à tenir, remis à zéro chaque mois.', icon: 'savings' },
  { value: 'extra', label: 'Extra', help: 'Aucun montant prévu : sert à suivre le non-planifié.', icon: 'bolt' },
  { value: 'off', label: 'Hors budget', help: 'Ni dépense ni revenu : virement entre tes comptes, avance remboursée.', icon: 'swap_horiz' },
  { value: 'income', label: 'Revenu', help: 'Montant prévu, modifiable mois par mois (congés, RTT).', icon: 'payments' },
]

export const PERIODS: { value: AmountPeriod, label: string }[] = [
  { value: 'month', label: 'Par mois' }, { value: 'year', label: 'Par an' }, { value: 'months', label: 'Tous les N mois' },
]

export const DIRECTIONS: { value: RuleDirection, label: string }[] = [
  { value: 'out', label: 'Dépenses' }, { value: 'in', label: 'Revenus' }, { value: 'any', label: 'Les deux' },
]

export const SPREADS = [1, 2, 3, 6, 12]

export function kindOf(kind: LineKind) { return KINDS.find(k => k.value === kind) ?? KINDS[0]! }

/** 1 234,50 € (no decimals when round); with a sign for operations. */
export function euro(value: number, signed = false): string {
  const abs = Math.abs(value)
  const text = abs.toLocaleString('fr-FR', { minimumFractionDigits: Number.isInteger(Math.round(abs * 100) / 100) ? 0 : 2, maximumFractionDigits: 2 }) + ' €'
  if (signed) return (value < 0 ? '−' : '+') + text
  return (value < 0 ? '−' : '') + text
}

export function monthOf(date: string): string { return date.slice(0, 7) }

export function addMonths(month: string, count: number): string {
  const [y, m] = month.split('-').map(Number) as [number, number]
  const index = y * 12 + (m - 1) + count
  return `${Math.floor(index / 12)}-${String(index % 12 + 1).padStart(2, '0')}`
}

/** « Novembre 2026 ». */
export function monthLabel(month: string): string {
  const [y, m] = month.split('-').map(Number) as [number, number]
  const name = MONTHS[m - 1] ?? ''
  return name.charAt(0).toUpperCase() + name.slice(1) + ' ' + y
}

export function shortMonth(month: string): string { return SHORT[Number(month.slice(5, 7)) - 1] ?? month }

/** « 29 oct. ». */
export function dayLabel(date: string): string { return `${Number(date.slice(8, 10))} ${SHORT[Number(date.slice(5, 7)) - 1] ?? ''}` }

/** The bank label without its technical parts: « CARTE 29/10/26 LE PASSIONNE CB*1234 » → « LE PASSIONNE ». */
export function cleanLabel(label: string): string {
  return label.replace(/^(CARTE|AVOIR)\s+\d\d\/\d\d\/\d\d\s+/i, '').replace(/\s+CB\*\d+$/i, '').replace(/^PRLV SEPA\s+/i, '')
    .replace(/^VIR (SEPA|INST)\s+/i, 'Virement ').trim()
}

/** Text proposed for a rule from a label; the user edits it before saving. */
export function ruleTextOf(label: string): string { return cleanLabel(label).replace(/^Virement /, '').toUpperCase() }

/** Labels a rule cannot classify: they hide purchases of any kind or a person. */
export function needsJudgement(label: string): boolean { return /PAYPAL|AMAZON|^VIR /i.test(label) }

export function classifiedLabel(o: BudgetOperationDto): string {
  if (!o.lineId) return 'à ranger'
  return o.classifiedBy === 'rule' ? 'rangée par règle' : o.classifiedBy === 'assistant' ? 'rangée par Claude' : 'rangée à la main'
}

export function amountLabel(line: BudgetLineDto): string {
  if (line.kind === 'extra') return 'Extra · non prévu'
  if (line.kind === 'off') return 'Hors budget · ne compte pas'
  if (line.fromSupplements) return `${kindOf(line.kind).label} · coût des compléments (Santé)`
  const a = line.amount
  if (!a) return kindOf(line.kind).label
  const per = a.period === 'year' ? ' par an' : a.period === 'months' ? ` tous les ${a.months} mois` : ' par mois'
  return `${kindOf(line.kind).label} · ${euro(a.amount)}${per}`
}

/** Parses « 1 234,50 », « -18.5 »; null when empty, NaN when invalid. */
export function parseAmount(text: string | number | null | undefined): number | null {
  if (text === null || text === undefined) return null
  if (typeof text === 'number') return text
  const clean = text.replace(/[\s  €]/g, '').replace('−', '-').replace(',', '.')
  if (!clean) return null
  return /^-?\d+(\.\d{1,2})?$/.test(clean) ? Number(clean) : Number.NaN
}

export interface LineDraft {
  name: string, category: string, kind: LineKind, reduce: boolean, fromSupplements: boolean, notes: string,
  amount: string, period: AmountPeriod, months: string, fromMonth: string
}

export function lineDraftOf(line: BudgetLineDto | null, currentMonth: string): LineDraft {
  return {
    name: line?.name ?? '', category: line && line.kind !== 'income' && line.kind !== 'off' ? line.category : '', kind: line?.kind ?? 'envelope',
    reduce: line?.reduce ?? false, fromSupplements: line?.fromSupplements ?? false, notes: line?.notes ?? '',
    amount: line?.amount ? String(line.amount.amount).replace('.', ',') : '', period: line?.amount?.period ?? 'month',
    months: String(line?.amount?.months && line.amount.period === 'months' ? line.amount.months : 3), fromMonth: currentMonth,
  }
}

export function hasAmount(kind: LineKind): boolean { return kind === 'fixed' || kind === 'envelope' || kind === 'income' }

/** The request of a line draft, or the message of what is missing. */
export function lineRequest(d: LineDraft, existing: BudgetLineDto | null): BudgetLineRequest | string {
  const name = d.name.trim()
  if (!name) return 'Indique un nom.'
  const expense = d.kind === 'fixed' || d.kind === 'envelope' || d.kind === 'extra'
  if (expense && !d.category.trim()) return 'Choisis ou écris un poste.'
  const supplements = (d.kind === 'fixed' || d.kind === 'envelope') && d.fromSupplements
  let amount: number | null = null
  if (hasAmount(d.kind) && !supplements) {
    amount = parseAmount(d.amount)
    if (amount !== null && (Number.isNaN(amount) || amount < 0)) return 'Montant invalide.'
    if (amount === null && d.kind !== 'income' && !existing?.amount) return 'Indique le montant prévu.'
  }
  const period: AmountPeriod = d.kind === 'income' ? 'month' : d.period
  const months = period === 'months' ? Number(d.months) : null
  if (months !== null && (!Number.isInteger(months) || months < 1 || months > 60)) return 'Tous les N mois : de 1 à 60.'
  const changed = amount !== null && (!existing?.amount || existing.amount.amount !== amount || existing.amount.period !== period
    || (period === 'months' && existing.amount.months !== months))
  return {
    name, category: expense ? d.category.trim() : null, kind: d.kind, reduce: expense && d.reduce, fromSupplements: supplements,
    notes: d.notes.trim() || null, amount: changed ? amount : null, period: changed ? period : null, months: changed ? months : null,
    fromMonth: existing && changed ? d.fromMonth : null,
  }
}

/** Monthly amount shown while typing. */
export function monthlyOf(d: LineDraft): number | null {
  const a = parseAmount(d.amount)
  if (a === null || Number.isNaN(a)) return null
  if (d.period === 'year') return Math.round(a / 12 * 100) / 100
  if (d.period === 'months') { const n = Number(d.months); return n >= 1 ? Math.round(a / n * 100) / 100 : null }
  return a
}

/** Share of a bar: 0 to 100. */
export function percent(value: number, max: number): number { return max <= 0 ? 0 : Math.max(0, Math.min(100, Math.round(value / max * 100))) }
