import type { SymptomDayDto, SymptomEntryDto, SymptomJournalDto } from './health.models.ts'

const DAYS = ['dim.', 'lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.']
const LONG_DAYS = ['Dimanche', 'Lundi', 'Mardi', 'Mercredi', 'Jeudi', 'Vendredi', 'Samedi']
const MONTHS = ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre']

function parts(iso: string): Date { return new Date(`${iso}T12:00:00Z`) }
export function addDays(iso: string, days: number): string { const d = parts(iso); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10) }

export function longDay(iso: string, today: string): string {
  if (iso === today) return 'Aujourd’hui'
  if (iso === addDays(today, -1)) return 'Hier'
  const d = parts(iso)
  return `${LONG_DAYS[d.getUTCDay()]} ${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}`
}

/** "light" 1-3, "medium" 4-6, "strong" 7-10. */
export function level(intensity: number): 'light' | 'medium' | 'strong' { return intensity >= 7 ? 'strong' : intensity >= 4 ? 'medium' : 'light' }

export interface DayCell { day: string, weekday: string, text: string, tone: 'none' | 'calm' | 'light' | 'medium' | 'strong', label: string }

/** The last `count` days, oldest first: the strongest intensity of the day, ✓ for "rien à signaler", empty when nothing was noted. */
export function dayStrip(journal: SymptomJournalDto, count = 14): DayCell[] {
  const byDay = new Map(journal.days.map(d => [d.day, d]))
  return Array.from({ length: count }, (_, i) => addDays(journal.today, i - count + 1)).map(day => {
    const d = byDay.get(day)
    const weekday = DAYS[parts(day).getUTCDay()]!.replace('.', '')
    if (d?.maxIntensity) return { day, weekday, text: String(d.maxIntensity), tone: level(d.maxIntensity), label: `${longDay(day, journal.today)} : intensité ${d.maxIntensity} au plus fort` }
    if (d?.calm) return { day, weekday, text: '✓', tone: 'calm', label: `${longDay(day, journal.today)} : rien à signaler` }
    return { day, weekday, text: '', tone: 'none', label: `${longDay(day, journal.today)} : rien de noté` }
  })
}

/** The journal days filtered by kind of symptom; a calm day only shows without a filter. */
export function filteredDays(days: SymptomDayDto[], kind: '' | 'physique' | 'mental'): SymptomDayDto[] {
  if (!kind) return days.filter(d => d.entries.length)
  return days.map(d => ({ ...d, entries: d.entries.filter(e => e.kind === kind) })).filter(d => d.entries.length)
}

export function entryMeta(entry: SymptomEntryDto): string {
  return [entry.time ? entry.time.slice(0, 5).replace(':', ' h ') : '', entry.note ?? ''].filter(x => x).join(' · ')
}

/** Today's status line of the daily check-in. */
export function checkinStatus(journal: SymptomJournalDto): string {
  const day = journal.days.find(d => d.day === journal.today)
  if (day?.calm) return 'Rien à signaler aujourd’hui.'
  if (day?.entries.length) return `${day.entries.length} symptôme${day.entries.length > 1 ? 's' : ''} noté${day.entries.length > 1 ? 's' : ''} aujourd’hui.`
  return 'Pas encore de bilan aujourd’hui.'
}
