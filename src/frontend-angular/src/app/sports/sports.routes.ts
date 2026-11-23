import type { Routes } from '@angular/router'
import { ExercisesPageComponent } from './exercises-page.component'
import { ChallengesPageComponent, GoalsPageComponent, TemplatesPageComponent } from './library-pages.component'
import { RoutinesPageComponent } from './routines-page.component'
import { SportHistoryPageComponent } from './sport-history-page.component'
import { SportProgressPageComponent } from './sport-progress-page.component'
import { SportRoutinePageComponent } from './sport-routine-page.component'
import { SportGarminPageComponent } from './sport-garmin-page.component'
import { SportSessionPageComponent } from './sport-session-page.component'
import { SportTodayPageComponent } from './sport-today-page.component'
import { SportVo2MaxPageComponent } from './sport-vo2max-page.component'

/** Sport module pages (ADR 0013, ADR 0014, ADR 0015, ADR 0019): one page per element, loaded on demand from app.routes.ts. */
export const SPORT_ROUTES: Routes = [
  { path: '', component: SportTodayPageComponent },
  { path: 'routine/:id', component: SportRoutinePageComponent },
  { path: 'sessions', component: SportHistoryPageComponent },
  { path: 'session/:id', component: SportSessionPageComponent },
  { path: 'vo2max', component: SportVo2MaxPageComponent },
  { path: 'garmin', component: SportGarminPageComponent },
  { path: 'history', pathMatch: 'full', redirectTo: 'sessions' },
  { path: 'progress', component: SportProgressPageComponent },
  { path: 'routines', component: RoutinesPageComponent },
  { path: 'exercises', component: ExercisesPageComponent },
  { path: 'challenges', component: ChallengesPageComponent },
  { path: 'goals', component: GoalsPageComponent },
  { path: 'templates', component: TemplatesPageComponent },
  // The former settings page was split into the pages above.
  { path: 'settings', pathMatch: 'full', redirectTo: 'routines' },
]
