import { Component, computed, inject, input, OnChanges, output, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, ProductDto, StoreDto } from '../food.models'
import { packageLabel, today } from '../shared/format'
import { buildPriceRequest, type PriceFormValues } from './product-form'

/**
 * Records the price paid for one package. On the ticket screen the store and date come from the
 * receipt (`storeId`, `observedOn` inputs) and stay editable.
 */
@Component({
  selector: 'app-price-form',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form class="price-form" (ngSubmit)="record()">
      <label class="field">
        <span>Produit</span>
        <select class="lifeos-input" name="product" required [(ngModel)]="values.productId">
          <option value="">Choisir un produit</option>
          @for (option of options(); track option.id) { <option [value]="option.id">{{ option.label }}</option> }
        </select>
      </label>
      <div class="field-row">
        <label class="field">
          <span>Magasin</span>
          <select class="lifeos-input" name="store" required [(ngModel)]="values.storeId">
            <option value="">Choisir</option>
            @for (store of stores(); track store.id) { <option [value]="store.id">{{ store.name }}</option> }
          </select>
        </label>
        <label class="field">
          <span>Date</span>
          <input class="lifeos-input" name="date" type="date" required [max]="maxDate" [(ngModel)]="values.observedOn" />
        </label>
        <label class="field">
          <span>Prix payé (€)</span>
          <input class="lifeos-input" name="price" inputmode="decimal" placeholder="0,00" required [(ngModel)]="values.price" />
        </label>
      </div>
      <p class="muted small">Prix d’un conditionnement. Le prix au kilo, au litre ou à la pièce se calcule depuis sa contenance.</p>
      <div class="page-actions">
        <button type="submit" class="lifeos-button" [disabled]="busy()">{{ busy() ? 'Enregistrement…' : 'Ajouter le prix' }}</button>
        <ng-content />
      </div>
      @if (error()) { <p class="error-text" role="alert">{{ error() }}</p> }
      @if (lastRecorded()) { <p class="muted" role="status">{{ lastRecorded() }}</p> }
    </form>
  `,
  styles: [`.price-form { display: flex; flex-direction: column; gap: 12px; }`],
})
export class PriceFormComponent implements OnChanges {
  private readonly api = inject(FoodApiService)
  readonly products = input.required<ProductDto[]>()
  readonly foods = input.required<FoodDto[]>()
  readonly stores = input.required<StoreDto[]>()
  readonly storeId = input<string | null>(null)
  readonly observedOn = input<string | null>(null)
  readonly recorded = output<ProductDto>()
  readonly busy = signal(false)
  readonly error = signal('')
  readonly lastRecorded = signal('')
  readonly maxDate = today()
  values: PriceFormValues = { productId: '', storeId: '', price: '', observedOn: today() }

  readonly options = computed(() => this.products().filter(product => !product.isArchived).map(product => ({
    id: product.id,
    label: `${this.foods().find(food => food.id === product.foodItemId)?.name ?? '?'} — ${product.name} (${packageLabel(product.packageQuantity, product.packageUnit)})`,
  })).sort((a, b) => a.label.localeCompare(b.label, 'fr')))

  ngOnChanges(): void {
    this.values = { ...this.values, storeId: this.storeId() ?? this.values.storeId, observedOn: this.observedOn() ?? this.values.observedOn }
  }

  async record(): Promise<void> {
    const request = buildPriceRequest(this.values, today())
    if (!request.ok) { this.error.set(request.error); return }
    this.busy.set(true)
    this.error.set('')
    try {
      const product = await firstValueFrom(this.api.recordPrice(this.values.productId, request.value))
      this.lastRecorded.set(`Prix enregistré pour « ${product.name} ».`)
      this.values = { ...this.values, productId: '', price: '' }
      this.recorded.emit(product)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }
}
