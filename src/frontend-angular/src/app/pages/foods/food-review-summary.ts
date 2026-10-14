import type { ReviewDto } from '../../core/api/api.models'

export interface FoodReviewSummary {
  count: number
  scoredCount: number
  average: number | null
}

export function summarizeFoodReviews(reviews: ReviewDto[]): FoodReviewSummary {
  const scores = reviews.flatMap(review => review.score === null ? [] : [review.score])
  return {
    count: reviews.length,
    scoredCount: scores.length,
    average: scores.length ? scores.reduce((total, score) => total + score, 0) / scores.length : null,
  }
}
