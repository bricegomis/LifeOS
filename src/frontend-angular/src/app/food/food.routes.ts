import type { Routes } from '@angular/router'
import { protectReviewDrafts } from '@/app/shared/reviews/review-drafts.guard'
import { FoodPageComponent } from './foods/food-page.component'
import { FoodsPageComponent } from './foods/foods-page.component'
import { ProductsPageComponent } from './products/products-page.component'
import { RecipePageComponent } from './recipes/recipe-page.component'
import { RecipesPageComponent } from './recipes/recipes-page.component'
import { StoresPageComponent } from './stores/stores-page.component'
import { TicketsPageComponent } from './tickets/tickets-page.component'

/** Food module pages, loaded on demand from app.routes.ts. */
export const FOOD_ROUTES: Routes = [
  { path: 'recipes', component: RecipesPageComponent },
  { path: 'recipes/:id', component: RecipePageComponent, canDeactivate: [(page: RecipePageComponent) => page.canLeave()] },
  { path: 'foods', component: FoodsPageComponent },
  { path: 'foods/:id', component: FoodPageComponent, canDeactivate: [(page: FoodPageComponent) => page.canLeave()] },
  { path: 'products', component: ProductsPageComponent },
  { path: 'stores', component: StoresPageComponent },
  { path: 'tickets', component: TicketsPageComponent, canDeactivate: [(page: TicketsPageComponent) => page.canLeave(), protectReviewDrafts] },
]
