import type { PeriodUnit, SupplementDto, SupplementMoment, SupplementRequest, SupplementUnit } from './health.models.ts'

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']
const DAYS = ['dim.', 'lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.']

export const UNITS: { value: SupplementUnit, label: string, plural: string }[] = [
  { value: 'gelule', label: 'gélule', plural: 'gélules' },
  { value: 'comprime', label: 'comprimé', plural: 'comprimés' },
  { value: 'g', label: 'g', plural: 'g' },
  { value: 'ml', label: 'ml', plural: 'ml' },
  { value: 'goutte', label: 'goutte', plural: 'gouttes' },
  { value: 'dose', label: 'dose', plural: 'doses' },
]

export const MOMENTS: { value: SupplementMoment, label: string }[] = [
  { value: 'matin', label: 'Matin' }, { value: 'midi', label: 'Midi' }, { value: 'soir', label: 'Soir' }, { value: 'choix', label: 'Au choix' },
]

function parts(iso: string): Date { return new Date(`${iso}T12:00:00Z`) }

export function shortDate(iso: string): string { const d = parts(iso); return `${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}` }
export function weekday(iso: string): string { return DAYS[parts(iso).getUTCDay()]!.replace('.', '') }
export function dayOfMonth(iso: string): number { return parts(iso).getUTCDate() }

export function decimal(value: number): string { return String(Math.round(value * 100) / 100).replace('.', ',') }
export function euro(value: number): string { return `${value.toFixed(2).replace('.', ',')} €` }

export function unitLabel(unit: SupplementUnit, quantity: number): string {
  const u = UNITS.find(x => x.value === unit)
  return !u ? unit : quantity > 1 ? u.plural : u.label
}

export function doseLabel(s: Pick<SupplementDto, 'dose' | 'unit'>): string { return `${decimal(s.dose)} ${unitLabel(s.unit, s.dose)} par jour` }
export function momentLabel(moment: SupplementMoment): string { return MOMENTS.find(m => m.value === moment)?.label ?? moment }

function periodUnit(unit: PeriodUnit, n: number): string { return unit === 'week' ? `semaine${n > 1 ? 's' : ''}` : 'mois' }

/** "À l'année", "Cure de 3 mois, pause de 1 mois, puis on recommence"… */
export function periodLabel(s: Pick<SupplementDto, 'period'>): string {
  const p = s.period
  if (p.kind === 'year') return `À l’année depuis le ${shortDate(p.startOn)}`
  const cure = `Cure de ${p.cureLength} ${periodUnit(p.cureUnit, p.cureLength)}`
  return p.pauseLength ? `${cure}, pause de ${p.pauseLength} ${periodUnit(p.pauseUnit, p.pauseLength)}, puis on recommence` : `${cure}, une fois`
}

/** Where it stands today: "À l'année", "Cure : jour 12 sur 90", "Pause jusqu'au 5 déc."… */
export function statusLabel(s: Pick<SupplementDto, 'isStopped' | 'period' | 'status'>): string {
  if (s.isStopped) return 'Arrêté'
  const st = s.status
  if (st.dayInCure && st.cureDays) return `Cure : jour ${st.dayInCure} sur ${st.cureDays}`
  if (st.ended) return 'Cure terminée'
  if (st.nextCureOn) return s.period.startOn === st.nextCureOn ? `Commence le ${shortDate(st.nextCureOn)}` : `Pause jusqu’au ${shortDate(st.nextCureOn)}`
  return 'À l’année'
}

/** Price per 100 units of the package, "6,23 € les 100 gélules". */
export function unitPrice(s: Pick<SupplementDto, 'packagePrice' | 'packageContent' | 'unit'>): string {
  if (s.packagePrice === null) return ''
  return `${euro(s.packagePrice / s.packageContent * 100)} les 100 ${unitLabel(s.unit, 100)}`
}

