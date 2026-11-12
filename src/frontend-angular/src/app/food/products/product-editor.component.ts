import { Component, inject, input, OnInit, output, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, ProductDto, StoreDto } from '../food.models'
import { formatDate, formatEuro, formatQuantity } from '../shared/format'
import { buildProductRequest, PACKAGE_UNITS, type ProductFormValues } from './product-form'

let nextEditorId = 0

/** Creates or edits a product; in edition it also lists its price history, where a mistyped price can be removed. */
@Component({
  selector: 'app-product-editor',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent],
  template: `
    <app-lifeos-dialog [open]="true" [heading]="product() ? 'Modifier le produit' : 'Nouveau produit'" [wide]="true"
      [formId]="formId" [submitLabel]="product() ? 'Enregistrer' : 'Créer le produit'" [busy]="busy()" [dirty]="dirty()"
      [error]="error()" (dismissed)="closed.emit()"
      description="Un produit n’existe que complet : aliment, nom et contenance sont obligatoires.">
      <form [id]="formId" class="editor-form" (ngSubmit)="save()">
        <label class="field">
          <span>Aliment</span>
          <select class="lifeos-input" name="food" required [(ngModel)]="values.foodItemId">
            <option value="">Choisir un aliment</option>
            @for (food of foods(); track food.id) {
              @if (!food.isArchived || food.id === values.foodItemId) { <option [value]="food.id">{{ food.name }}</option> }
            }
          </select>
        </label>
        <label class="field">
          <span>Nom du produit</span>
          <input class="lifeos-input" name="name" maxlength="200" required placeholder="Lentilles corail 500 g" [(ngModel)]="values.name" />
        </label>
        <div class="field-row">
          <label class="field">
            <span>Contenance</span>
            <input class="lifeos-input" name="quantity" inputmode="decimal" required [(ngModel)]="values.packageQuantity" />
          </label>
          <label class="field">
            <span>Unité</span>
            <select class="lifeos-input" name="unit" [(ngModel)]="values.packageUnit">
              @for (unit of units; track unit) { <option [value]="unit">{{ unit }}</option> }
            </select>
          </label>
          <label class="field">
            <span>Marque (facultatif)</span>
            <input class="lifeos-input" name="brand" maxlength="100" [(ngModel)]="values.brand" />
          </label>
          <label class="field">
            <span>Code-barres (facultatif)</span>
            <input class="lifeos-input" name="barcode" maxlength="50" [(ngModel)]="values.barcode" />
          </label>
        </div>
        <p class="muted small">Vendu au poids : 1 kg. Les prix déjà relevés gardent la contenance de leur relevé.</p>
      </form>
      @if (product(); as product) {
        <section class="history">
          <h3>Prix relevés</h3>
          @for (price of product.prices; track price.id) {
            <div class="history-row">
              <span>{{ storeName(price.storeId) }} · {{ formatDate(price.observedOn) }}</span>
              <span>{{ formatEuro(price.price) }} pour {{ formatQuantity(price.packageQuantity, price.packageUnit) }} · {{ formatEuro(price.unitPrice) }}/{{ price.unitLabel }}</span>
              <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="busy()" (click)="removePrice(price.id)">Supprimer</button>
            </div>
          } @empty { <p class="muted">Aucun prix relevé.</p> }
        </section>
      }
      @if (product()) {
        <button dialogExtraActions type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="archive()">Archiver</button>
      }
    </app-lifeos-dialog>
  `,
  styles: [`
    .editor-form { display: flex; flex-direction: column; gap: 16px; }
    .history { margin-top: 20px; }
    .history h3 { margin-bottom: 8px; font-size: 15px; }
    .history-row { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; padding: 8px 0; border-top: 1px solid var(--lifeos-line); }
  `],
})
export class ProductEditorComponent implements OnInit {
  private readonly api = inject(FoodApiService)
  readonly product = input.required<ProductDto | null>()
  readonly foods = input.required<FoodDto[]>()
  readonly stores = input.required<StoreDto[]>()
  /** Pre-selected food when created from a food sheet. */
  readonly foodId = input('')
  readonly saved = output<ProductDto>()
  /** A price was removed; the dialog stays open. */
  readonly changed = output<ProductDto>()
  readonly archived = output<string>()
  readonly closed = output<void>()
  readonly busy = signal(false)
  readonly error = signal('')
  readonly formId = `product-editor-${++nextEditorId}`
  readonly units = PACKAGE_UNITS
  readonly formatDate = formatDate
  readonly formatEuro = formatEuro
  readonly formatQuantity = formatQuantity
  values: ProductFormValues = { foodItemId: '', name: '', packageQuantity: '', packageUnit: 'g', brand: '', barcode: '' }
  private initial = formSnapshot(this.values)

  ngOnInit(): void {
    const product = this.product()
    this.values = product
      ? {
          foodItemId: product.foodItemId, name: product.name, packageQuantity: String(product.packageQuantity).replace('.', ','),
          packageUnit: product.packageUnit, brand: product.brand ?? '', barcode: product.barcode ?? '',
        }
      : { ...this.values, foodItemId: this.foodId() }
    this.initial = formSnapshot(this.values)
  }

  dirty(): boolean { return hasChanges(this.initial, this.values) }
  storeName(id: string): string { return this.stores().find(store => store.id === id)?.name ?? 'Magasin supprimé' }

  async save(): Promise<void> {
    const request = buildProductRequest(this.values)
    if (!request.ok) { this.error.set(request.error); return }
    const product = this.product()
    await this.run(async () => {
      const result = product
        ? await firstValueFrom(this.api.updateProduct(product.id, request.value))
        : await firstValueFrom(this.api.createProduct(request.value))
      this.initial = formSnapshot(this.values)
      this.saved.emit(result)
    })
  }

  async removePrice(priceId: string): Promise<void> {
    const product = this.product()
    if (!product || !window.confirm('Supprimer ce prix relevé ?')) return
    await this.run(async () => this.changed.emit(await firstValueFrom(this.api.removePrice(product.id, priceId))))
  }

  async archive(): Promise<void> {
    const product = this.product()
    if (!product || !window.confirm(`Archiver « ${product.name} » ? Ses prix ne comptent plus dans les coûts.`)) return
    await this.run(async () => {
      await firstValueFrom(this.api.archiveProduct(product.id))
      this.archived.emit(product.id)
    })
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true)
    this.error.set('')
    try { await action() } catch (error) { this.error.set(apiErrorMessage(error)) } finally { this.busy.set(false) }
  }
}
