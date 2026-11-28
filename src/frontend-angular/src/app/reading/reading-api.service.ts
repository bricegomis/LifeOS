import { inject, Injectable } from '@angular/core'
import { from, switchMap, type Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LIBRARY_IMAGE, reduceImage } from '@/app/shared/image-reduction'
import type {
  BookCreateRequest, BookDto, BookFinishRequest, BookPastReadingRequest, BookQuoteRequest, BookReadingUpdateRequest, BookRequest,
  BookSeriesDetailDto, BookSeriesDto, BookSeriesRequest, BookStateFilter, BookStopRequest,
} from './reading.models'

const id = (value: string) => encodeURIComponent(value)

/** Every HTTP call of the Reading screens (ADR 0022). Without a profile, the API uses the household's main profile. */
@Injectable({ providedIn: 'root' })
export class ReadingApiService {
  private readonly api = inject(LifeosApiService)

  /** Every book (« pas envie » included); the library filters on the client. */
  books(state: BookStateFilter = 'any', seriesId = ''): Observable<BookDto[]> {
    return this.api.get<BookDto[]>('/reading/books', { state, ...(seriesId ? { seriesId } : {}) })
  }
  book(bookId: string): Observable<BookDto> { return this.api.get<BookDto>(`/reading/books/${id(bookId)}`) }
  genres(): Observable<string[]> { return this.api.get<string[]>('/reading/books/genres') }
  createBook(request: BookCreateRequest): Observable<BookDto> { return this.api.post<BookDto>('/reading/books', request) }
  updateBook(bookId: string, request: BookRequest): Observable<BookDto> { return this.api.put<BookDto>(`/reading/books/${id(bookId)}`, request) }
  deleteBook(bookId: string): Observable<void> { return this.api.delete(`/reading/books/${id(bookId)}`) }

  /** A first reading, a re-read or a resumed book; `day` is today when null. */
  start(bookId: string, day: string | null = null): Observable<BookDto> { return this.api.post<BookDto>(`/reading/books/${id(bookId)}/start`, { day }) }
  finish(bookId: string, request: BookFinishRequest): Observable<BookDto> { return this.api.post<BookDto>(`/reading/books/${id(bookId)}/finish`, request) }
  stop(bookId: string, request: BookStopRequest): Observable<BookDto> { return this.api.post<BookDto>(`/reading/books/${id(bookId)}/stop`, request) }
  addPastReading(bookId: string, request: BookPastReadingRequest): Observable<BookDto> {
    return this.api.post<BookDto>(`/reading/books/${id(bookId)}/readings`, request)
  }
  updateReading(bookId: string, readingId: string, request: BookReadingUpdateRequest): Observable<BookDto> {
    return this.api.put<BookDto>(`/reading/books/${id(bookId)}/readings/${id(readingId)}`, request)
  }
  removeReading(bookId: string, readingId: string): Observable<BookDto> {
    return this.api.delete<BookDto>(`/reading/books/${id(bookId)}/readings/${id(readingId)}`)
  }
  notWanted(bookId: string, reason: string | null): Observable<BookDto> {
    return this.api.post<BookDto>(`/reading/books/${id(bookId)}/not-wanted`, { reason })
  }
  clearNotWanted(bookId: string): Observable<BookDto> { return this.api.delete<BookDto>(`/reading/books/${id(bookId)}/not-wanted`) }

  addQuote(bookId: string, request: BookQuoteRequest): Observable<BookDto> { return this.api.post<BookDto>(`/reading/books/${id(bookId)}/quotes`, request) }
  updateQuote(bookId: string, quoteId: string, request: BookQuoteRequest): Observable<BookDto> {
    return this.api.put<BookDto>(`/reading/books/${id(bookId)}/quotes/${id(quoteId)}`, request)
  }
  removeQuote(bookId: string, quoteId: string): Observable<BookDto> {
    return this.api.delete<BookDto>(`/reading/books/${id(bookId)}/quotes/${id(quoteId)}`)
  }

  /** Reduces the picture, then uploads it; returns the updated book. */
  uploadCover(bookId: string, file: File): Observable<BookDto> {
    return from(reduceImage(file, LIBRARY_IMAGE)).pipe(switchMap(image => {
      const form = new FormData()
      form.append('image', image)
      return this.api.put<BookDto>(`/reading/books/${id(bookId)}/image`, form)
    }))
  }
  removeCover(bookId: string): Observable<void> { return this.api.delete(`/reading/books/${id(bookId)}/image`) }

  seriesList(): Observable<BookSeriesDto[]> { return this.api.get<BookSeriesDto[]>('/reading/series') }
  series(seriesId: string): Observable<BookSeriesDetailDto> { return this.api.get<BookSeriesDetailDto>(`/reading/series/${id(seriesId)}`) }
  createSeries(request: BookSeriesRequest): Observable<BookSeriesDetailDto> { return this.api.post<BookSeriesDetailDto>('/reading/series', request) }
  updateSeries(seriesId: string, request: BookSeriesRequest): Observable<BookSeriesDetailDto> {
    return this.api.put<BookSeriesDetailDto>(`/reading/series/${id(seriesId)}`, request)
  }
  deleteSeries(seriesId: string): Observable<void> { return this.api.delete(`/reading/series/${id(seriesId)}`) }
}
