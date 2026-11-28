/** Reading module (ADR 0022): what the API returns and accepts. */

export type BookFormat = 'paper' | 'ereader' | 'audio' | 'comic'
export type BookState = 'toread' | 'reading' | 'read' | 'stopped' | 'notwanted'
/** `all`: every state but « pas envie »; `any`: every book. */
export type BookStateFilter = 'all' | 'any' | BookState
export type ReadingOutcome = 'reading' | 'read' | 'stopped'

export interface BookReadingDto {
  id: string
  outcome: ReadingOutcome
  startedOn: string | null
  finishedOn: string | null
  rating: number | null
  review: string | null
  stopReason: string | null
}

export interface BookQuoteDto { id: string, text: string, place: string | null }

export interface BookDto {
  id: string
  memberProfileId: string
  title: string
  authors: string
  format: BookFormat
  genres: string[]
  seriesId: string | null
  seriesName: string | null
  tome: number | null
  notes: string | null
  state: BookState
  notWantedReason: string | null
  lastRating: number | null
  readCount: number
  startedOn: string | null
  lastFinishedOn: string | null
  hasImage: boolean
  /** Oldest first. */
  readings: BookReadingDto[]
  quotes: BookQuoteDto[]
  updatedAt: string
}

export interface BookRequest {
  title: string
  authors: string | null
  format: BookFormat
  genres: string[]
  notes: string | null
  seriesId: string | null
  tome: number | null
}

/** How the book enters the library. */
export interface BookCreateRequest extends BookRequest { state: 'toread' | 'reading' | 'read' | 'notwanted' }

export interface BookFinishRequest { day: string | null, rating: number | null, review: string | null }
export interface BookStopRequest { day: string | null, reason: string | null }
export interface BookPastReadingRequest {
  outcome: 'read' | 'stopped'
  startedOn: string | null
  finishedOn: string | null
  rating: number | null
  review: string | null
  reason: string | null
}
export interface BookReadingUpdateRequest {
  startedOn: string | null
  finishedOn: string | null
  rating: number | null
  review: string | null
  reason: string | null
}
export interface BookQuoteRequest { text: string, place: string | null }

export interface BookSeriesDto {
  id: string
  name: string
  totalTomes: number | null
  bookCount: number
  readCount: number
  toReadCount: number
  missingTomes: number[]
}

/** `book` is null for a missing tome. */
export interface BookSeriesTomeDto { number: number | null, book: BookDto | null }
export interface BookSeriesDetailDto { series: BookSeriesDto, tomes: BookSeriesTomeDto[] }
export interface BookSeriesRequest { name: string, totalTomes: number | null }
