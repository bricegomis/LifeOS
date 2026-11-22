import { Component, computed, inject, input, OnChanges, output, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
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
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <form class="lo-form" (ngSubmit)="record()">
      <mat-form-field appearance="outline">
        <mat-label>Produit</mat-label>
        <mat-select name="product" required [(ngModel)]="values.productId">
          @for (option of options(); track option.id) { <mat-option [value]="option.id">{{ option.label }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <div class="lo-form-row">
        <mat-form-field appearance="outline">
          <mat-label>Magasin</mat-label>
          <mat-select name="store" required [(ngModel)]="values.storeId">
            @for (store of stores(); track store.id) { <mat-option [value]="store.id">{{ store.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Date</mat-label>
          <input matInput name="date" type="date" required [max]="maxDate" [(ngModel)]="values.observedOn" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Prix payé (€)</mat-label>
          <input matInput name="price" inputmode="decimal" placeholder="0,00" required [(ngModel)]="values.price" />
        </mat-form-field>
      </div>
      <p class="lo-help">Prix d’un conditionnement. Le prix au kilo, au litre ou à la pièce se calcule depuis sa contenance.</p>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (lastRecorded()) { <p class="lo-muted" role="status">{{ lastRecorded() }}</p> }
      <div class="lo-actions">
        <button mat-flat-button type="submit" [disabled]="busy()"><mat-icon svgIcon="sell" />{{ busy() ? 'Enregistrement…' : 'Ajouter le prix' }}</button>
        <ng-content />
      </div>
    </form>
  `,
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
    if (this.busy()) return
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
