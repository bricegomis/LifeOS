import { inject } from '@angular/core'
import { CanActivateFn, Router, Routes } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'
import { DashboardPageComponent } from '@/app/pages/dashboard/dashboard-page.component'
import { FoodItemsPageComponent } from '@/app/pages/foods/food-items-page.component'
import { LoginPageComponent } from '@/app/pages/login/login-page.component'
import { ComposedMealsPageComponent } from '@/app/pages/meals/composed-meal-page.component'
import { PlanningPageComponent } from '@/app/pages/planning/planning-page.component'
import { RecipesPageComponent } from '@/app/pages/recipes/recipes-page.component'
import { SettingsPageComponent } from '@/app/pages/settings/settings-page.component'
import { TicketsPageComponent } from '@/app/pages/tickets/tickets-page.component'
import { StoresArticlesPageComponent } from '@/app/pages/stores/stores-articles-page.component'
import { AppLayoutComponent } from '@/app/shell/app-layout.component'
import { SportsPageComponent } from '@/app/pages/sports/sports-page.component'

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
      { path: '', pathMatch: 'full', component: DashboardPageComponent },
      { path: 'planning', component: PlanningPageComponent },
      { path: 'recipes', component: RecipesPageComponent },
      { path: 'meals', component: ComposedMealsPageComponent },
      { path: 'foods', component: FoodItemsPageComponent, canDeactivate: [(component: FoodItemsPageComponent) => component.canLeave()] },
      { path: 'products', pathMatch: 'full', redirectTo: 'foods' },
      { path: 'sports', component: SportsPageComponent },
      { path: 'stores', component: StoresArticlesPageComponent },
      { path: 'articles', pathMatch: 'full', redirectTo: 'foods' },
      { path: 'tickets', component: TicketsPageComponent, canDeactivate: [(component: TicketsPageComponent) => component.canLeave()] },
      { path: 'library', pathMatch: 'full', redirectTo: 'foods' },
      { path: 'settings', component: SettingsPageComponent },
      { path: 'today', pathMatch: 'full', redirectTo: '' },
      { path: 'planner', pathMatch: 'full', redirectTo: 'planning' },
    ],
  },
  { path: '**', redirectTo: '' },
]
