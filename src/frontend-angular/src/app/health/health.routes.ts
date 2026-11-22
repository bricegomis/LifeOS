import type { Routes } from '@angular/router'
import { AnalysesPageComponent } from './analyses-page.component'
import { AnalysisPageComponent } from './analysis-page.component'
import { AppointmentsPageComponent } from './appointments-page.component'
import { DocumentPageComponent } from './document-page.component'
import { DocumentsPageComponent } from './documents-page.component'
import { HealthTodayPageComponent } from './health-today-page.component'
import { MarkerPageComponent } from './marker-page.component'
import { MeasuresPageComponent } from './measures-page.component'
import { SupplementPageComponent } from './supplement-page.component'
import { SupplementsPageComponent } from './supplements-page.component'
import { SymptomsPageComponent } from './symptoms-page.component'

/** Health module pages (ADR 0018), loaded on demand from app.routes.ts. */
export const HEALTH_ROUTES: Routes = [
  { path: '', component: HealthTodayPageComponent },
  { path: 'supplements', component: SupplementsPageComponent },
  { path: 'supplement/:id', component: SupplementPageComponent },
  { path: 'symptoms', component: SymptomsPageComponent },
  { path: 'analyses', component: AnalysesPageComponent },
  { path: 'marker/:id', component: MarkerPageComponent },
  { path: 'analysis/:id', component: AnalysisPageComponent },
  { path: 'measures', component: MeasuresPageComponent },
  { path: 'documents', component: DocumentsPageComponent },
  { path: 'document/:id', component: DocumentPageComponent },
  { path: 'appointments', component: AppointmentsPageComponent },
]
