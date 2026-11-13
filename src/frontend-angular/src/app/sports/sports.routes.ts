import type { Routes } from '@angular/router'
import { SportHistoryPageComponent } from './sport-history-page.component'
import { SportProgressPageComponent } from './sport-progress-page.component'
import { SportSettingsPageComponent } from './sport-settings-page.component'
import { SportTodayPageComponent } from './sport-today-page.component'

/** Sport module pages (ADR 0013), loaded on demand from app.routes.ts. */
export const SPORT_ROUTES: Routes = [
  { path: '', component: SportTodayPageComponent },
  { path: 'history', component: SportHistoryPageComponent },
  { path: 'progress', component: SportProgressPageComponent },
  { path: 'settings', component: SportSettingsPageComponent },
]
