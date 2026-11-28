import type { BookDto, BookFormat, BookReadingDto, BookState } from './reading.models.ts'

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']

export const FORMATS: { value: BookFormat, label: string, icon: string }[] = [
  { value: 'paper', label: 'Papier', icon: 'menu_book' },
  { value: 'ereader', label: 'Liseuse', icon: 'tablet_android' },
  { value: 'audio', label: 'Audio', icon: 'headphones' },
  { value: 'comic', label: 'BD / manga', icon: 'auto_stories' },
]

export interface StateInfo { label: string, icon: string, tag: string }

/** Label, icon and `lo-tag` modifier of each state (ADR 0022). */
export const STATES: Record<BookState, StateInfo> = {
  reading: { label: 'En cours', icon: 'auto_stories', tag: 'is-warn' },
  toread: { label: 'À lire', icon: 'bookmark', tag: '' },
  read: { label: 'Lu', icon: 'check_circle', tag: 'is-ok' },
  stopped: { label: 'Arrêté', icon: 'pause_circle', tag: 'is-off' },
  notwanted: { label: 'Pas envie', icon: 'do_not_disturb_on', tag: 'is-off' },
}

/** The library's tabs, in order; « Tous » leaves « Pas envie » out. */
export const TABS: { value: 'all' | BookState, label: string }[] = [
  { value: 'all', label: 'Tous' },
  { value: 'reading', label: 'En cours' },
  { value: 'toread', label: 'À lire' },
  { value: 'read', label: 'Lus' },
  { value: 'stopped', label: 'Arrêtés' },
  { value: 'notwanted', label: 'Pas envie' },
]

const STATE_ORDER: Record<BookState, number> = { reading: 0, toread: 1, read: 2, stopped: 3, notwanted: 4 }

export function formatInfo(format: BookFormat): { label: string, icon: string } {
  return FORMATS.find(f => f.value === format) ?? FORMATS[0]!
}

/** Lower case, without accents nor punctuation. */
export function normalize(text: string): string {
  return text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase('fr').replace(/[^\p{L}\p{N}]+/gu, ' ')
}

export interface LibraryFilter { tab: 'all' | BookState, query: string, format: BookFormat | null, genre: string | null }

function matches(book: BookDto, filter: Omit<LibraryFilter, 'tab'>): boolean {
  if (filter.format && book.format !== filter.format) return false
  if (filter.genre && !book.genres.some(g => g.toLocaleLowerCase('fr') === filter.genre!.toLocaleLowerCase('fr'))) return false
  const words = normalize(filter.query).split(' ').filter(Boolean)
  if (!words.length) return true
  const text = normalize([book.title, book.authors, book.seriesName ?? '', ...book.genres].join(' '))
  return words.every(w => text.includes(w))
}

function inTab(book: BookDto, tab: 'all' | BookState): boolean {
  return tab === 'all' ? book.state !== 'notwanted' : book.state === tab
}

/** In progress first, then to read, read, stopped, not wanted; by title within a state. */
export function sortBooks(books: BookDto[]): BookDto[] {
  return [...books].sort((a, b) => STATE_ORDER[a.state] - STATE_ORDER[b.state] || a.title.localeCompare(b.title, 'fr'))
}

/** The books of the tab that match the search and the chips. */
export function filterBooks(books: BookDto[], filter: LibraryFilter): BookDto[] {
  return sortBooks(books.filter(b => inTab(b, filter.tab) && matches(b, filter)))
}

/** How many books each tab holds with the current search and chips. */
export function tabCounts(books: BookDto[], filter: Omit<LibraryFilter, 'tab'>): Record<'all' | BookState, number> {
  const shown = books.filter(b => matches(b, filter))
  return Object.fromEntries(TABS.map(t => [t.value, shown.filter(b => inTab(b, t.value)).length])) as Record<'all' | BookState, number>
}

/** Every genre used, in alphabetical order, case ignored. */
export function genresOf(books: BookDto[]): string[] {
  const seen = new Map<string, string>()
  for (const g of books.flatMap(b => b.genres)) if (!seen.has(g.toLocaleLowerCase('fr'))) seen.set(g.toLocaleLowerCase('fr'), g)
  return [...seen.values()].sort((a, b) => a.localeCompare(b, 'fr'))
}

