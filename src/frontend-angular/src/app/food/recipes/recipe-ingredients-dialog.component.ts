import { Component, computed, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import type { FoodDto, FoodFamily, FoodRole, IngredientUnit } from '../food.models'
import {
  currentMonth, familyLabel, familyOptions, MONTH_NAMES, ROLE_LABELS, roleOptions, seasonStatus, seasonTagClass, sortBySeason,
} from '../shared/classification'
import { FoodImageComponent } from '../shared/food-image.component'
import { matches } from '../shared/format'
import { buildIngredientRequest, INGREDIENT_UNITS } from './recipe-form'
import type { RecipeDto } from './recipe.models'
import { RecipesApiService } from './recipes-api.service'

export interface RecipeIngredientsData {
  recipe: RecipeDto
  foods: FoodDto[]
  /** Called after each saved change, so the page shows it at once. */
  changed: (recipe: RecipeDto) => void
}

interface IngredientDraft { id: string; quantity: string; unit: IngredientUnit }

/** Ingredients of a recipe: every row saves itself; foods are picked in season first (ADR 0012). */
@Component({
  selector: 'app-recipe-ingredients-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FoodImageComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame heading="Ingrédients" cancelLabel="Fermer" [busy]="busy()" [error]="error()">
      <p class="lo-help">Chaque ligne s’enregistre aussitôt. Quantités en g, ml ou pièce.</p>
      @if (drafts.length) {
        <div class="lines">
          @for (draft of drafts; track draft.id) {
            <div class="line">
              <span class="line-name">{{ ingredientName(draft.id) }}</span>
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="quantity">
                <mat-label>Quantité</mat-label>
                <input matInput inputmode="decimal" [name]="'q-' + draft.id" [(ngModel)]="draft.quantity" (change)="updateIngredient(draft)" />
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="unit">
                <mat-label>Unité</mat-label>
                <mat-select [name]="'u-' + draft.id" [(ngModel)]="draft.unit" (selectionChange)="updateIngredient(draft)">
                  @for (unit of units; track unit) { <mat-option [value]="unit">{{ unit }}</mat-option> }
                </mat-select>
              </mat-form-field>
              <button mat-icon-button type="button" [disabled]="busy()" (click)="removeIngredient(draft)"
                [attr.aria-label]="'Retirer ' + ingredientName(draft.id)" title="Retirer"><mat-icon svgIcon="delete" /></button>
            </div>
          }
        </div>
      }

      <section class="picker" aria-labelledby="picker-title">
        <h3 id="picker-title">Ajouter un ingrédient</h3>
        <div class="picker-filters">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
            <mat-label>Chercher un aliment</mat-label>
            <mat-icon matPrefix svgIcon="search" />
            <input matInput type="search" [ngModel]="pickSearch()" (ngModelChange)="pickSearch.set($event)" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Famille</mat-label>
            <mat-select [ngModel]="pickFamily()" (ngModelChange)="pickFamily.set($event)">
              <mat-option value="">Toutes les familles</mat-option>
              @for (option of familyOptions; track option[0]) { <mat-option [value]="option[0]">{{ option[1] }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Rôle</mat-label>
            <mat-select [ngModel]="pickRole()" (ngModelChange)="pickRole.set($event)">
              <mat-option value="">Tous les rôles</mat-option>
              @for (option of roleOptions; track option[0]) { <mat-option [value]="option[0]">{{ option[1] }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <div class="lo-chips">
          <button type="button" class="lo-filter-chip" [class.is-on]="pickSeasonFirst()" [attr.aria-pressed]="pickSeasonFirst()"
            (click)="pickSeasonFirst.set(!pickSeasonFirst())">@if (pickSeasonFirst()) { <mat-icon svgIcon="check" /> }De saison d’abord</button>
          <span class="lo-help">{{ pickHint() }}</span>
        </div>
        <div class="lo-list picker-list">
          @for (food of pickerFoods(); track food.id) {
            @let status = season(food);
            <div class="lo-row" [class.is-picked]="picked.foodItemId === food.id">
              <button type="button" class="lo-row-main" [attr.aria-pressed]="picked.foodItemId === food.id" (click)="choose(food)">
                <app-food-image [path]="'/foods/' + food.id + '/image'" [hasImage]="food.hasImage" [version]="food.updatedAt"
                  [name]="food.name" width="40px" height="40px" />
                <span class="lo-row-text"><span class="lo-row-title">{{ food.name }}</span><span class="lo-row-sub">{{ pickDetail(food) }}</span></span>
                <span class="lo-tag" [class]="seasonTagClass(status.kind)">{{ status.label }}</span>
              </button>
            </div>
          } @empty {
            <p class="lo-empty">{{ activeFoods().length ? 'Aucun aliment ne correspond.' : 'Créez d’abord des aliments dans la page Aliments.' }}</p>
          }
        </div>
        <form class="add-row" (ngSubmit)="addIngredient()">
          <div class="lo-field-group picked">
            <span class="lo-field-label">Aliment choisi</span>
            <strong>{{ picked.foodItemId ? (food(picked.foodItemId)?.name ?? '') : 'Choisissez un aliment ci-dessus' }}</strong>
          </div>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="quantity">
            <mat-label>Quantité</mat-label>
            <input matInput name="quantity" inputmode="decimal" [(ngModel)]="picked.quantity" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="unit">
            <mat-label>Unité</mat-label>
            <mat-select name="unit" [(ngModel)]="picked.unit">
              @for (unit of units; track unit) { <mat-option [value]="unit">{{ unit }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <button mat-flat-button type="submit" [disabled]="busy()"><mat-icon svgIcon="add" />Ajouter</button>
        </form>
      </section>
    </app-editor-frame>
  `,
  styles: [`
    .lines { display: flex; flex-direction: column; gap: 16px; }
    .line, .add-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .line-name, .picked { min-width: 0; flex: 1 1 160px; }
    .line-name { font-weight: 500; }
    .quantity { width: 120px; }
    .unit { width: 110px; }
    .picker { display: flex; flex-direction: column; gap: 12px; padding: 16px; border-radius: 12px; background: var(--mat-sys-surface-container-low); }
    .picker h3 { font: var(--mat-sys-title-small); }
    .picker-filters { display: flex; flex-wrap: wrap; gap: 8px; }
    .picker-filters mat-form-field { flex: 1 1 160px; }
    .picker-filters .search { flex-basis: 220px; }
    .picker-list { max-height: 300px; overflow-y: auto; }
    .picker-list .lo-row-main { min-height: 56px; }
    .lo-row.is-picked { background: var(--mat-sys-secondary-container); }
  `],
})
export class RecipeIngredientsDialogComponent {
  private readonly api = inject(RecipesApiService)
  private readonly data = inject<RecipeIngredientsData>(MAT_DIALOG_DATA)
  readonly recipe = signal(this.data.recipe)
  readonly foods = this.data.foods
  readonly busy = signal(false)
  readonly error = signal('')
  drafts: IngredientDraft[] = this.data.recipe.ingredients.map(i => ({ id: i.id, quantity: String(i.quantity).replace('.', ','), unit: i.unit }))
  picked = { foodItemId: '', quantity: '', unit: 'g' as IngredientUnit }

  readonly units = INGREDIENT_UNITS
  readonly familyOptions = familyOptions
  readonly roleOptions = roleOptions
  readonly seasonTagClass = seasonTagClass
  readonly month = currentMonth()
  readonly activeFoods = computed(() => this.foods.filter(food => !food.isArchived))

  /** Search and filters, in season first unless asked otherwise. Nothing is hidden by season. */
  readonly pickSearch = signal('')
  readonly pickFamily = signal<FoodFamily | ''>('')
  readonly pickRole = signal<FoodRole | ''>('')
  readonly pickSeasonFirst = signal(true)
  readonly pickHint = computed(() => this.pickSeasonFirst()
    ? `De saison en ${MONTH_NAMES[this.month - 1]}, puis toute l’année, puis hors saison. Rien n’est masqué.`
    : 'Ordre alphabétique.')
  readonly pickerFoods = computed(() => {
    const used = new Set(this.recipe().ingredients.map(i => i.foodItemId))
    const family = this.pickFamily()
    const role = this.pickRole()
    const found = this.activeFoods().filter(food => !used.has(food.id) && matches(food.name, this.pickSearch())
      && (!family || food.family === family) && (!role || food.roles.includes(role)))
    return this.pickSeasonFirst() ? sortBySeason(found, this.month) : [...found].sort((a, b) => a.name.localeCompare(b.name, 'fr'))
  })

  food(id: string): FoodDto | undefined { return this.foods.find(food => food.id === id) }
  season(food: FoodDto) { return seasonStatus(food.seasonMonths, this.month) }

  pickDetail(food: FoodDto): string {
    return [familyLabel(food.family), food.roles.map(role => ROLE_LABELS[role]).join(', ')].filter(Boolean).join(' · ')
  }

  ingredientName(id: string): string {
    const ingredient = this.recipe().ingredients.find(i => i.id === id)
    return ingredient ? this.food(ingredient.foodItemId)?.name ?? 'Aliment indisponible' : ''
  }

  /** Picks the food and the unit matching its nutrition base; the quantity is still typed by hand. */
  choose(food: FoodDto): void {
    const unit: IngredientUnit = food.referenceUnit === '100 ml' ? 'ml' : food.referenceUnit === '1 pièce' ? 'pièce' : 'g'
    this.picked = { ...this.picked, foodItemId: food.id, unit }
    this.error.set('')
  }

  async addIngredient(): Promise<void> {
    const recipe = this.recipe()
    const request = buildIngredientRequest(this.picked.quantity, this.picked.unit)
    if (!this.picked.foodItemId) { this.error.set('Choisissez un aliment.'); return }
    if (!request.ok) { this.error.set(request.error); return }
    await this.action(async () => {
      const updated = await firstValueFrom(this.api.addIngredient(recipe.id, { foodItemId: this.picked.foodItemId, ...request.value }))
      const added = updated.ingredients.find(i => !this.drafts.some(d => d.id === i.id))
      if (added) this.drafts.push({ id: added.id, quantity: String(added.quantity).replace('.', ','), unit: added.unit })
      this.picked = { foodItemId: '', quantity: '', unit: 'g' }
      return updated
    })
  }

  async updateIngredient(draft: IngredientDraft): Promise<void> {
    const request = buildIngredientRequest(draft.quantity, draft.unit)
    if (!request.ok) { this.error.set(request.error); return }
    await this.action(() => firstValueFrom(this.api.updateIngredient(this.recipe().id, draft.id, request.value)))
  }

  async removeIngredient(draft: IngredientDraft): Promise<void> {
    await this.action(async () => {
      const updated = await firstValueFrom(this.api.removeIngredient(this.recipe().id, draft.id))
      this.drafts = this.drafts.filter(d => d.id !== draft.id)
      return updated
    })
  }

  private async action(run: () => Promise<RecipeDto>): Promise<void> {
    this.busy.set(true)
    this.error.set('')
    try {
      const recipe = await run()
      this.recipe.set(recipe)
      this.data.changed(recipe)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }
}
