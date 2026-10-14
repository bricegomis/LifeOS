import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { ReviewDto } from '../../core/api/api.models.ts'
import { summarizeFoodReviews } from './food-review-summary.ts'

const rows = (...scores: (number | null)[]) => scores.map(score => ({ score })) as ReviewDto[]

describe('food review summary', () => {
  it('does not invent a rating for missing or comment-only reviews', () => {
    assert.deepEqual(summarizeFoodReviews([]), { count: 0, scoredCount: 0, average: null })
    assert.deepEqual(summarizeFoodReviews(rows(null, null)), { count: 2, scoredCount: 0, average: null })
  })

  it('averages scored reviews on the existing ten-point scale, including zero', () => {
    assert.deepEqual(summarizeFoodReviews(rows(0)), { count: 1, scoredCount: 1, average: 0 })
    assert.deepEqual(summarizeFoodReviews(rows(0, 7, 10, null)), { count: 4, scoredCount: 3, average: 17 / 3 })
  })
})
