import type { MarkerDto, MarkerRequest, MarkerStatus, MarkerTrend, MeasureSource, MeasureStatus, MeasureTypeRequest } from './health.models.ts'

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
  in_lab: { label: 'Dans la norme du labo, sans cible', tag: '' },
}

export const TREND: Record<MarkerTrend, string> = {
  better: 'S’améliore', worse: 'Se dégrade', stable: 'Stable', in_target: 'Stable dans la cible', in_lab: 'Stable dans la norme du labo',
}

/** Values out of the personal target: in the lab range but not the target, or out of the lab range. */
export function isOffTarget(status: MarkerStatus | null): boolean { return status === 'normal' || status === 'below_lab' || status === 'above_lab' }

export const MEASURE_STATUS: Record<MeasureStatus, { label: string, tag: string }> = {
  below_target: { label: 'Sous ta cible', tag: 'is-warn' },
  in_target: { label: 'Dans ta cible', tag: 'is-ok' },
  above_target: { label: 'Au-dessus de ta cible', tag: 'is-warn' },
}

/** A body measure kind from the form; the target is optional (none for what cannot change, such as height). */
export function measureTypeRequest(name: string, unit: string, targetMin: string, targetMax: string, source: MeasureSource | null = null): MeasureTypeRequest | string {
  if (!name.trim() || !unit.trim()) return 'Nom et unité sont obligatoires.'
  const min = parseNumber(targetMin), max = parseNumber(targetMax)
  if (Number.isNaN(min) || Number.isNaN(max)) return 'Les bornes de la cible sont des nombres.'
  if (min !== null && max !== null && min > max) return 'Cible : le minimum dépasse le maximum.'
  return { name: name.trim(), unit: unit.trim(), targetMin: min, targetMax: max, source }
}

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
  const hasTarget = m.targetMin !== null || m.targetMax !== null
  const targetLeft = hasTarget ? pct(m.targetMin ?? lo) : 0, targetRight = hasTarget ? pct(m.targetMax ?? hi) : 0
  const last = m.values[m.values.length - 1]
  return { labLeft, labWidth: Math.round((labRight - labLeft) * 10) / 10, targetLeft, targetWidth: Math.round((targetRight - targetLeft) * 10) / 10, value: last ? pct(last.value) : null }
}

export interface MarkerDraft {
  name: string, unit: string, labMin: string, labMax: string, targetMin: string, targetMax: string, frequencyMonths: number, panels: string[],
  description: string, whenLow: string, whenHigh: string
}

export function markerDraftOf(m: MarkerDto | null): MarkerDraft {
  const s = (v: number | null) => v === null ? '' : num(v)
  return m
    ? { name: m.name, unit: m.unit, labMin: s(m.labMin), labMax: s(m.labMax), targetMin: s(m.targetMin), targetMax: s(m.targetMax), frequencyMonths: m.frequencyMonths, panels: [...m.panels],
      description: m.description ?? '', whenLow: m.whenLow ?? '', whenHigh: m.whenHigh ?? '' }
    : { name: '', unit: '', labMin: '', labMax: '', targetMin: '', targetMax: '', frequencyMonths: 12, panels: [], description: '', whenLow: '', whenHigh: '' }
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
  const text = (v: string) => v.trim() || null
  return { name: d.name.trim(), unit: d.unit.trim(), labMin, labMax, targetMin, targetMax, frequencyMonths: d.frequencyMonths, panels: d.panels,
    description: text(d.description), whenLow: text(d.whenLow), whenHigh: text(d.whenHigh) }
}

export type MarkerPositionFilter = 'all' | 'in_target' | 'off_target' | 'in_lab_range' | 'off_lab_range' | 'no_target'
export type MarkerFollowUpFilter = 'all' | 'due' | 'late' | 'never' | 'up_to_date'
export interface MarkerFilter { query: string, position: MarkerPositionFilter, followUp: MarkerFollowUpFilter, panel: string }

export const POSITION_FILTERS: { value: MarkerPositionFilter, label: string }[] = [
  { value: 'all', label: 'Toutes les positions' },
  { value: 'in_target', label: 'Dans ta cible' },
  { value: 'off_target', label: 'Hors de ta cible' },
  { value: 'in_lab_range', label: 'Dans la norme du labo' },
  { value: 'off_lab_range', label: 'Hors de la norme du labo' },
  { value: 'no_target', label: 'Sans cible' },
]

export const FOLLOW_UP_FILTERS: { value: MarkerFollowUpFilter, label: string }[] = [
  { value: 'all', label: 'Tout le suivi' },
  { value: 'due', label: 'À faire ou refaire' },
  { value: 'late', label: 'En retard' },
  { value: 'never', label: 'Jamais mesuré' },
  { value: 'up_to_date', label: 'À jour' },
]

/** Position of the last value; a marker never measured has none and only shows under "all". */
export function matchesPosition(status: MarkerStatus | null, filter: MarkerPositionFilter): boolean {
  switch (filter) {
    case 'all': return true
    case 'in_target': return status === 'in_target'
    case 'off_target': return isOffTarget(status)
    case 'in_lab_range': return status === 'in_target' || status === 'normal' || status === 'in_lab'
    case 'off_lab_range': return status === 'below_lab' || status === 'above_lab'
    case 'no_target': return status === 'in_lab'
  }
}

export function matchesFollowUp(m: MarkerDto, filter: MarkerFollowUpFilter): boolean {
  switch (filter) {
    case 'all': return true
    case 'due': return m.due
    case 'late': return m.late
    case 'never': return !m.values.length
    case 'up_to_date': return !m.due
  }
}

function normalize(value: string): string { return value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase('fr') }

/** Markers page: search in the name and panels (case and accents ignored), position, follow-up and panel filters; by name. */
export function filterMarkers(markers: MarkerDto[], f: MarkerFilter): MarkerDto[] {
  const wanted = normalize(f.query.trim())
  return markers
    .filter(m => !wanted || normalize([m.name, ...m.panels].join(' ')).includes(wanted))
    .filter(m => matchesPosition(m.status, f.position) && matchesFollowUp(m, f.followUp) && (!f.panel || m.panels.includes(f.panel)))
    .sort((a, b) => a.name.localeCompare(b.name, 'fr'))
}
