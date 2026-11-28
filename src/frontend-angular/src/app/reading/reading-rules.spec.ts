import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { BookDto, BookReadingDto } from './reading.models.ts'
import {
  cardMeta, dateLabel, filterBooks, genresOf, initial, parseGenres, parseTome, readingDates, readingTitle, seriesLabel, stateDetail, tabCounts,
} from './reading-rules.ts'

function book(over: Partial<BookDto>): BookDto {
  return {
    id: over.title ?? 'x', memberProfileId: 'p', title: 'Livre', authors: '', format: 'paper', genres: [], seriesId: null, seriesName: null,
    tome: null, notes: null, state: 'toread', notWantedReason: null, lastRating: null, readCount: 0, startedOn: null, lastFinishedOn: null,
    hasImage: false, readings: [], quotes: [], updatedAt: '', ...over,
  }
}

const reading = (over: Partial<BookReadingDto>): BookReadingDto =>
  ({ id: 'r', outcome: 'read', startedOn: null, finishedOn: null, rating: null, review: null, stopReason: null, ...over })

const library = [
  book({ title: 'Sapiens', authors: 'Yuval Noah Harari', format: 'ereader', genres: ['Essai'], state: 'stopped' }),
  book({ title: 'Dune', authors: 'Frank Herbert', genres: ['SF'], state: 'read', lastRating: 9, seriesName: 'Dune', tome: 1 }),
  book({ title: 'Les Enfants de Dune', authors: 'Frank Herbert', format: 'ereader', genres: ['sf'], state: 'reading' }),
  book({ title: 'Da Vinci Code', state: 'notwanted' }),
  book({ title: 'L’Étranger', authors: 'Albert Camus', state: 'toread' }),
]

describe('reading rules', () => {
  it('sorts by state then title and leaves « pas envie » out of « Tous »', () => {
    const all = { tab: 'all' as const, query: '', format: null, genre: null }
    assert.deepEqual(filterBooks(library, all).map(b => b.title), ['Les Enfants de Dune', 'L’Étranger', 'Dune', 'Sapiens'])
    assert.deepEqual(filterBooks(library, { ...all, tab: 'notwanted' }).map(b => b.title), ['Da Vinci Code'])
    assert.deepEqual(filterBooks(library, { ...all, query: 'etranger camus' }).map(b => b.title), ['L’Étranger'])
    assert.deepEqual(filterBooks(library, { ...all, query: 'HERBERT', format: 'ereader' }).map(b => b.title), ['Les Enfants de Dune'])
    assert.deepEqual(filterBooks(library, { ...all, genre: 'SF' }).map(b => b.title), ['Les Enfants de Dune', 'Dune'])
    const counts = tabCounts(library, { query: '', format: null, genre: null })
    assert.deepEqual([counts.all, counts.reading, counts.toread, counts.read, counts.stopped, counts.notwanted], [4, 1, 1, 1, 1, 1])
    assert.deepEqual(genresOf(library), ['Essai', 'SF'])
  })

  it('labels dates, readings, series and cards', () => {
    assert.equal(dateLabel('2026-11-28'), '28 nov. 2026')
    assert.equal(readingDates(reading({ startedOn: '2026-10-02', finishedOn: '2026-10-20' })), 'Du 2 oct. 2026 au 20 oct. 2026')
    assert.equal(readingDates(reading({})), 'Dates inconnues')
    assert.equal(readingDates(reading({ outcome: 'stopped', finishedOn: '2026-04-10' })), 'Arrêté le 10 avr. 2026')
    assert.equal(readingTitle(reading({}), 0, 2), '1re lecture · Lu')
    assert.equal(readingTitle(reading({ outcome: 'reading' }), 1, 2), '2e lecture · En cours')
    assert.equal(readingTitle(reading({}), 0, 1), 'Lu')
    assert.equal(seriesLabel(library[1]!, 6), 'Dune · tome 1 sur 6')
    assert.equal(seriesLabel(book({ seriesName: 'Blacksad', tome: 2.5 })), 'Blacksad · tome 2,5')
    assert.equal(cardMeta(library[1]!), '9/10 · Papier')
    assert.equal(stateDetail(book({ state: 'read', readCount: 2 })), 'Lu 2 fois')
    assert.equal(stateDetail(book({ state: 'notwanted' })), 'Claude ne le proposera plus')
  })

  it('reads genres, tomes and initials as typed', () => {
    assert.deepEqual(parseGenres(' SF, polar ; sf ,, Essai'), ['SF', 'polar', 'Essai'])
    assert.equal(parseTome('2,5'), 2.5)
    assert.equal(parseTome(''), null)
    assert.equal(parseTome('-1'), null)
    assert.equal(initial(' « Dune »'), 'D')
  })
})
