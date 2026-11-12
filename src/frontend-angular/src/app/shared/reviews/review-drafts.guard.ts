import { inject } from '@angular/core'
import type { CanDeactivateFn } from '@angular/router'
import { ReviewDraftsService } from './review-drafts.service'

/** Asks before leaving a page that hosts an unsaved household review. */
export const protectReviewDrafts: CanDeactivateFn<unknown> = () => inject(ReviewDraftsService).canLeave()
