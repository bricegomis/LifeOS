import { inject } from '@angular/core'
import { CanActivateFn, Router, Routes } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'
import { LoginPageComponent } from '@/app/login/login-page.component'
import { AppLayoutComponent } from '@/app/shell/app-layout.component'

const requireAuthentication: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService)
  const router = inject(Router)

  await auth.ensureReady()
  return auth.authenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { redirect: state.url } })
}

export const routes: Routes = [
  { path: 'login', component: LoginPageComponent },
  {
    path: '',
    component: AppLayoutComponent,
    canActivate: [requireAuthentication],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'recipes' },
      // Food module: recipes, foods, products and prices, stores, tickets.
      { path: '', loadChildren: () => import('@/app/food/food.routes').then(module => module.FOOD_ROUTES) },
      { path: 'sports', loadComponent: () => import('@/app/sports/sports-page.component').then(module => module.SportsPageComponent) },
      { path: 'settings', loadComponent: () => import('@/app/household/settings-page.component').then(module => module.SettingsPageComponent) },
      // Old links keep working.
      { path: 'articles', pathMatch: 'full', redirectTo: 'products' },
      { path: 'library', pathMatch: 'full', redirectTo: 'foods' },
      { path: 'planning', pathMatch: 'full', redirectTo: 'recipes' },
      { path: 'today', pathMatch: 'full', redirectTo: 'recipes' },
      { path: 'planner', pathMatch: 'full', redirectTo: 'recipes' },
    ],
  },
  { path: '**', redirectTo: '' },
]
