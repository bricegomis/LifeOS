import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, ProductDto, StoreDto } from '../food.models'
import { formatDate, formatEuro, formatQuantity } from '../shared/format'
import { buildProductRequest, PACKAGE_UNITS, type ProductFormValues } from './product-form'

export interface ProductEditorData {
  product: ProductDto | null
  foods: FoodDto[]
  stores: StoreDto[]
  /** Pre-selected food of a new product. */
  foodId?: string
}

/**
 * Creates or edits a product; in edition it also lists its price history, where a mistyped price can be removed.
 * Returns the saved product; the caller reloads its list after any close, since prices may have been removed.
 */
@Component({
  selector: 'app-product-editor',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame [heading]="product() ? 'Modifier le produit' : 'Nouveau produit'" formId="product-form"
      [saveLabel]="product() ? 'Enregistrer' : 'Créer'" [busy]="busy()" [dirty]="dirty()" [error]="error()"
      [dangerLabel]="product() ? 'Archiver' : ''" (danger)="archive()">
      <form id="product-form" class="lo-form" (ngSubmit)="save()">
        <p class="lo-help">Un produit n’existe que complet : aliment, nom et contenance sont obligatoires.</p>
        <mat-form-field appearance="outline">
          <mat-label>Aliment</mat-label>
          <mat-select name="food" required [(ngModel)]="values.foodItemId">
            @for (food of data.foods; track food.id) {
              @if (!food.isArchived || food.id === values.foodItemId) { <mat-option [value]="food.id">{{ food.name }}</mat-option> }
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Nom du produit</mat-label>
          <input matInput name="name" maxlength="200" required placeholder="Lentilles corail 500 g" [(ngModel)]="values.name" />
        </mat-form-field>
        <div class="lo-form-row">
          <mat-form-field appearance="outline">
            <mat-label>Contenance</mat-label>
            <input matInput name="quantity" inputmode="decimal" required [(ngModel)]="values.packageQuantity" />
            <mat-hint>Vendu au poids : 1 kg.</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Unité</mat-label>
            <mat-select name="unit" [(ngModel)]="values.packageUnit">
              @for (unit of units; track unit) { <mat-option [value]="unit">{{ unit }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <div class="lo-form-row">
          <mat-form-field appearance="outline">
            <mat-label>Marque (facultatif)</mat-label>
            <input matInput name="brand" maxlength="100" [(ngModel)]="values.brand" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Code-barres (facultatif)</mat-label>
            <input matInput name="barcode" maxlength="50" [(ngModel)]="values.barcode" />
          </mat-form-field>
        </div>
        <p class="lo-help">Les prix déjà relevés gardent la contenance de leur relevé.</p>
      </form>
      @if (product(); as product) {
        <section class="lo-section" aria-labelledby="product-prices">
          <h3 id="product-prices" class="history-title">Prix relevés</h3>
          @if (product.prices.length) {
            <div class="lo-list">
              @for (price of product.prices; track price.id) {
                <div class="lo-row">
                  <div class="lo-row-main">
                    <span class="lo-row-text">
                      <span class="lo-row-title">{{ formatEuro(price.price) }} pour {{ formatQuantity(price.packageQuantity, price.packageUnit) }}</span>
                      <span class="lo-row-sub">{{ storeName(price.storeId) }} · {{ formatDate(price.observedOn) }} · {{ formatEuro(price.unitPrice) }}/{{ price.unitLabel }}</span>
                    </span>
                  </div>
                  <span class="lo-row-action">
                    <button mat-icon-button type="button" [disabled]="busy()" (click)="removePrice(price.id)" aria-label="Supprimer ce prix" title="Supprimer ce prix">
                      <mat-icon svgIcon="delete" /></button>
                  </span>
                </div>
              }
            </div>
          } @else { <p class="lo-muted">Aucun prix relevé.</p> }
        </section>
      }
    </app-editor-frame>
  `,
  styles: [`.history-title { font: var(--mat-sys-title-small); }`],
})
export class ProductEditorComponent {
  private readonly api = inject(FoodApiService)
  private readonly ref = inject<MatDialogRef<ProductEditorComponent, ProductDto>>(MatDialogRef)
  readonly data = inject<ProductEditorData>(MAT_DIALOG_DATA)
  readonly product = signal(this.data.product)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly units = PACKAGE_UNITS
  readonly formatDate = formatDate
  readonly formatEuro = formatEuro
  readonly formatQuantity = formatQuantity
  readonly values: ProductFormValues = this.data.product
    ? {
        foodItemId: this.data.product.foodItemId, name: this.data.product.name,
        packageQuantity: String(this.data.product.packageQuantity).replace('.', ','), packageUnit: this.data.product.packageUnit,
        brand: this.data.product.brand ?? '', barcode: this.data.product.barcode ?? '',
      }
    : { foodItemId: this.data.foodId ?? '', name: '', packageQuantity: '', packageUnit: 'g', brand: '', barcode: '' }
  private readonly initial = formSnapshot(this.values)

  dirty(): boolean { return hasChanges(this.initial, this.values) }
  storeName(id: string): string { return this.data.stores.find(store => store.id === id)?.name ?? 'Magasin supprimé' }

  async save(): Promise<void> {
    const request = buildProductRequest(this.values)
    if (!request.ok) { this.error.set(request.error); return }
    const product = this.product()
    await this.run(async () => this.ref.close(product
      ? await firstValueFrom(this.api.updateProduct(product.id, request.value))
      : await firstValueFrom(this.api.createProduct(request.value))))
  }

  async removePrice(priceId: string): Promise<void> {
    const product = this.product()
    if (!product || !window.confirm('Supprimer ce prix relevé ?')) return
    await this.run(async () => this.product.set(await firstValueFrom(this.api.removePrice(product.id, priceId))))
  }

  /** Its prices no longer count in recipe costs. */
  async archive(): Promise<void> {
    const product = this.product()
    if (!product) return
    await this.run(async () => {
      await firstValueFrom(this.api.archiveProduct(product.id))
      this.ref.close()
    })
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy()) return
    this.busy.set(true)
    this.error.set('')
    try { await action() } catch (error) { this.error.set(apiErrorMessage(error)) } finally { this.busy.set(false) }
  }
}
