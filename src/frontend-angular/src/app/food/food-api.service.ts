import { inject, Injectable } from '@angular/core'
import { from, map, Observable, switchMap } from 'rxjs'
import type { MemberProfileDto } from '@/app/core/api/api.models'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type {
  FoodDto, FoodRequest, PriceRequest, ProductDto, ProductRequest, ReceiptDto, StoreDto, StoreRequest, UpdateReceiptRequest,
} from './food.models'
import { LIBRARY_IMAGE, RECEIPT_PHOTO, reduceImage } from './shared/image-reduction'

const id = (value: string) => encodeURIComponent(value)

/** Every HTTP call of the foods, products, stores and receipts screens. */
@Injectable({ providedIn: 'root' })
export class FoodApiService {
  private readonly api = inject(LifeosApiService)

  /** Household profiles, who give the reviews. */
  profiles(): Observable<MemberProfileDto[]> {
    return this.api.get<{ memberProfiles: MemberProfileDto[] }>('/households/members').pipe(map(response => response.memberProfiles))
  }

  foods(): Observable<FoodDto[]> { return this.api.get<FoodDto[]>('/foods') }
  food(foodId: string): Observable<FoodDto> { return this.api.get<FoodDto>(`/foods/${id(foodId)}`) }
  createFood(request: FoodRequest): Observable<FoodDto> { return this.api.post<FoodDto>('/foods', request) }
  updateFood(foodId: string, request: FoodRequest): Observable<FoodDto> { return this.api.put<FoodDto>(`/foods/${id(foodId)}`, request) }
  archiveFood(foodId: string): Observable<void> { return this.api.delete(`/foods/${id(foodId)}`) }

  /** Reduces the picture, then uploads it to `/{kind}/{id}/image`; returns the updated food or recipe. */
  uploadImage<T>(kind: 'foods' | 'recipes', targetId: string, file: File): Observable<T> {
    return from(reduceImage(file, LIBRARY_IMAGE)).pipe(switchMap(image => {
      const form = new FormData()
      form.append('image', image)
      return this.api.put<T>(`/${kind}/${id(targetId)}/image`, form)
    }))
  }
  removeImage(kind: 'foods' | 'recipes', targetId: string): Observable<void> { return this.api.delete(`/${kind}/${id(targetId)}/image`) }

  stores(): Observable<StoreDto[]> { return this.api.get<StoreDto[]>('/stores') }
  createStore(request: StoreRequest): Observable<StoreDto> { return this.api.post<StoreDto>('/stores', request) }
  updateStore(storeId: string, request: StoreRequest): Observable<StoreDto> { return this.api.put<StoreDto>(`/stores/${id(storeId)}`, request) }
  deleteStore(storeId: string): Observable<void> { return this.api.delete(`/stores/${id(storeId)}`) }

  products(foodId?: string): Observable<ProductDto[]> { return this.api.get<ProductDto[]>('/products', { foodId }) }
  createProduct(request: ProductRequest): Observable<ProductDto> { return this.api.post<ProductDto>('/products', request) }
  updateProduct(productId: string, request: ProductRequest): Observable<ProductDto> { return this.api.put<ProductDto>(`/products/${id(productId)}`, request) }
  archiveProduct(productId: string): Observable<void> { return this.api.delete(`/products/${id(productId)}`) }
  recordPrice(productId: string, request: PriceRequest): Observable<ProductDto> {
    return this.api.post<ProductDto>(`/products/${id(productId)}/prices`, request)
  }
  removePrice(productId: string, priceId: string): Observable<ProductDto> {
    return this.api.delete<ProductDto>(`/products/${id(productId)}/prices/${id(priceId)}`)
  }

  receipts(): Observable<ReceiptDto[]> { return this.api.get<ReceiptDto[]>('/receipts') }
  /** Reduces the photo (readable, a few hundred KB), then stores it as a pending receipt. */
  uploadReceipt(photo: File, storeId: string | null, purchaseDate: string | null): Observable<ReceiptDto> {
    return from(reduceImage(photo, RECEIPT_PHOTO)).pipe(switchMap(reduced => {
      const form = new FormData()
      form.append('photo', reduced)
      if (storeId) form.append('storeId', storeId)
      if (purchaseDate) form.append('purchaseDate', purchaseDate)
      return this.api.post<ReceiptDto>('/receipts/photo', form)
    }))
  }
  updateReceipt(receiptId: string, request: UpdateReceiptRequest): Observable<ReceiptDto> {
    return this.api.put<ReceiptDto>(`/receipts/${id(receiptId)}`, request)
  }
}
