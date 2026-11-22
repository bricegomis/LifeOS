import type { Routes } from '@angular/router'
import { RoutinePageComponent } from './routine-page.component'
import { RoutinesPageComponent } from './routines-page.component'
import { TasksAllPageComponent } from './tasks-all-page.component'
import { TasksTodayPageComponent } from './tasks-today-page.component'
import { TodoPageComponent } from './todo-page.component'

/** Tasks module pages (ADR 0017), loaded on demand from app.routes.ts. */
export const TASK_ROUTES: Routes = [
  { path: '', component: TasksTodayPageComponent },
  { path: 'all', component: TasksAllPageComponent },
  { path: 'todo/:id', component: TodoPageComponent },
  { path: 'routines', component: RoutinesPageComponent },
  { path: 'routine/:id', component: RoutinePageComponent },
]
