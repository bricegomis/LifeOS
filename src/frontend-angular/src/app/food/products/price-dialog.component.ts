import { Component, inject } from '@angular/core'
import { MAT_DIALOG_DATA } from '@angular/material/dialog'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import type { FoodDto, ProductDto, StoreDto } from '../food.models'
import { PriceFormComponent } from './price-form.component'

export interface PriceDialogData { products: ProductDto[], foods: FoodDto[], stores: StoreDto[] }

/** Several prices can be recorded in a row; each one is saved when added. */
@Component({
  selector: 'app-price-dialog',
  standalone: true,
  imports: [EditorFrameComponent, PriceFormComponent],
  template: `
    <app-editor-frame heading="Relever un prix" cancelLabel="Fermer">
      <app-price-form [products]="data.products" [foods]="data.foods" [stores]="data.stores" />
    </app-editor-frame>
  `,
})
export class PriceDialogComponent {
  readonly data = inject<PriceDialogData>(MAT_DIALOG_DATA)
}
