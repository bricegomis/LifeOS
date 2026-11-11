import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { ReceiptDto, StoreDto, UpdateReceiptRequest } from '@/app/core/api/api.models'
import { pendingCount, receiptSubtitle, todayInputValue, uploadDay, visibleReceipts, type ReceiptFilter } from './receipt-view'

/**
 * A receipt is only a stored photo (ADR 0010): take or pick a picture, optionally say where
 * and when, then mark it processed once its prices have been typed in by hand elsewhere.
 */
@Component({
  selector: 'app-tickets-page',
  standalone: true,
  imports: [FormsModule],
  styleUrl: './tickets.css',
  template: `
    <section class="page-stack tickets-page">
      <header class="page-hero">
        <div><h1>Tickets</h1><p>Photographiez un ticket de caisse : il est conservé tel quel, rien n’est lu automatiquement.</p></div>
        <div class="ticket-actions">
          <label class="lifeos-button">
            <i class="pi pi-camera" aria-hidden="true"></i> Photographier un ticket
            <input class="ticket-file-input" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" [disabled]="busy()" (change)="choosePhoto($event)" />
          </label>
          <label class="lifeos-button lifeos-button-secondary">
            Choisir une photo
            <input class="ticket-file-input" type="file" accept="image/jpeg,image/png,image/webp" [disabled]="busy()" (change)="choosePhoto($event)" />
          </label>
        </div>
      </header>

      @if (error()) { <p class="ticket-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }

      @if (draftFile()) {
        <section class="surface-card">
          <h2>Nouveau ticket</h2>
          <form class="ticket-form" (ngSubmit)="saveDraft()">
            @if (draftPreview(); as preview) { <img class="receipt-photo-preview" [src]="preview" alt="Aperçu de la photo du ticket" /> }
            <div class="dialog-row">
              <label class="dialog-field"><span>Magasin (facultatif)</span>
                <select class="text-input" name="draftStore" [(ngModel)]="draftStoreId">
                  <option value="">Non renseigné</option>
                  @for (store of stores(); track store.id) { <option [value]="store.id">{{ store.name }}</option> }
                </select>
              </label>
              <label class="dialog-field"><span>Date d’achat (facultative)</span>
                <input class="text-input" name="draftDate" type="date" [max]="today" [(ngModel)]="draftDate" />
              </label>
            </div>
            <div class="ticket-actions">
              <button class="lifeos-button" type="submit" [disabled]="busy()">Enregistrer le ticket</button>
              <button class="lifeos-button lifeos-button-secondary" type="button" [disabled]="busy()" (click)="discardDraft()">Annuler</button>
            </div>
          </form>
        </section>
      }

      <div class="ticket-filters" role="group" aria-label="Filtrer les tickets">
        <button type="button" class="lifeos-button" [class.lifeos-button-secondary]="filter() !== 'pending'" [attr.aria-pressed]="filter() === 'pending'" (click)="filter.set('pending')">À traiter ({{ pending() }})</button>
        <button type="button" class="lifeos-button" [class.lifeos-button-secondary]="filter() !== 'all'" [attr.aria-pressed]="filter() === 'all'" (click)="filter.set('all')">Tous ({{ receipts().length }})</button>
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
          <section class="surface-card ticket-detail">
            <h2>Ticket déposé le {{ uploadDay(receipt.createdAt) }}</h2>
            @if (photoUrl(); as url) { <img class="receipt-photo-preview" [src]="url" alt="Photo du ticket" /> }
            @else if (photoError()) { <p class="ticket-error" role="alert">{{ photoError() }}</p> }
            @else { <p role="status">Chargement de la photo…</p> }
            <form class="ticket-form" (ngSubmit)="saveSelected(receipt.status === 'processed')">
              <div class="dialog-row">
                <label class="dialog-field"><span>Magasin (facultatif)</span>
                  <select class="text-input" name="store" [(ngModel)]="storeId">
                    <option value="">Non renseigné</option>
                    @for (store of stores(); track store.id) { <option [value]="store.id">{{ store.name }}</option> }
                  </select>
                </label>
                <label class="dialog-field"><span>Date d’achat (facultative)</span>
                  <input class="text-input" name="date" type="date" [max]="today" [(ngModel)]="purchaseDate" />
                </label>
              </div>
              <div class="ticket-actions">
                <button class="lifeos-button lifeos-button-secondary" type="submit" [disabled]="busy() || !contextDirty()">Enregistrer</button>
                <button class="lifeos-button" type="button" [disabled]="busy()" (click)="saveSelected(receipt.status !== 'processed')">
                  {{ receipt.status === 'processed' ? 'Remettre à traiter' : 'Marquer comme traité' }}
                </button>
              </div>
            </form>
          </section>
        }
      </div>
    </section>
  `,
})
export class TicketsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(LifeosApiService)
  readonly receipts = signal<ReceiptDto[]>([])
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
      const [receipts, stores] = await Promise.all([
        firstValueFrom(this.api.get<ReceiptDto[]>('/receipts')),
        firstValueFrom(this.api.get<StoreDto[]>('/stores')),
      ])
      this.receipts.set(receipts)
      this.stores.set(stores)
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
    const form = new FormData()
    form.append('photo', file)
    if (this.draftStoreId) form.append('storeId', this.draftStoreId)
    if (this.draftDate) form.append('purchaseDate', this.draftDate)
    this.busy.set(true)
    this.error.set('')
    try {
      const receipt = await firstValueFrom(this.api.post<ReceiptDto>('/receipts/photo', form))
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
      const saved = await firstValueFrom(this.api.put<ReceiptDto>(`/receipts/${encodeURIComponent(receipt.id)}`, body))
      this.receipts.update(items => items.map(item => item.id === saved.id ? saved : item))
      this.showSelected(saved)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  private showSelected(receipt: ReceiptDto): void {
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
