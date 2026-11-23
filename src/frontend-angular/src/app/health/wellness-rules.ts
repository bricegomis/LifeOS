import type { WellnessDto } from './health.models.ts'

/** Sleep and recovery curves (ADR 0019): per night over 4 weeks, per week over 3 months, per month over a year. */
export type WellnessPeriod = '4w' | '3m' | '1y'
export type WellnessField = 'sleepSeconds' | 'deepSleepSeconds' | 'lightSleepSeconds' | 'remSleepSeconds' | 'awakeSeconds' | 'sleepScore'
  | 'restingHeartRate' | 'hrvNightAverage' | 'bodyBatteryWake' | 'stressAverage' | 'steps' | 'respiration'

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']
const DAY_MS = 86_400_000

function addDays(iso: string, days: number): string { return new Date(Date.parse(`${iso}T12:00:00Z`) + days * DAY_MS).toISOString().slice(0, 10) }
function short(iso: string): string { return `${Number(iso.slice(8, 10))}/${Number(iso.slice(5, 7))}` }

/** First day shown for a period ending today. */
export function periodStart(period: WellnessPeriod, today: string): string {
  return period === '4w' ? addDays(today, -27) : period === '3m' ? addDays(today, -90) : addDays(today, -364)
}

/** Groups of days: one per night, per 7 days or per calendar month, oldest first. */
export function buckets(period: WellnessPeriod, today: string): { label: string, from: string, to: string }[] {
  if (period === '4w') return Array.from({ length: 28 }, (_, i) => { const day = addDays(today, i - 27); return { label: short(day), from: day, to: day } })
  if (period === '3m') return Array.from({ length: 13 }, (_, i) => { const to = addDays(today, -7 * (12 - i)); const from = addDays(to, -6); return { label: short(from), from, to } })
  const year = Number(today.slice(0, 4)); const month = Number(today.slice(5, 7)) - 1
  return Array.from({ length: 12 }, (_, i) => {
    const index = year * 12 + month - (11 - i)
    const y = Math.floor(index / 12); const m = index % 12
    const from = `${y}-${String(m + 1).padStart(2, '0')}-01`
    const next = m === 11 ? `${y + 1}-01-01` : `${y}-${String(m + 2).padStart(2, '0')}-01`
    return { label: MONTHS[m]!, from, to: addDays(next, -1) }
  })
}

/** Average of a field over days, ignoring days without it; null when no day has it. */
export function average(days: WellnessDto[], field: WellnessField): number | null {
  const values = days.map(day => day[field]).filter((value): value is number => typeof value === 'number')
  return values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : null
}

export function series(days: WellnessDto[], period: WellnessPeriod, today: string, field: WellnessField): { label: string, value: number | null }[] {
  return buckets(period, today).map(bucket => ({ label: bucket.label, value: average(days.filter(day => day.date >= bucket.from && day.date <= bucket.to), field) }))
}

export interface SleepColumn { label: string, tip: string, empty: boolean, deep: number, light: number, rem: number, awake: number }

/** Stacked sleep phases as heights in % of an 11 h night; a night without data stays empty. */
export function sleepColumns(days: WellnessDto[], period: WellnessPeriod, today: string): SleepColumn[] {
  const scale = (seconds: number | null) => seconds === null ? 0 : Math.round(seconds / (11 * 3600) * 1000) / 10
  return buckets(period, today).map(bucket => {
    const inside = days.filter(day => day.date >= bucket.from && day.date <= bucket.to && day.sleepSeconds !== null)
    const total = average(inside, 'sleepSeconds')
    if (total === null) return { label: bucket.label, tip: `${bucket.label} : pas de données`, empty: true, deep: 0, light: 0, rem: 0, awake: 0 }
    const deep = average(inside, 'deepSleepSeconds'); const rem = average(inside, 'remSleepSeconds')
    const light = average(inside, 'lightSleepSeconds') ?? Math.max(0, total - (deep ?? 0) - (rem ?? 0))
    return { label: bucket.label, tip: `${bucket.label} : ${hoursMinutes(total)}`, empty: false, deep: scale(deep), light: scale(light), rem: scale(rem), awake: scale(average(inside, 'awakeSeconds')) }
  })
}

/** "6 h 13" from seconds. */
export function hoursMinutes(seconds: number | null): string {
  if (seconds === null) return '—'
  const minutes = Math.round(seconds / 60)
  return `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')}`
}

/** The usual HRV range the watch reported most recently. */
export function hrvBand(days: WellnessDto[]): [number, number] | null {
  const last = [...days].reverse().find(day => day.hrvBaselineLow !== null && day.hrvBaselineHigh !== null)
  return last ? [last.hrvBaselineLow!, last.hrvBaselineHigh!] : null
}