/** "28 nov. 2026". */
export function dateLabel(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return `${d} ${MONTHS[(m ?? 1) - 1]} ${y}`
}

/** "2,5" for a tome number. */
export function tomeLabel(tome: number): string {
  return String(tome).replace('.', ',')
}

/** "Dune · tome 2", "Dune · tome 2 sur 6". */
export function seriesLabel(book: BookDto, total: number | null = null): string {
  if (!book.seriesName) return ''
  if (book.tome === null) return book.seriesName
  return `${book.seriesName} · tome ${tomeLabel(book.tome)}${total ? ` sur ${total}` : ''}`
}

/** One line under the title on a card: "9/10 · Papier", "Liseuse". */
export function cardMeta(book: BookDto): string {
  const format = formatInfo(book.format).label
  return book.lastRating !== null ? `${book.lastRating}/10 · ${format}` : format
}

/** The sentence next to the state on a book page. */
export function stateDetail(book: BookDto): string {
  const last = book.readings.at(-1)
  switch (book.state) {
    case 'toread': return 'Dans la bibliothèque, pas encore commencé'
    case 'reading': return book.startedOn ? `Depuis le ${dateLabel(book.startedOn)}` : 'En cours'
    case 'read':
      if (book.readCount > 1) return `Lu ${book.readCount} fois`
      return last?.finishedOn ? `Le ${dateLabel(last.finishedOn)}` : 'Date inconnue'
    case 'stopped': return last?.stopReason ?? (last?.finishedOn ? `Le ${dateLabel(last.finishedOn)}` : '')
    case 'notwanted': return book.notWantedReason ?? 'Claude ne le proposera plus'
  }
}

/** "1re lecture · Lu", or "Lu" alone when there is one reading. */
export function readingTitle(reading: BookReadingDto, index: number, count: number): string {
  const outcome = reading.outcome === 'reading' ? 'En cours' : reading.outcome === 'read' ? 'Lu' : 'Arrêté'
  if (count < 2) return outcome
  return `${index === 0 ? '1re' : `${index + 1}e`} lecture · ${outcome}`
}

/** "Du 2 oct. 2026 au 20 oct. 2026", "Commencé le …", "Fini le …", "Dates inconnues". */
export function readingDates(reading: BookReadingDto): string {
  const { startedOn: start, finishedOn: end } = reading
  if (reading.outcome === 'reading') return start ? `Commencé le ${dateLabel(start)}` : 'En cours'
  if (start && end) return `Du ${dateLabel(start)} au ${dateLabel(end)}`
  if (end) return `${reading.outcome === 'read' ? 'Fini' : 'Arrêté'} le ${dateLabel(end)}`
  if (start) return `Commencé le ${dateLabel(start)}`
  return 'Dates inconnues'
}

/** Genres typed as "SF, Polar ; Essai": trimmed, without duplicates (case ignored), 10 at most. */
export function parseGenres(text: string): string[] {
  const seen = new Map<string, string>()
  for (const part of text.split(/[,;]/).map(p => p.trim()).filter(Boolean)) if (!seen.has(part.toLocaleLowerCase('fr'))) seen.set(part.toLocaleLowerCase('fr'), part)
  return [...seen.values()].slice(0, 10)
}

/** A tome typed with a comma or a dot; empty or invalid = none. */
export function parseTome(text: string | number | null): number | null {
  if (text === null || text === '') return null
  const value = typeof text === 'number' ? text : Number(String(text).trim().replace(',', '.'))
  return Number.isFinite(value) && value > 0 ? Math.round(value * 100) / 100 : null
}

/** First letter of the title, for a book without a cover. */
export function initial(title: string): string {
  return (title.trim().match(/\p{L}|\p{N}/u)?.[0] ?? '?').toLocaleUpperCase('fr')
}

/** Today as yyyy-MM-dd in the browser's time zone (the API checks the Paris day). */
export function localToday(now = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}
