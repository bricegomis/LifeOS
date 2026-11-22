import type { MarkerDto, MarkerRequest, MarkerStatus, MarkerTrend } from './health.models.ts'

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']
function parts(iso: string): Date { return new Date(`${iso}T12:00:00Z`) }

export function dateLabel(iso: string): string { const d = parts(iso); return `${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]} ${d.getUTCFullYear()}` }
export function monthLabel(iso: string): string { const d = parts(iso); return `${MONTHS[d.getUTCMonth()]} ${String(d.getUTCFullYear()).slice(2)}` }

export function num(value: number): string { return String(Math.round(value * 1000) / 1000).replace('.', ',') }
export function valueLabel(value: number, unit: string): string { return `${num(value)} ${unit}` }

export function rangeLabel(min: number | null, max: number | null, unit: string): string {
  if (min !== null && max !== null) return `${num(min)} – ${num(max)} ${unit}`
  if (max !== null) return `< ${num(max)} ${unit}`
  if (min !== null) return `> ${num(min)} ${unit}`
  return '—'
}

export const STATUS: Record<MarkerStatus, { label: string, tag: string }> = {
  below_lab: { label: 'Sous la plage du labo', tag: 'is-off' },
  above_lab: { label: 'Au-dessus de la plage du labo', tag: 'is-off' },
  in_target: { label: 'Dans ta cible', tag: 'is-ok' },
  normal: { label: 'Normal, hors de ta cible', tag: 'is-warn' },
}

export const TREND: Record<MarkerTrend, string> = { better: 'S’améliore', worse: 'Se dégrade', stable: 'Stable', in_target: 'Stable dans la cible' }

export function frequencyLabel(months: number): string {
  if (!months) return 'Une seule fois'
  if (months === 12) return 'Tous les ans'
  if (months % 12 === 0) return `Tous les ${months / 12} ans`
  return `Tous les ${months} mois`
}

/** "Jamais mesuré", "À refaire depuis le 3 mai 2026", "À refaire vers le …", "Une seule fois : fait". */
export function dueLabel(m: MarkerDto): string {
  if (!m.values.length) return 'Jamais mesuré'
  if (!m.nextDueOn) return 'Une seule fois : fait'
  return m.late ? `À refaire depuis le ${dateLabel(m.nextDueOn)}` : `À refaire vers le ${dateLabel(m.nextDueOn)}`
}

/** Positions (in %) of the lab range, the target and the last value on a common scale. */
export function rangeBar(m: MarkerDto): { labLeft: number, labWidth: number, targetLeft: number, targetWidth: number, value: number | null } {
  const values = m.values.map(v => v.value)
  const lows = [m.labMin, m.targetMin, ...values].filter((x): x is number => x !== null)
  const highs = [m.labMax, m.targetMax, ...values].filter((x): x is number => x !== null)
  let lo = Math.min(...lows), hi = Math.max(...highs)
  if (!Number.isFinite(lo) || !Number.isFinite(hi)) return { labLeft: 0, labWidth: 100, targetLeft: 0, targetWidth: 100, value: null }
  const pad = (hi - lo) * 0.15 || 1
  lo = Math.max(0, lo - pad); hi += pad
  const pct = (v: number) => Math.round(Math.max(0, Math.min(100, (v - lo) / (hi - lo) * 100)) * 10) / 10
  const labLeft = pct(m.labMin ?? lo), labRight = pct(m.labMax ?? hi)
  const targetLeft = pct(m.targetMin ?? lo), targetRight = pct(m.targetMax ?? hi)
  const last = m.values[m.values.length - 1]
  return { labLeft, labWidth: Math.round((labRight - labLeft) * 10) / 10, targetLeft, targetWidth: Math.round((targetRight - targetLeft) * 10) / 10, value: last ? pct(last.value) : null }
}

export interface MarkerDraft {
  name: string, unit: string, labMin: string, labMax: string, targetMin: string, targetMax: string, frequencyMonths: number, panels: string[]
}

export function markerDraftOf(m: MarkerDto | null): MarkerDraft {
  const s = (v: number | null) => v === null ? '' : num(v)
  return m
    ? { name: m.name, unit: m.unit, labMin: s(m.labMin), labMax: s(m.labMax), targetMin: s(m.targetMin), targetMax: s(m.targetMax), frequencyMonths: m.frequencyMonths, panels: [...m.panels] }
    : { name: '', unit: '', labMin: '', labMax: '', targetMin: '', targetMax: '', frequencyMonths: 12, panels: [] }
}

export function parseNumber(value: string | number | null): number | null {
  if (value === null || String(value).trim() === '') return null
  const n = Number(String(value).replace(',', '.').trim())
  return Number.isFinite(n) ? n : NaN
}

export function markerRequest(d: MarkerDraft): MarkerRequest | string {
  if (!d.name.trim()) return 'Le nom est obligatoire.'
  if (!d.unit.trim()) return 'L’unité est obligatoire.'
  const labMin = parseNumber(d.labMin), labMax = parseNumber(d.labMax), targetMin = parseNumber(d.targetMin), targetMax = parseNumber(d.targetMax)
  if ([labMin, labMax, targetMin, targetMax].some(v => Number.isNaN(v))) return 'Les bornes sont des nombres.'
  if (labMin !== null && labMax !== null && labMin > labMax) return 'Plage du labo : le minimum dépasse le maximum.'
  if (targetMin !== null && targetMax !== null && targetMin > targetMax) return 'Cible : le minimum dépasse le maximum.'
  return { name: d.name.trim(), unit: d.unit.trim(), labMin, labMax, targetMin, targetMax, frequencyMonths: d.frequencyMonths, panels: d.panels }
}
