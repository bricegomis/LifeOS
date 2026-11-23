import type { Vo2MaxDto } from './sports.models.ts'

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']

/** The last value of each of the `months` months ending with the month of `today`, oldest first; null for a month without one. */
export function monthlyVo2(measures: Vo2MaxDto[], sport: string, today: string, months: number): { label: string, value: number | null }[] {
  const year = Number(today.slice(0, 4))
  const month = Number(today.slice(5, 7)) - 1
  const own = measures.filter(m => m.sport === sport).sort((a, b) => a.date.localeCompare(b.date))
  const points: { label: string, value: number | null }[] = []
  for (let i = months - 1; i >= 0; i--) {
    const index = year * 12 + month - i
    const key = `${Math.floor(index / 12)}-${String(index % 12 + 1).padStart(2, '0')}`
    const inMonth = own.filter(m => m.date.startsWith(key))
    points.push({ label: MONTHS[index % 12]! + (index % 12 === 0 ? ` ${Math.floor(index / 12)}` : ''), value: inMonth.at(-1)?.value ?? null })
  }
  // Drop the empty months before the first value, so the curve starts with the data.
  const first = points.findIndex(point => point.value !== null)
  return first < 0 ? [] : points.slice(first)
}

/** Last value, change over 12 months and best value; null without data. */
export function vo2Summary(measures: Vo2MaxDto[], sport: string, today: string): { last: number, lastDay: string, change: number | null, best: number } | null {
  const own = measures.filter(m => m.sport === sport).sort((a, b) => a.date.localeCompare(b.date))
  const last = own.at(-1)
  if (!last) return null
  const yearAgo = `${Number(today.slice(0, 4)) - 1}${today.slice(4)}`
  const before = own.filter(m => m.date <= yearAgo).at(-1) ?? own.find(m => m.date > yearAgo && m !== last)
  const [y, m, d] = last.date.split('-').map(Number)
  return {
    last: last.value,
    lastDay: `le ${d} ${MONTHS[m! - 1]} ${y}`,
    change: before && before !== last ? Math.round((last.value - before.value) * 10) / 10 : null,
    best: Math.max(...own.map(item => item.value)),
  }
}
