import { inject } from '@angular/core'
import { CanActivateFn, Router, Routes } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'
import { LastRouteService } from '@/app/core/navigation/last-route.service'
import { LoginPageComponent } from '@/app/login/login-page.component'
import { AppLayoutComponent } from '@/app/shell/app-layout.component'

const requireAuthentication: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService)
  const router = inject(Router)

  await auth.ensureReady()
  if (!auth.authenticated()) return router.createUrlTree(['/login'], { queryParams: { redirect: state.url } })
  // Back from the sign-in page: go where the user was heading; otherwise an installed app
  // reopened on its start address goes back to the page left last.
  const launchTarget = inject(LastRouteService).takeLaunchTarget()
  const returnPath = auth.takeReturnPath() ?? launchTarget
  return returnPath && returnPath !== '/' && returnPath !== state.url ? router.parseUrl(returnPath) : true
}

export const routes: Routes = [
  { path: 'login', component: LoginPageComponent },
  {
    path: '',
    component: AppLayoutComponent,
    canActivate: [requireAuthentication],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'today' },
      // The day at a glance, read from the Sport and Health endpoints (ADR 0019); the planner's future home.
      { path: 'today', loadComponent: () => import('@/app/today/today-page.component').then(module => module.TodayPageComponent) },
      // Food module: recipes, foods, products and prices, stores, tickets.
      { path: '', loadChildren: () => import('@/app/food/food.routes').then(module => module.FOOD_ROUTES) },
      // Sport module: today, history, progress, settings (ADR 0013).
      { path: 'sports', loadChildren: () => import('@/app/sports/sports.routes').then(module => module.SPORT_ROUTES) },
      // Tasks module: today, all tasks, one page per todo (ADR 0017).
      { path: 'tasks', loadChildren: () => import('@/app/tasks/tasks.routes').then(module => module.TASK_ROUTES) },
      // Health module: private to its account (ADR 0018).
      { path: 'health', loadChildren: () => import('@/app/health/health.routes').then(module => module.HEALTH_ROUTES) },
      // Budget module: private to its account, reads published contracts (ADR 0020).
      { path: 'budget', loadChildren: () => import('@/app/budget/budget.routes').then(module => module.BUDGET_ROUTES) },
      { path: 'settings', loadComponent: () => import('@/app/household/settings-page.component').then(module => module.SettingsPageComponent) },
      // Old links keep working.
      { path: 'articles', pathMatch: 'full', redirectTo: 'products' },
      { path: 'library', pathMatch: 'full', redirectTo: 'foods' },
      { path: 'planning', pathMatch: 'full', redirectTo: 'recipes' },
      { path: 'planner', pathMatch: 'full', redirectTo: 'recipes' },
    ],
  },
  { path: '**', redirectTo: '' },
]
