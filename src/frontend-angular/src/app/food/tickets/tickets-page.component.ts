import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, ProductDto, ReceiptDto, StoreDto, UpdateReceiptRequest } from '../food.models'
import { PriceFormComponent } from '../products/price-form.component'
import { ProductEditorComponent, type ProductEditorData } from '../products/product-editor.component'
import { pendingCount, receiptSubtitle, todayInputValue, uploadDay, visibleReceipts, type ReceiptFilter } from './receipt-view'

/**
 * A receipt is only a stored photo (ADR 0010): take or pick a picture, optionally say where
 * and when. On a computer the pending receipts form an inbox: the prices are typed by hand
 * next to the photo, then the receipt is marked processed.
 */
@Component({
  selector: 'app-tickets-page',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, PriceFormComponent],
  template: `
    <div class="lo-page is-wide">
      <header class="lo-page-head">
        <div class="lo-page-title">
          <h1 class="lo-bar-title">Tickets</h1>
          <p class="lo-intro">La photo est allégée avant l’envoi, sans perdre la lisibilité ; rien n’est lu automatiquement. Les prix se saisissent sur ordinateur, à côté de la photo.</p>
        </div>
        <div class="lo-actions">
          <input #camera class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" tabindex="-1" aria-hidden="true" (change)="choosePhoto($event)" />
          <input #library class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" tabindex="-1" aria-hidden="true" (change)="choosePhoto($event)" />
          <button mat-flat-button type="button" [disabled]="busy()" (click)="camera.click()"><mat-icon svgIcon="photo_camera" />Photographier un ticket</button>
          <button mat-stroked-button type="button" [disabled]="busy()" (click)="library.click()"><mat-icon svgIcon="photo_library" />Choisir une photo</button>
        </div>
      </header>

      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }

      @if (draftFile()) {
        <section class="lo-panel" aria-labelledby="new-ticket">
          <h2 id="new-ticket">Nouveau ticket</h2>
          <form class="lo-form" (ngSubmit)="saveDraft()">
            @if (draftPreview(); as preview) { <img class="receipt-preview" [src]="preview" alt="Aperçu de la photo du ticket" /> }
            <div class="lo-form-row">
              <mat-form-field appearance="outline">
                <mat-label>Magasin (facultatif)</mat-label>
                <mat-select name="draftStore" [(ngModel)]="draftStoreId">
                  <mat-option value="">Non renseigné</mat-option>
                  @for (store of stores(); track store.id) { <mat-option [value]="store.id">{{ store.name }}</mat-option> }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Date d’achat (facultative)</mat-label>
                <input matInput name="draftDate" type="date" [max]="today" [(ngModel)]="draftDate" />
              </mat-form-field>
            </div>
            <div class="lo-actions">
              <button mat-flat-button type="submit" [disabled]="busy()"><mat-icon svgIcon="check" />Enregistrer le ticket</button>
              <button mat-button type="button" [disabled]="busy()" (click)="discardDraft()">Annuler</button>
            </div>
          </form>
        </section>
      }

      <div class="lo-chips" role="group" aria-label="Filtrer les tickets">
        <button type="button" class="lo-filter-chip" [class.is-on]="filter() === 'pending'" [attr.aria-pressed]="filter() === 'pending'" (click)="filter.set('pending')">
          @if (filter() === 'pending') { <mat-icon svgIcon="check" /> }À traiter ({{ pending() }})</button>
        <button type="button" class="lo-filter-chip" [class.is-on]="filter() === 'all'" [attr.aria-pressed]="filter() === 'all'" (click)="filter.set('all')">
          @if (filter() === 'all') { <mat-icon svgIcon="check" /> }Tous ({{ receipts().length }})</button>
      </div>

      @if (loading()) { <p class="lo-status" role="status">Chargement…</p> }
      @if (!loading() && !visible().length) {
        <p class="lo-empty">{{ receipts().length ? 'Aucun ticket à traiter.' : 'Aucun ticket enregistré.' }}</p>
      }

      <div class="ticket-layout">
        @if (visible().length) {
          <div class="lo-list ticket-list">
            @for (receipt of visible(); track receipt.id) {
              <div class="lo-row" [class.is-selected]="selected()?.id === receipt.id">
                <button type="button" class="lo-row-main" [attr.aria-pressed]="selected()?.id === receipt.id" (click)="select(receipt)">
                  <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="receipt_long" /></span>
                  <span class="lo-row-text"><span class="lo-row-title">Déposé le {{ uploadDay(receipt.createdAt) }}</span>
                    <span class="lo-row-sub">{{ subtitle(receipt) }}</span></span>
                  <span class="lo-tag" [class.is-ok]="receipt.status === 'processed'" [class.is-warn]="receipt.status !== 'processed'">
                    {{ receipt.status === 'processed' ? 'Traité' : 'À traiter' }}</span>
                </button>
              </div>
            }
          </div>
        }

        @if (selected(); as receipt) {
          <section class="lo-panel ticket-detail" [attr.aria-label]="'Ticket déposé le ' + uploadDay(receipt.createdAt)">
            <div class="ticket-photo">
              @if (photoUrl(); as url) {
                <img class="receipt-photo" [src]="url" alt="Photo du ticket" [style.transform]="'rotate(' + rotation() + 'deg)'" />
                <div class="lo-actions">
                  <a mat-button [href]="url" target="_blank" rel="noopener"><mat-icon svgIcon="open_in_new" />Ouvrir en grand</a>
                  <button mat-button type="button" (click)="rotate()"><mat-icon svgIcon="rotate_right" />Pivoter</button>
                </div>
              }
              @else if (photoError()) { <p class="lo-error" role="alert">{{ photoError() }}</p> }
              @else { <p class="lo-status" role="status">Chargement de la photo…</p> }
            </div>
            <div class="ticket-side">
              <h2>Déposé le {{ uploadDay(receipt.createdAt) }}</h2>
              <form class="lo-form" (ngSubmit)="saveSelected(receipt.status === 'processed')">
                <div class="lo-form-row">
                  <mat-form-field appearance="outline">
                    <mat-label>Magasin (facultatif)</mat-label>
                    <mat-select name="store" [(ngModel)]="storeId">
                      <mat-option value="">Non renseigné</mat-option>
                      @for (store of stores(); track store.id) { <mat-option [value]="store.id">{{ store.name }}</mat-option> }
                    </mat-select>
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Date d’achat (facultative)</mat-label>
                    <input matInput name="date" type="date" [max]="today" [(ngModel)]="purchaseDate" />
                  </mat-form-field>
                </div>
                <div class="lo-actions">
                  <button mat-stroked-button type="submit" [disabled]="busy() || !contextDirty()">Enregistrer</button>
                  <button mat-flat-button type="button" [disabled]="busy()" (click)="saveSelected(receipt.status !== 'processed')">
                    <mat-icon [svgIcon]="receipt.status === 'processed' ? 'undo' : 'check'" />
                    {{ receipt.status === 'processed' ? 'Remettre à traiter' : 'Marquer comme traité' }}
                  </button>
                </div>
              </form>
              <div class="price-entry lo-desktop-only">
                <h3>Relever un prix</h3>
                <app-price-form [products]="products()" [foods]="foods()" [stores]="stores()"
                  [storeId]="receipt.storeId" [observedOn]="receipt.purchaseDate ?? uploadDate(receipt)" (recorded)="priceRecorded($event)">
                  <button mat-button type="button" (click)="createProduct()"><mat-icon svgIcon="add" />Nouveau produit</button>
                </app-price-form>
              </div>
            </div>
          </section>
        }
      </div>
    </div>
  `,
  styles: [`
    .ticket-layout { display: flex; flex-wrap: wrap; align-items: flex-start; gap: 24px; }
    .ticket-list { min-width: 0; flex: 1 1 260px; }
    .lo-row.is-selected { background: var(--mat-sys-secondary-container); }
    .ticket-detail { flex: 3 1 520px; flex-direction: row; flex-wrap: wrap; align-items: flex-start; gap: 24px; }
    .ticket-photo { display: flex; min-width: 0; flex: 1 1 220px; flex-direction: column; gap: 8px; }
    .receipt-photo, .receipt-preview {
      display: block; width: 100%; max-height: 640px; border-radius: 8px;
      background: var(--mat-sys-surface-container-high); object-fit: contain; transition: transform 0.2s;
    }
    .receipt-preview { max-height: 320px; }
    .ticket-side { display: flex; min-width: 0; flex: 1 1 260px; flex-direction: column; gap: 16px; }
    .ticket-side h2, .lo-panel > h2 { font: var(--mat-sys-title-medium); font-weight: 600; }
    .price-entry { display: flex; flex-direction: column; gap: 12px; padding: 16px; border-radius: 12px; background: var(--mat-sys-surface-container-low); }
    .price-entry h3 { font: var(--mat-sys-title-small); }
  `],
})
export class TicketsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(LifeosApiService)
  private readonly food = inject(FoodApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly receipts = signal<ReceiptDto[]>([])
  readonly products = signal<ProductDto[]>([])
  readonly foods = signal<FoodDto[]>([])
  readonly rotation = signal(0)
  readonly stores = signal<StoreDto[]>([])
  readonly selected = signal<ReceiptDto | null>(null)
  readonly filter = signal<ReceiptFilter>('pending')
  readonly loading = signal(true)
  readonly busy = signal(false)
  readonly error = signal('')
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
      this.snack.open('Ticket enregistré', undefined, { duration: 4000 })
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

  async createProduct(): Promise<void> {
    const product = await firstValueFrom(openEditor<ProductEditorComponent, ProductEditorData, ProductDto>(this.dialog, ProductEditorComponent,
      { product: null, foods: this.foods(), stores: this.stores() }).afterClosed())
    if (product) this.products.update(list => [...list, product])
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
