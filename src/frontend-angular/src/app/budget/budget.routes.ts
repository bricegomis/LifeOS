import type { Routes } from '@angular/router'
import { BudgetLinePageComponent } from './line-page.component'
import { BudgetLinesPageComponent } from './lines-page.component'
import { BudgetMonthPageComponent } from './month-page.component'
import { BudgetOperationsPageComponent } from './operations-page.component'
import { BudgetRulesPageComponent } from './rules-page.component'
import { BudgetTrendPageComponent } from './trend-page.component'

/** Budget module pages (ADR 0020), loaded on demand from app.routes.ts. */
export const BUDGET_ROUTES: Routes = [
  { path: '', component: BudgetMonthPageComponent },
  { path: 'operations', component: BudgetOperationsPageComponent },
  { path: 'lines', component: BudgetLinesPageComponent },
  { path: 'line/:id', component: BudgetLinePageComponent },
  { path: 'rules', component: BudgetRulesPageComponent },
  { path: 'trend', component: BudgetTrendPageComponent },
]