/** Supplements to take today, grouped by moment of the day. */
export function todayGroups(supplements: SupplementDto[]): { moment: SupplementMoment, label: string, items: SupplementDto[] }[] {
  const active = supplements.filter(s => s.status.active)
  return MOMENTS.map(m => ({ moment: m.value, label: m.label, items: active.filter(s => s.moment === m.value) })).filter(g => g.items.length)
}

export interface SupplementDraft {
  name: string
  notes: string
  dose: number | string
  unit: SupplementUnit
  moment: SupplementMoment
  packageContent: number | string
  packagePrice: number | string | null
  shop: string
  links: { label: string, url: string }[]
  kind: 'year' | 'cure'
  startOn: string
  cureLength: number | string
  cureUnit: PeriodUnit
  pauseLength: number | string
  pauseUnit: PeriodUnit
}

export function supplementDraftOf(s: SupplementDto | null, today: string): SupplementDraft {
  if (!s) {
    return { name: '', notes: '', dose: 1, unit: 'gelule', moment: 'matin', packageContent: '', packagePrice: '', shop: '', links: [], kind: 'year',
      startOn: today, cureLength: 3, cureUnit: 'month', pauseLength: 0, pauseUnit: 'month' }
  }
  return {
    name: s.name, notes: s.notes ?? '', dose: s.dose, unit: s.unit, moment: s.moment, packageContent: s.packageContent, packagePrice: s.packagePrice ?? '',
    shop: s.shop ?? '', links: s.links.map(l => ({ ...l })), kind: s.period.kind, startOn: s.period.startOn,
    cureLength: s.period.kind === 'cure' ? s.period.cureLength : 3, cureUnit: s.period.cureUnit, pauseLength: s.period.pauseLength, pauseUnit: s.period.pauseUnit,
  }
}

function amount(value: number | string | null): number | null {
  if (value === null || String(value).trim() === '') return null
  const n = Number(String(value).replace(',', '.'))
  return Number.isFinite(n) ? n : NaN
}

/** The request of a draft, or the message to show. */
export function supplementRequest(d: SupplementDraft): SupplementRequest | string {
  const name = d.name.trim()
  if (!name) return 'Le nom est obligatoire.'
  const dose = amount(d.dose)
  if (dose === null || Number.isNaN(dose) || dose <= 0) return 'Dose par jour : un nombre supérieur à 0.'
  const content = amount(d.packageContent)
  if (content === null || Number.isNaN(content) || content <= 0) return 'Contenu du conditionnement : un nombre supérieur à 0, dans l’unité de la dose.'
  const price = amount(d.packagePrice)
  if (Number.isNaN(price) || (price !== null && price < 0)) return 'Prix : un montant en euros.'
  const links = d.links.filter(l => l.url.trim() || l.label.trim())
  if (links.some(l => !/^https:\/\//.test(l.url.trim()))) return 'Chaque lien d’achat est une adresse qui commence par https://'
  let cureLength: number | null = null
  let pauseLength: number | null = null
  if (d.kind === 'cure') {
    cureLength = amount(d.cureLength)
    if (cureLength === null || !Number.isInteger(cureLength) || cureLength < 1 || cureLength > 52) return 'Durée de la cure : de 1 à 52.'
    pauseLength = amount(d.pauseLength) ?? 0
    if (!Number.isInteger(pauseLength) || pauseLength < 0 || pauseLength > 52) return 'Pause : de 0 à 52.'
  }
  return {
    name, notes: d.notes.trim() || null, dose, unit: d.unit, moment: d.moment, packageContent: content, packagePrice: price, shop: d.shop.trim() || null,
    links: links.map(l => ({ label: l.label.trim() || null, url: l.url.trim() })),
    period: { kind: d.kind, startOn: d.startOn || null, cureLength, cureUnit: d.kind === 'cure' ? d.cureUnit : null, pauseLength, pauseUnit: d.kind === 'cure' ? d.pauseUnit : null },
  }
}
