import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatSelectModule } from '@angular/material/select'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent } from '@/app/shared/ui/list-page.component'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, ProductDto, StoreDto } from '../food.models'
import { PriceDialogComponent, type PriceDialogData } from './price-dialog.component'
import { ProductEditorComponent, type ProductEditorData } from './product-editor.component'
import { productRows } from './product-form'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

/** Products and their latest price per store (ADR 0010), one column per store; prices are typed on the computer. */
@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [SkeletonComponent, FormsModule, ListPageComponent, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule, RouterLink],
  template: `
    <app-list-page heading="Produits et prix" addLabel="Ajouter un produit" [(query)]="search" (add)="edit(null)">
      <button pageActions mat-stroked-button type="button" [disabled]="!products().length || !stores().length" (click)="recordPrice()">
        <mat-icon svgIcon="sell" />Relever un prix</button>
      @if (stores().length) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="store-filter">
          <mat-label>Magasin</mat-label>
          <mat-select [ngModel]="storeId()" (ngModelChange)="storeId.set($event)">
            <mat-option value="">Tous les magasins</mat-option>
            @for (store of stores(); track store.id) { <mat-option [value]="store.id">{{ store.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
      }
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (loading()) {
        <app-skeleton />
      } @else if (!foods().length) {
        <p class="lo-empty">Un produit appartient à un aliment : crée d’abord tes aliments.</p>
      } @else if (!products().length) {
        <p class="lo-empty">Aucun produit pour le moment. Ajoutes-en avec « Ajouter un produit ».</p>
      } @else if (!rows().length) {
        <p class="lo-empty">Aucun produit ne correspond à ce filtre.</p>
      } @else {
        @if (!stores().length) { <p class="lo-notice">Ajoute un magasin pour relever des prix.</p> }
        <div class="lo-table">
          <div class="lo-table-head" [style.grid-template-columns]="gridColumns()" [style.min-width]="minWidth()" aria-hidden="true">
            <span>Aliment</span><span>Produit</span><span>Conditionnement</span>
            @for (store of stores(); track store.id) { <span>{{ store.name }}</span> }
          </div>
          @for (row of rows(); track row.product.id) {
            <div class="lo-table-row" [style.grid-template-columns]="gridColumns()" [style.min-width]="minWidth()">
              <span><a class="cell-link" [routerLink]="['/foods', row.product.foodItemId]" [queryParams]="{ from: 'products' }">{{ row.foodName }}</a></span>
              <span><button type="button" class="cell-link product-name" (click)="edit(row.product)">{{ row.product.name }}</button></span>
              <span>{{ row.pack }}</span>
              @for (cell of row.cells; track $index) {
                @if (cell) {
                  <span class="price-cell"><span class="price-value">{{ cell.price }}</span><span class="lo-muted lo-small">{{ cell.detail }}</span></span>
                } @else { <span class="lo-muted">—</span> }
              }
            </div>
          }
        </div>
      }
    </app-list-page>
  `,
  styles: [`
    .store-filter { max-width: 320px; }
    .cell-link { padding: 4px 0; border: 0; background: transparent; color: var(--mat-sys-primary); font: inherit; font-weight: 600; text-align: left; }
    .cell-link:hover { text-decoration: underline; }
    .product-name { color: var(--mat-sys-on-surface); font-weight: 500; }
    .price-cell { display: flex; flex-direction: column; }
    .price-value { font-weight: 600; }
  `],
})
export class ProductsPageComponent implements OnInit {
  private readonly api = inject(FoodApiService)
  private readonly dialog = inject(MatDialog)
  readonly products = signal<ProductDto[]>([])
  readonly foods = signal<FoodDto[]>([])
  readonly stores = signal<StoreDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  readonly search = signal('')
  readonly storeId = signal('')
  readonly rows = computed(() => productRows(this.products(), this.foods(), this.stores(), this.search(), this.storeId()))
  readonly gridColumns = computed(() => `1fr 1.5fr 1.1fr repeat(${this.stores().length}, 1fr)`)
  readonly minWidth = computed(() => `${540 + this.stores().length * 160}px`)

  async ngOnInit(): Promise<void> {
    try {
      const [products, foods, stores] = await Promise.all([
        firstValueFrom(this.api.products()), firstValueFrom(this.api.foods()), firstValueFrom(this.api.stores()),
      ])
      this.products.set(products)
      this.foods.set(foods)
      this.stores.set(stores)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  async edit(product: ProductDto | null): Promise<void> {
    if (!this.foods().length) return
    await firstValueFrom(openEditor<ProductEditorComponent, ProductEditorData>(this.dialog, ProductEditorComponent,
      { product, foods: this.foods(), stores: this.stores() }).afterClosed())
    await this.reload()
  }

  async recordPrice(): Promise<void> {
    await firstValueFrom(openEditor<PriceDialogComponent, PriceDialogData>(this.dialog, PriceDialogComponent,
      { products: this.products(), foods: this.foods(), stores: this.stores() }).afterClosed())
    await this.reload()
  }

  /** A window may have saved, archived or removed prices: the table is read again. */
  private async reload(): Promise<void> {
    try { this.products.set(await firstValueFrom(this.api.products())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
