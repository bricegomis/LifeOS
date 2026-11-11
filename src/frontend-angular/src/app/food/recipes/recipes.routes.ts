import type { Routes } from '@angular/router'
import { protectReviewDrafts } from '@/app/shared/reviews/review-drafts.guard'
import { RecipesPageComponent } from './recipes-page.component'

/** Loaded on demand from app.routes.ts. */
export const RECIPES_ROUTES: Routes = [
  { path: '', component: RecipesPageComponent, canDeactivate: [protectReviewDrafts] },
]
