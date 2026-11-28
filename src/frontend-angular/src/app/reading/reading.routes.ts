import type { Routes } from '@angular/router'
import { BookPageComponent } from './book-page.component'
import { LibraryPageComponent } from './library-page.component'
import { SeriesListPageComponent } from './series-list-page.component'
import { SeriesPageComponent } from './series-page.component'

/** Reading module pages (ADR 0022), loaded on demand from app.routes.ts. */
export const READING_ROUTES: Routes = [
  { path: '', component: LibraryPageComponent },
  { path: 'book/:id', component: BookPageComponent },
  { path: 'series', component: SeriesListPageComponent },
  { path: 'series/:id', component: SeriesPageComponent },
]
