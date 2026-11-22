import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatDialog } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatSelectModule } from '@angular/material/select'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent } from '@/app/shared/ui/list-page.component'
import { FoodApiService } from '../food-api.service'
import type { Conservation, FoodDto, FoodFamily, FoodRole } from '../food.models'
import {
  conservationOptions, currentMonth, familyLabel, familyOptions, filterFoods, type FoodListFilter, householdTags,
  MONTH_NAMES, roleOptions, type SeasonKind, seasonStatus, seasonTagClass,
} from '../shared/classification'
import { FoodImageComponent } from '../shared/food-image.component'
import { fodmapLabel, formatNumber } from '../shared/format'
import { NewFoodDialogComponent } from './new-food-dialog.component'

/** Food library (ADR 0010, ADR 0012): filters by state, classification, season and tag; each food opens its sheet. */
@Component({
  selector: 'app-foods-page',
  standalone: true,
  imports: [FoodImageComponent, FormsModule, ListPageComponent, MatFormFieldModule, MatIconModule, MatSelectModule, RouterLink],
  template: `
    <app-list-page heading="Aliments" addLabel="Ajouter un aliment" [(query)]="search" (add)="create()">
      <div class="filters">
        <div class="lo-chips" role="group" aria-label="État des fiches">
          <button type="button" class="lo-filter-chip" [class.is-on]="!todo()" [attr.aria-pressed]="!todo()" (click)="todo.set(false)">
            @if (!todo()) { <mat-icon svgIcon="check" /> }Tous ({{ activeCount() }})</button>
          <button type="button" class="lo-filter-chip" [class.is-on]="todo()" [attr.aria-pressed]="todo()" (click)="todo.set(true)">
            @if (todo()) { <mat-icon svgIcon="check" /> }À compléter ({{ todoCount() }})</button>
        </div>
        <div class="selects">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Famille</mat-label>
            <mat-select [ngModel]="family()" (ngModelChange)="family.set($event)">
              <mat-option value="">Toutes les familles</mat-option>
              @for (option of familyOptions; track option[0]) { <mat-option [value]="option[0]">{{ option[1] }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Rôle</mat-label>
            <mat-select [ngModel]="role()" (ngModelChange)="role.set($event)">
              <mat-option value="">Tous les rôles</mat-option>
              @for (option of roleOptions; track option[0]) { <mat-option [value]="option[0]">{{ option[1] }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Conservation</mat-label>
            <mat-select [ngModel]="conservation()" (ngModelChange)="conservation.set($event)">
              <mat-option value="">Toute conservation</mat-option>
              @for (option of conservationOptions; track option[0]) { <mat-option [value]="option[0]">{{ option[1] }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Saison</mat-label>
            <mat-select [ngModel]="season()" (ngModelChange)="season.set($event)">
              <mat-option value="">Toutes saisons</mat-option>
              <mat-option value="in">De saison en {{ monthName }}</mat-option>
              <mat-option value="all">Toute l’année</mat-option>
              <mat-option value="out">Hors saison</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        @if (tags().length) {
          <div class="lo-chips" role="group" aria-label="Étiquettes">
            <button type="button" class="lo-filter-chip" [class.is-on]="!tag()" [attr.aria-pressed]="!tag()" (click)="tag.set('')">
              @if (!tag()) { <mat-icon svgIcon="check" /> }Toutes les étiquettes</button>
            @for (item of tags(); track item) {
              <button type="button" class="lo-filter-chip" [class.is-on]="tag() === item" [attr.aria-pressed]="tag() === item" (click)="tag.set(item)">
                @if (tag() === item) { <mat-icon svgIcon="check" /> }{{ item }}</button>
            }
          </div>
        }
      </div>

      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (loading()) {
        <p class="lo-status" role="status">Chargement des aliments…</p>
      } @else if (!foods().length) {
        <p class="lo-empty">Aucun aliment pour le moment. Ajoutez le premier avec « Ajouter un aliment ».</p>
      } @else if (!visible().length) {
        <p class="lo-empty">Aucun aliment ne correspond aux filtres.</p>
      } @else {
        <div class="lo-list">
          @for (food of visible(); track food.id) {
            @let status = season$(food);
            <div class="lo-row">
              <a class="lo-row-main" [routerLink]="['/foods', food.id]">
                <app-food-image [path]="'/foods/' + food.id + '/image'" [hasImage]="food.hasImage" [version]="food.updatedAt" [name]="food.name" />
                <span class="lo-row-text">
                  <span class="lo-row-title">{{ food.name }}</span>
                  <span class="lo-row-sub">{{ details(food) }}</span>
                  @if (food.tags.length) { <span class="lo-row-sub">{{ food.tags.join(' · ') }}</span> }
                </span>
                <span class="row-tags">
                  <span class="lo-tag" [class]="seasonTagClass(status.kind)">{{ status.label }}</span>
                  @if (food.missingFields.length) { <span class="lo-tag is-warn">À compléter</span> }
                  @else { <span class="lo-tag is-ok">Complet</span> }
                </span>
              </a>
            </div>
          }
        </div>
      }
    </app-list-page>
  `,
  styles: [`
    .filters { display: flex; flex-direction: column; gap: 12px; }
    .selects { display: flex; flex-wrap: wrap; gap: 8px; }
    .selects mat-form-field { flex: 1 1 180px; }
    .row-tags { display: flex; flex: 0 0 auto; flex-wrap: wrap; justify-content: flex-end; gap: 6px; }
    @media (max-width: 760px) { .row-tags { flex-direction: column; align-items: flex-end; } }
  `],
})
export class FoodsPageComponent implements OnInit {
  private readonly api = inject(FoodApiService)
  private readonly dialog = inject(MatDialog)
  private readonly router = inject(Router)
  readonly foods = signal<FoodDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  readonly search = signal('')
  readonly todo = signal(false)
  readonly family = signal<FoodFamily | ''>('')
  readonly role = signal<FoodRole | ''>('')
  readonly conservation = signal<Conservation | ''>('')
  readonly season = signal<SeasonKind | ''>('')
  readonly tag = signal('')
  readonly month = currentMonth()
  readonly monthName = MONTH_NAMES[this.month - 1]
  private readonly filter = computed<FoodListFilter>(() => ({
    search: this.search(), todo: this.todo(), family: this.family(), role: this.role(), conservation: this.conservation(),
    season: this.season(), tag: this.tag(), month: this.month,
  }))
  readonly visible = computed(() => filterFoods(this.foods(), this.filter()))
  readonly activeCount = computed(() => this.foods().filter(food => !food.isArchived).length)
  readonly todoCount = computed(() => this.foods().filter(food => !food.isArchived && food.missingFields.length > 0).length)
  readonly tags = computed(() => householdTags(this.foods().filter(food => !food.isArchived)))
  readonly familyOptions = familyOptions
  readonly roleOptions = roleOptions
  readonly conservationOptions = conservationOptions
  readonly seasonTagClass = seasonTagClass

  season$(food: FoodDto) { return seasonStatus(food.seasonMonths, this.month) }

  async ngOnInit(): Promise<void> {
    try {
      this.foods.set(await firstValueFrom(this.api.foods()))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  /** "Légume · 32 kcal · FODMAP bas · NOVA 1"; unknown values stay as "—". */
  details(food: FoodDto): string {
    const calories = food.nutrition?.caloriesPerUnit
    return [
      familyLabel(food.family),
      calories === null || calories === undefined ? '— kcal' : `${formatNumber(calories, 0)} kcal`,
      `FODMAP ${fodmapLabel(food.fodmap).toLocaleLowerCase('fr')}`,
      `NOVA ${food.nova ?? '—'}`,
    ].join(' · ')
  }

  async create(): Promise<void> {
    const food = await firstValueFrom(openEditor<NewFoodDialogComponent, null, FoodDto>(this.dialog, NewFoodDialogComponent, null).afterClosed())
    if (food) await this.router.navigate(['/foods', food.id])
  }
}
