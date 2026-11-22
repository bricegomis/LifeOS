import type { Routes } from '@angular/router'
import { HealthTodayPageComponent } from './health-today-page.component'
import { SupplementPageComponent } from './supplement-page.component'
import { SupplementsPageComponent } from './supplements-page.component'

/** Health module pages (ADR 0018), loaded on demand from app.routes.ts. */
export const HEALTH_ROUTES: Routes = [
  { path: '', component: HealthTodayPageComponent },
  { path: 'supplements', component: SupplementsPageComponent },
  { path: 'supplement/:id', component: SupplementPageComponent },
]
