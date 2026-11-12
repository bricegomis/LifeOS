import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, ProductDto, ReceiptDto, StoreDto, UpdateReceiptRequest } from '../food.models'
import { PriceFormComponent } from '../products/price-form.component'
import { ProductEditorComponent } from '../products/product-editor.component'
import { pendingCount, receiptSubtitle, todayInputValue, uploadDay, visibleReceipts, type ReceiptFilter } from './receipt-view'

/**
 * A receipt is only a stored photo (ADR 0010): take or pick a picture, optionally say where
 * and when. On a computer the pending receipts form an inbox: the prices are typed by hand
 * next to the photo, then the receipt is marked processed.
 */
@Component({
  selector: 'app-tickets-page',
  standalone: true,
  imports: [FormsModule, PriceFormComponent, ProductEditorComponent],
  styleUrl: './tickets.css',
  template: `
    <section class="page tickets-page">
      <div class="page-header">
        <h1>Tickets</h1>
        <div class="page-actions">
          <label class="lifeos-button">
            <i class="pi pi-camera" aria-hidden="true"></i> Photographier un ticket
            <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" [disabled]="busy()" (change)="choosePhoto($event)" />
          </label>
          <label class="lifeos-button lifeos-button-secondary">
            Choisir une photo existante
            <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" [disabled]="busy()" (change)="choosePhoto($event)" />
          </label>
        </div>
      </div>
      <p class="muted">Le ticket est conservé tel quel : rien n’est lu automatiquement. Les prix se saisissent sur ordinateur, à côté de la photo.</p>

      @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }

      @if (draftFile()) {
        <section class="card">
          <h2>Nouveau ticket</h2>
          <form class="ticket-form" (ngSubmit)="saveDraft()">
            @if (draftPreview(); as preview) { <img class="receipt-photo-preview" [src]="preview" alt="Aperçu de la photo du ticket" /> }
            <div class="field-row">
              <label class="field"><span>Magasin (facultatif)</span>
                <select class="lifeos-input" name="draftStore" [(ngModel)]="draftStoreId">
                  <option value="">Non renseigné</option>
                  @for (store of stores(); track store.id) { <option [value]="store.id">{{ store.name }}</option> }
                </select>
              </label>
              <label class="field"><span>Date d’achat (facultative)</span>
                <input class="lifeos-input" name="draftDate" type="date" [max]="today" [(ngModel)]="draftDate" />
              </label>
            </div>
            <div class="ticket-actions">
              <button class="lifeos-button" type="submit" [disabled]="busy()">Enregistrer le ticket</button>
              <button class="lifeos-button lifeos-button-secondary" type="button" [disabled]="busy()" (click)="discardDraft()">Annuler</button>
            </div>
          </form>
        </section>
      }

      <div class="filter-bar" role="group" aria-label="Filtrer les tickets">
        <button type="button" class="pill" [attr.aria-pressed]="filter() === 'pending'" (click)="filter.set('pending')">À traiter ({{ pending() }})</button>
        <button type="button" class="pill" [attr.aria-pressed]="filter() === 'all'" (click)="filter.set('all')">Tous ({{ receipts().length }})</button>
      </div>

      @if (loading()) { <p role="status">Chargement…</p> }
      @if (!loading() && !visible().length) {
        <p>{{ receipts().length ? 'Aucun ticket à traiter.' : 'Aucun ticket enregistré.' }}</p>
      }

      <div class="ticket-layout">
        <ul class="ticket-list">
          @for (receipt of visible(); track receipt.id) {
            <li>
              <button type="button" class="ticket-row" [class.is-selected]="selected()?.id === receipt.id" [attr.aria-pressed]="selected()?.id === receipt.id" (click)="select(receipt)">
                <strong>Déposé le {{ uploadDay(receipt.createdAt) }}</strong>
                <small>{{ subtitle(receipt) }}</small>
                <span class="ticket-status" [class.is-processed]="receipt.status === 'processed'">{{ receipt.status === 'processed' ? 'Traité' : 'À traiter' }}</span>
              </button>
            </li>
          }
        </ul>

        @if (selected(); as receipt) {
          <section class="card ticket-detail">
            <div class="ticket-photo">
              @if (photoUrl(); as url) {
                <img class="receipt-photo" [src]="url" alt="Photo du ticket" [style.transform]="'rotate(' + rotation() + 'deg)'" />
                <div class="page-actions">
                  <a class="lifeos-button lifeos-button-secondary lifeos-button-sm" [href]="url" target="_blank" rel="noopener">Ouvrir en grand</a>
                  <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" (click)="rotate()">Pivoter</button>
                </div>
              }
              @else if (photoError()) { <p class="error-text" role="alert">{{ photoError() }}</p> }
              @else { <p class="status-text" role="status">Chargement de la photo…</p> }
            </div>
            <div class="ticket-side">
            <h2>Déposé le {{ uploadDay(receipt.createdAt) }}</h2>
            <form class="ticket-form" (ngSubmit)="saveSelected(receipt.status === 'processed')">
              <div class="field-row">
                <label class="field"><span>Magasin (facultatif)</span>
                  <select class="lifeos-input" name="store" [(ngModel)]="storeId">
                    <option value="">Non renseigné</option>
                    @for (store of stores(); track store.id) { <option [value]="store.id">{{ store.name }}</option> }
                  </select>
                </label>
                <label class="field"><span>Date d’achat (facultative)</span>
                  <input class="lifeos-input" name="date" type="date" [max]="today" [(ngModel)]="purchaseDate" />
                </label>
              </div>
              <div class="ticket-actions">
                <button class="lifeos-button lifeos-button-secondary" type="submit" [disabled]="busy() || !contextDirty()">Enregistrer</button>
                <button class="lifeos-button" type="button" [disabled]="busy()" (click)="saveSelected(receipt.status !== 'processed')">
                  {{ receipt.status === 'processed' ? 'Remettre à traiter' : 'Marquer comme traité' }}
                </button>
              </div>
            </form>
            <div class="price-entry desktop-only">
              <h3>Relever un prix</h3>
              <app-price-form [products]="products()" [foods]="foods()" [stores]="stores()"
                [storeId]="receipt.storeId" [observedOn]="receipt.purchaseDate ?? uploadDate(receipt)" (recorded)="priceRecorded($event)">
                <button type="button" class="lifeos-button lifeos-button-secondary" (click)="creatingProduct.set(true)">Nouveau produit</button>
              </app-price-form>
            </div>
            </div>
          </section>
        }
      </div>
    </section>
    @if (creatingProduct()) {
      <app-product-editor [product]="null" [foods]="foods()" [stores]="stores()" (saved)="productCreated($event)" (closed)="creatingProduct.set(false)" />
    }
  `,
})
export class TicketsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(LifeosApiService)
  private readonly food = inject(FoodApiService)
  readonly receipts = signal<ReceiptDto[]>([])
  readonly products = signal<ProductDto[]>([])
  readonly foods = signal<FoodDto[]>([])
  readonly creatingProduct = signal(false)
  readonly rotation = signal(0)
  readonly stores = signal<StoreDto[]>([])
  readonly selected = signal<ReceiptDto | null>(null)
  readonly filter = signal<ReceiptFilter>('pending')
  readonly loading = signal(true)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly draftFile = signal<File | null>(null)
  readonly draftPreview = signal<string | null>(null)
  readonly photoUrl = signal<string | null>(null)
  readonly photoError = signal('')
  readonly visible = computed(() => visibleReceipts(this.receipts(), this.filter()))
  readonly pending = computed(() => pendingCount(this.receipts()))
  readonly today = todayInputValue()
  readonly uploadDay = uploadDay
  draftStoreId = ''
  draftDate = ''
  storeId = ''
  purchaseDate = ''
  private photoVersion = 0

  ngOnInit(): void { void this.load() }

  ngOnDestroy(): void {
    this.revoke(this.draftPreview())
    this.revoke(this.photoUrl())
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.draftFile() || this.contextDirty()) { event.preventDefault(); event.returnValue = '' }
  }

  canLeave(): boolean {
    return !(this.draftFile() || this.contextDirty()) || window.confirm('Quitter sans enregistrer ce ticket ?')
  }

  subtitle(receipt: ReceiptDto): string { return receiptSubtitle(receipt, this.stores()) }

  contextDirty(): boolean {
    const receipt = this.selected()
    return !!receipt && (this.storeId !== (receipt.storeId ?? '') || this.purchaseDate !== (receipt.purchaseDate ?? ''))
  }

  async load(): Promise<void> {
    this.loading.set(true)
    this.error.set('')
    try {
      const [receipts, stores, products, foods] = await Promise.all([
        firstValueFrom(this.food.receipts()),
        firstValueFrom(this.food.stores()),
        firstValueFrom(this.food.products()),
        firstValueFrom(this.food.foods()),
      ])
      this.receipts.set(receipts)
      this.stores.set(stores)
      this.products.set(products)
      this.foods.set(foods)
      const first = receipts.find(receipt => receipt.status === 'pending')
      if (first && !this.selected() && window.matchMedia?.('(min-width: 761px)').matches) this.select(first)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  choosePhoto(event: Event): void {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0] ?? null
    input.value = ''
    if (!file) return
    this.revoke(this.draftPreview())
    this.draftFile.set(file)
    this.draftPreview.set(URL.createObjectURL(file))
    this.draftStoreId = ''
    this.draftDate = ''
    this.error.set('')
    this.notice.set('')
  }

  discardDraft(): void {
    this.revoke(this.draftPreview())
    this.draftFile.set(null)
    this.draftPreview.set(null)
  }

  async saveDraft(): Promise<void> {
    const file = this.draftFile()
    if (!file || this.busy()) return
    this.busy.set(true)
    this.error.set('')
    try {
      const receipt = await firstValueFrom(this.food.uploadReceipt(file, this.draftStoreId || null, this.draftDate || null))
      this.receipts.update(items => [receipt, ...items])
      this.discardDraft()
      this.notice.set('Ticket enregistré.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  select(receipt: ReceiptDto): void {
    if (this.selected()?.id === receipt.id) return
    if (this.contextDirty() && !window.confirm('Abandonner les modifications de ce ticket ?')) return
    this.showSelected(receipt)
    void this.loadPhoto(receipt)
  }

  async saveSelected(processed: boolean): Promise<void> {
    const receipt = this.selected()
    if (!receipt || this.busy()) return
    const body: UpdateReceiptRequest = { storeId: this.storeId || null, purchaseDate: this.purchaseDate || null, processed }
    this.busy.set(true)
    this.error.set('')
    this.notice.set('')
    try {
      const saved = await firstValueFrom(this.food.updateReceipt(receipt.id, body))
      this.receipts.update(items => items.map(item => item.id === saved.id ? saved : item))
      this.showSelected(saved)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  /** Prices default to the purchase date, or else the upload day. */
  uploadDate(receipt: ReceiptDto): string { return receipt.createdAt.slice(0, 10) }

  rotate(): void { this.rotation.update(angle => (angle + 90) % 360) }

  priceRecorded(product: ProductDto): void {
    this.products.update(list => list.map(item => item.id === product.id ? product : item))
  }

  productCreated(product: ProductDto): void {
    this.products.update(list => [...list, product])
    this.creatingProduct.set(false)
  }

  private showSelected(receipt: ReceiptDto): void {
    this.rotation.set(0)
    this.selected.set(receipt)
    this.storeId = receipt.storeId ?? ''
    this.purchaseDate = receipt.purchaseDate ?? ''
  }

  private async loadPhoto(receipt: ReceiptDto): Promise<void> {
    const version = ++this.photoVersion
    this.revoke(this.photoUrl())
    this.photoUrl.set(null)
    this.photoError.set('')
    try {
      const blob = await firstValueFrom(this.api.getBlob(`/receipts/${encodeURIComponent(receipt.id)}/photo`))
      if (version !== this.photoVersion) return
      this.photoUrl.set(URL.createObjectURL(blob))
    } catch (error) {
      if (version === this.photoVersion) this.photoError.set(apiErrorMessage(error))
    }
  }

  private revoke(url: string | null): void {
    if (url) URL.revokeObjectURL(url)
  }
}
