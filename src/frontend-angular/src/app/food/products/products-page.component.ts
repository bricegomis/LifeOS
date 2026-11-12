import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, ProductDto, StoreDto } from '../food.models'
import { PriceFormComponent } from './price-form.component'
import { ProductEditorComponent } from './product-editor.component'
import { productRows } from './product-form'

@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent, PriceFormComponent, ProductEditorComponent],
  templateUrl: './products-page.component.html',
  styleUrl: './products.css',
})
export class ProductsPageComponent implements OnInit {
  private readonly api = inject(FoodApiService)
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
  /** `undefined`: closed; `null`: new product. */
  readonly editing = signal<ProductDto | null | undefined>(undefined)
  readonly recordingPrice = signal(false)

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

  replace(product: ProductDto): void {
    this.products.update(list => list.some(p => p.id === product.id) ? list.map(p => p.id === product.id ? product : p) : [...list, product])
  }

  saved(product: ProductDto): void {
    this.replace(product)
    this.editing.set(undefined)
  }

  changed(product: ProductDto): void {
    this.replace(product)
    this.editing.set(product)
  }

  archived(id: string): void {
    this.products.update(list => list.filter(p => p.id !== id))
    this.editing.set(undefined)
  }
}
