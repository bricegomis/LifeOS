import { Component, HostListener, inject, OnDestroy, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import type { FoodItemDto, ProductDto, ReceiptDto, ReceiptLineDto, ReceiptSummaryDto, StoreDto, ValidateReceiptLineRequest } from '@/app/core/api/api.models'
import { LifeosApiService, apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { purchaseUnits, unitLabel, formatNumber } from '../foods/food-items-page.component'
import { packageProposal, receiptDateInput } from './receipt-draft'

@Component({
  selector: 'app-tickets-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent],
  template: `
    <section class="page-stack">
      <header class="page-hero"><div><h1>Tickets</h1><p>Photographiez vos tickets, vérifiez les propositions OCR puis validez-les pour enregistrer vos achats et prix. Aucune création automatique.</p></div></header>
      @if (error()) { <p class="lifeos-dialog-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      <section class="surface-card">
        <h2>Importer une photo</h2>
        <p>Le texte est lu localement par le serveur. Les propositions peuvent être inexactes : vérifiez le magasin, la date, les quantités et les montants avant validation.</p>
        <form class="dialog-form" (ngSubmit)="uploadPhoto()">
          <label class="dialog-field"><span>Photo du ticket (JPEG, PNG ou WebP · 10 Mo maximum)</span><input class="text-input" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" (change)="selectPhoto($event)" /></label>
          <p class="dialog-hint">Sur mobile, l’appareil photo s’ouvre si disponible. Sinon, choisissez une image dans vos fichiers.</p>
          @if (photoFile()) { <p>{{ photoFile()!.name }} · {{ (photoFile()!.size / 1048576).toFixed(1) }} Mo</p> }
          <button class="lifeos-button" type="submit" [disabled]="busy() || !photoFile()">{{ busy() ? 'Lecture du ticket…' : 'Enregistrer la photo et proposer une lecture' }}</button>
        </form>
      </section>
      <details class="surface-card" [open]="!receipts().length || !!json.trim()">
        <summary>Importer un ticket JSON</summary>
        <form class="dialog-form" (ngSubmit)="importReceipt()">
          <label class="dialog-field"><span>Fichier JSON (1 Mo maximum)</span><input class="text-input" type="file" accept=".json,application/json" (change)="readFile($event)" /></label>
          <label class="dialog-field"><span>Ou coller le document JSON</span><textarea class="text-input" name="json" rows="7" [(ngModel)]="json"></textarea></label>
          <p class="dialog-hint">La source brute est conservée. Nutrition, FODMAP, NOVA et conseils du document ne seront jamais copiés dans la bibliothèque.</p>
          <button class="lifeos-button" [disabled]="busy() || !json.trim()">{{ busy() ? 'Import en cours…' : 'Importer sans créer de produits' }}</button>
        </form>
      </details>
      <section aria-label="Tickets importés">
        <h2>Imports enregistrés</h2>
        @if (loading()) { <p role="status">Chargement des tickets…</p> }
        <ul class="catalog-list">
          @for (r of receipts(); track r.id) {
            <li class="catalog-row"><div><strong>{{ r.storeName || 'Magasin à sélectionner' }}</strong><p>{{ r.hasPhoto && r.purchasedAt ? dateTimeLabel(r.purchasedAt) : dateInput(r.date) || 'Date à corriger' }} · {{ r.completedLines }} / {{ r.totalLines }} lignes traitées · {{ r.status === 'validated' ? 'Validé' : 'Brouillon' }}{{ r.hasPhoto ? ' · photo' : '' }}</p></div>
              <button class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="openReceipt(r.id)">Reprendre</button>
            </li>
          }
        </ul>
      </section>
      @if (receipt(); as r) {
        <section class="surface-card">
          <h2>{{ r.storeName || 'Ticket à relire' }} · {{ r.completedLines }} / {{ r.lines.length }}</h2>
          <p class="ticket-state">{{ r.status === 'validated' ? 'Ticket validé' : 'Brouillon — aucun prix photo ne sera enregistré avant confirmation' }}</p>
          @if (r.hasPhoto && photoUrl()) { <img class="receipt-photo-preview" [src]="photoUrl()" alt="Photo originale du ticket" /> }
          <progress [value]="r.completedLines" [max]="r.lines.length" aria-label="Progression du ticket"></progress>
          @if (r.completedLines === r.lines.length) { <p role="status">Toutes les lignes sont traitées.</p> }
          @if (r.diagnostic) { <p>{{ r.diagnostic }}</p> }
          @if (r.ocrError) { <p class="lifeos-dialog-error" role="alert">Lecture OCR impossible : {{ r.ocrError }} Vous pouvez ajouter et saisir les lignes manuellement.</p> }
          <p>Annoncées : {{ r.declaredLines ?? 'inconnu' }} · détectées : {{ r.detectedLines ?? 'inconnu' }} · conservées : {{ r.lines.length }}</p>
          <form class="dialog-form" ngNativeValidate (ngSubmit)="saveContext()">
            @if (r.hasPhoto) {
              <label class="dialog-field"><span>Date et heure du ticket</span><input class="text-input" name="purchasedAt" type="datetime-local" required [(ngModel)]="context.purchasedAtLocal" [disabled]="hasValidated()" /></label>
            } @else {
              <label class="dialog-field"><span>Date du ticket (jj/MM/aaaa)</span><input class="text-input" name="receiptDate" required placeholder="11/04/2026" [(ngModel)]="context.date" [disabled]="hasValidated()" /></label>
            }
            <label class="dialog-field"><span>Magasin de votre foyer</span><select class="text-input" name="receiptStore" [(ngModel)]="context.storeId" [disabled]="hasValidated()"><option value="">Créer un magasin</option>@for (store of stores(); track store.id) { <option [value]="store.id">{{ store.name }}</option> }</select></label>
            @if (!context.storeId) { <label class="dialog-field"><span>Nom du nouveau magasin</span><input class="text-input" name="newStore" required maxlength="200" [(ngModel)]="context.newStoreName" /></label> }
            @if (!hasValidated()) { <button class="lifeos-button lifeos-button-secondary" [disabled]="busy()">Confirmer date et magasin</button> }
          </form>
          @if (r.hasPhoto && r.status === 'draft') {
            <p class="dialog-hint">La confirmation s’applique à toutes les lignes en une seule transaction. Les lignes ignorées ne créent ni produit, ni achat, ni prix.</p>
            <p class="dialog-hint">Les choix de produits préparés sont gardés jusqu’à la validation sur cette page ; les corrections de lignes, la photo et l’OCR sont déjà conservés sur le serveur.</p>
            <button class="lifeos-button" [disabled]="busy() || !canFinalizePhoto()" (click)="finalizePhoto()">Valider le ticket et enregistrer les lignes préparées</button>
          }
          <h3>Lignes dans l'ordre du document</h3>
          @if (r.hasPhoto && r.status === 'draft') { <button class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="addPhotoLine()">Ajouter une ligne manuellement</button> }
          <ul class="catalog-list">
            @for (line of orderedLines(); track line.id) {
              <li class="catalog-row">
                <div><strong>{{ line.position + 1 }}. {{ line.label || 'Libellé à compléter' }}</strong>
                  <p class="source-line">{{ line.sourceText }}</p>
                  <p>{{ statusLabel(line.status) }} · {{ number(line.total) }} € au total · {{ number(line.quantity) }} {{ line.unit ? unitLabel(line.unit) : 'unité inconnue' }}</p>
                  @if (r.hasPhoto && line.status === 'pending') { <p>{{ photoChoices()[line.id] ? 'Produit choisi · prête pour validation finale' : 'À vérifier et associer à un produit' }}</p> }
                  @if (line.status === 'pending') { <p>{{ line.diagnostics.join(' ') }}</p> }
                  @if (line.status === 'validated') { <p>Achat enregistré · {{ productName(line.productId) }} · source : ce ticket</p> }
                </div>
                @if (line.status === 'pending') { <button class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="openLine(line)">Traiter cette ligne</button> }
              </li>
            }
          </ul>
          <details><summary>Document brut archivé</summary><pre class="raw-document">{{ r.rawDocument }}</pre></details>
        </section>
      }
    </section>
    <app-lifeos-dialog [open]="!!line()" [heading]="isPhotoDraft() ? 'Préparer une ligne du ticket' : 'Validation manuelle d’une ligne'" [description]="isPhotoDraft() ? 'Aucun achat ni prix ne sera créé avant la validation du ticket complet.' : 'Un achat par ligne. Les lignes répétées restent distinctes.'"
      [busy]="busy()" [dirty]="lineDirty()" [error]="dialogError()" (dismissed)="line.set(null)">
      @if (line(); as current) {
        <p class="source-line">{{ current.sourceText }}</p>
        @if (!current.excludedMention) {
          <form class="dialog-form" ngNativeValidate (ngSubmit)="saveLine()">
            <label class="dialog-field"><span>Libellé de la ligne</span><input class="text-input" name="label" required maxlength="500" [(ngModel)]="lineDraft.label" /></label>
            <div class="dialog-row">
              <label class="dialog-field"><span>Montant TOTAL payé (€)</span><input class="text-input" name="total" required type="number" min="0" step="0.01" [(ngModel)]="lineDraft.total" /></label>
              <label class="dialog-field"><span>Quantité achetée</span><input class="text-input" name="quantity" required type="number" min="0.000001" step="any" [(ngModel)]="lineDraft.quantity" /></label>
            </div>
            <label class="dialog-field"><span>Unité d'achat (pas la contenance)</span><select class="text-input" name="unit" required [(ngModel)]="lineDraft.unit"><option [ngValue]="null">À préciser</option>@for (unit of units; track unit.value) { <option [value]="unit.value">{{ unit.label }}</option> }</select></label>
            <label class="dialog-field"><span>Prix unitaire annoncé (facultatif)</span><input class="text-input" name="stated" type="number" min="0" step="any" [(ngModel)]="lineDraft.statedUnitPrice" /></label>
            <label><input type="checkbox" name="mismatch" [(ngModel)]="lineDraft.priceMismatchConfirmed" /> Je confirme explicitement l'écart entre montant, quantité et prix annoncé (aucune remise interprétée)</label>
            <button class="lifeos-button lifeos-button-secondary" [disabled]="busy()">Enregistrer les corrections en attente</button>
          </form>
          <p>{{ current.diagnostics.join(' ') }}</p>
          <form class="dialog-form" ngNativeValidate (ngSubmit)="submitValidation()">
            <h3>Choisir le produit</h3>
            <label class="dialog-field"><span>Rechercher un produit existant</span><input class="text-input" name="productSearch" type="search" [(ngModel)]="productSearch" /></label>
            <label class="dialog-field"><span>Produit de votre foyer</span><select class="text-input" name="product" [(ngModel)]="validation.productId"><option value="">Créer un produit alimentaire</option>@for (p of matchingProducts(); track p.id) { <option [value]="p.id">{{ p.name }} · {{ unitLabel(p.purchaseUnit) }}{{ p.foodItemId ? '' : ' · aliment à lier' }}</option> }</select></label>
            @if (!validation.productId || !selectedProduct()?.foodItemId) {
              <label class="dialog-field"><span>Aliment générique associé</span><select class="text-input" name="food" [(ngModel)]="validation.foodItemId"><option value="">Créer un aliment sans nutrition</option>@for (food of foods(); track food.id) { @if (!food.isArchived) { <option [value]="food.id">{{ food.name }}</option> } }</select></label>
              @if (!validation.foodItemId) { <label class="dialog-field"><span>Nom du nouvel aliment</span><input class="text-input" name="foodName" required maxlength="500" [(ngModel)]="validation.newFoodName" /></label> }
            }
            @if (!validation.productId) {
              <label class="dialog-field"><span>Nom commercial du nouveau produit</span><input class="text-input" name="productName" required maxlength="500" [(ngModel)]="validation.newProductName" /></label>
              <p class="dialog-hint">Contenance proposée depuis le libellé, jamais confirmée automatiquement. Un sachet de 400 g est une unité achetée, pas 400 unités.</p>
              <div class="dialog-row">
                <label class="dialog-field"><span>Contenance du paquet</span><input class="text-input" name="package" type="number" min="0.000001" step="any" [(ngModel)]="validation.packageQuantity" /></label>
                <label class="dialog-field"><span>Unité de contenance</span><select class="text-input" name="packageUnit" [(ngModel)]="validation.packageUnit"><option [ngValue]="null">Inconnue</option><option value="g">g</option><option value="kg">kg</option><option value="ml">ml</option><option value="L">L</option></select></label>
              </div>
              <label><input type="checkbox" name="packageConfirmed" [(ngModel)]="validation.packageConfirmed" /> Contenance vérifiée et confirmée</label>
              <label><input type="checkbox" name="pieces" [(ngModel)]="validation.piecesComparable" /> Pièces de taille réellement comparable pour cet aliment</label>
            } @else {
              <p>Le produit existant conserve sa contenance confirmée. Modifiez sa fiche depuis l'aliment si nécessaire.</p>
              @if (!selectedProduct()?.purchaseUnitConfirmed) {
                <label><input type="checkbox" name="confirmPurchaseUnit" [(ngModel)]="validation.confirmPurchaseUnit" /> Je confirme que l'unité de cette ligne est l'unité d'achat du produit historique</label>
              }
            }
            @if (lineCorrectionDirty() || contextDirty()) { <p>Enregistrez les corrections et le contexte avant de valider.</p> }
            <button class="lifeos-button" type="submit" [disabled]="busy() || (!isPhotoDraft() && (lineCorrectionDirty() || contextDirty() || !!current.diagnostics.length || !receipt()?.storeId))">
              {{ isPhotoDraft() ? 'Enregistrer les corrections et préparer cette ligne' : 'Valider uniquement cette ligne et enregistrer l’achat' }}
            </button>
          </form>
        } @else { <p>Mention non alimentaire : aucune création autorisée.</p> }
        <button class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="ignoreLine()">Ignorer cette ligne sans création</button>
        <p><a routerLink="/foods">Compléter les aliments séparément dans la bibliothèque</a></p>
      }
    </app-lifeos-dialog>
  `,
  styleUrls: ['../foods/catalog.css', './tickets.css'],
})
export class TicketsPageComponent implements OnDestroy {
  private readonly api = inject(LifeosApiService)
  private readonly router = inject(Router)
  private readonly route = inject(ActivatedRoute)
  readonly receipts = signal<ReceiptSummaryDto[]>([])
  readonly receipt = signal<ReceiptDto | null>(null)
  readonly line = signal<ReceiptLineDto | null>(null)
  readonly products = signal<ProductDto[]>([])
  readonly foods = signal<FoodItemDto[]>([])
  readonly stores = signal<StoreDto[]>([])
  readonly loading = signal(true)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly dialogError = signal('')
  readonly photoFile = signal<File | null>(null)
  readonly photoUrl = signal<string | null>(null)
  readonly photoChoices = signal<Record<string, ValidateReceiptLineRequest>>({})
  readonly units = purchaseUnits
  readonly unitLabel = unitLabel
  readonly number = formatNumber
  readonly dateInput = receiptDateInput
  json = ''
  productSearch = ''
  context = { storeId: '', newStoreName: '', date: '', purchasedAtLocal: '' }
  lineDraft: { label: string; total: number | null; quantity: number | null; unit: string | null; statedUnitPrice: number | null; priceMismatchConfirmed: boolean } = { label: '', total: null, quantity: null, unit: null, statedUnitPrice: null, priceMismatchConfirmed: false }
  validation = this.emptyValidation()
  private contextInitial = ''
  private lineInitial = ''
  private validationInitial = ''
  private photoObjectUrl: string | null = null
  constructor() { void this.load() }
  ngOnDestroy(): void { this.clearPhotoUrl() }
  private emptyValidation() { return { productId: '', foodItemId: '', newFoodName: '', newProductName: '', packageQuantity: null as number | null, packageUnit: null as string | null, packageConfirmed: false, piecesComparable: false, confirmPurchaseUnit: false } }
  orderedLines() { return [...(this.receipt()?.lines ?? [])].sort((a, b) => a.position - b.position) }
  hasValidated(): boolean { return !!this.receipt()?.lines.some(l => l.status === 'validated') }
  contextDirty(): boolean { return !!this.receipt() && hasChanges(this.contextInitial, this.context) }
  lineCorrectionDirty(): boolean { return !!this.line() && hasChanges(this.lineInitial, this.lineDraft) }
  lineDirty(): boolean { return this.lineCorrectionDirty() || (!!this.line() && hasChanges(this.validationInitial, this.validation)) }
  canLeave(): boolean { return !(this.json.trim() || this.photoFile() || this.contextDirty() || this.lineDirty() || Object.keys(this.photoChoices()).length > 0) || window.confirm('Abandonner les informations non enregistrées ? Les lignes déjà enregistrées seront conservées.') }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void { if (this.json.trim() || this.photoFile() || this.contextDirty() || this.lineDirty() || Object.keys(this.photoChoices()).length > 0) { event.preventDefault(); event.returnValue = '' } }
  productName(id: string | null): string { return this.products().find(p => p.id === id)?.name ?? 'Produit historique' }
  statusLabel(status: string): string { return status === 'validated' ? 'Validée' : status === 'ignored' ? 'Ignorée' : 'En attente' }
  isPhotoDraft(): boolean { return this.receipt()?.hasPhoto === true && this.receipt()?.status === 'draft' }
  dateTimeLabel(value: string): string { return new Intl.DateTimeFormat('fr-FR', { dateStyle: 'short', timeStyle: 'short', timeZone: 'UTC' }).format(new Date(value)) }
  canFinalizePhoto(): boolean {
    const receipt = this.receipt()
    if (!receipt || !receipt.hasPhoto || receipt.status !== 'draft' || this.contextDirty() || !receipt.storeId || !receipt.purchasedAt || receipt.lines.length === 0) return false
    return receipt.lines.filter(line => line.status === 'pending').every(line =>
      !!this.photoChoices()[line.id] && line.diagnostics.length === 0)
  }
  matchingProducts() { return this.products().filter(p => !p.isArchived && (p.id === this.validation.productId || p.name.toLocaleLowerCase('fr').includes(this.productSearch.toLocaleLowerCase('fr')))) }
  selectedProduct() { return this.products().find(p => p.id === this.validation.productId) }
  async load(): Promise<void> {
    this.loading.set(true); this.error.set('')
    try {
      await this.loadReferences()
      const id = this.route.snapshot.queryParamMap.get('ticket')
      if (id) await this.fetchReceipt(id)
    } catch (e) { this.error.set(apiErrorMessage(e)) } finally { this.loading.set(false) }
  }
  private async loadReferences(): Promise<void> {
    const [receipts, products, foods, stores] = await Promise.all([
      firstValueFrom(this.api.get<ReceiptSummaryDto[]>('/receipts')), firstValueFrom(this.api.get<ProductDto[]>('/products')),
      firstValueFrom(this.api.get<FoodItemDto[]>('/foods')), firstValueFrom(this.api.get<StoreDto[]>('/stores')),
    ])
    this.receipts.set(receipts); this.products.set(products); this.foods.set(foods); this.stores.set(stores)
  }
  async readFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    if (!file) return
    if (file.size > 1_048_576) { this.error.set('Fichier trop volumineux : 1 Mo maximum.'); input.value = ''; return }
    try { this.json = await file.text(); this.error.set('') } catch (e) { this.error.set(apiErrorMessage(e)) }
  }
  selectPhoto(event: Event): void {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    if (!file) return
    if (file.size > 10 * 1024 * 1024) { this.error.set('Fichier trop volumineux : 10 Mo maximum.'); input.value = ''; return }
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) { this.error.set('Formats acceptés : JPEG, PNG et WebP.'); input.value = ''; return }
    this.photoFile.set(file)
    this.error.set('')
  }
  async uploadPhoto(): Promise<void> {
    const file = this.photoFile()
    if (!file) return
    await this.action(async () => {
      const data = new FormData()
      data.append('photo', file, file.name)
      const r = await firstValueFrom(this.api.post<ReceiptDto>('/receipts/photo', data))
      this.photoFile.set(null)
      this.photoChoices.set({})
      this.setReceipt(r)
      await this.loadPhoto(r)
      await this.loadReferences()
      await this.router.navigate([], { relativeTo: this.route, queryParams: { ticket: r.id }, replaceUrl: true })
    }, 'Photo conservée en brouillon. Vérifiez chaque proposition avant de valider.')
  }
  async importReceipt(): Promise<void> {
    await this.action(async () => {
      const r = await firstValueFrom(this.api.post<ReceiptDto>('/receipts', { json: this.json }))
      this.json = ''; this.photoChoices.set({}); this.setReceipt(r); await this.loadPhoto(r); await this.loadReferences()
      await this.router.navigate([], { relativeTo: this.route, queryParams: { ticket: r.id }, replaceUrl: true })
    }, 'Ticket conservé. Choisissez le magasin, puis traitez chaque ligne.')
  }
  async openReceipt(id: string): Promise<void> {
    if (!this.canLeave()) return
    await this.action(async () => { this.line.set(null); this.photoChoices.set({}); await this.fetchReceipt(id); await this.router.navigate([], { relativeTo: this.route, queryParams: { ticket: id } }) }, '')
  }
  private async fetchReceipt(id: string): Promise<void> {
    const receipt = await firstValueFrom(this.api.get<ReceiptDto>(`/receipts/${id}`))
    this.setReceipt(receipt)
    await this.loadPhoto(receipt)
  }
  private setReceipt(r: ReceiptDto): void {
    this.receipt.set(r)
    this.context = {
      storeId: r.storeId ?? '',
      newStoreName: r.storeName,
      date: receiptDateInput(r.date),
      purchasedAtLocal: r.hasPhoto && r.purchasedAt ? this.toDateTimeLocal(r.purchasedAt) : '',
    }
    this.contextInitial = formSnapshot(this.context)
  }
  private async loadPhoto(r: ReceiptDto): Promise<void> {
    this.clearPhotoUrl()
    if (!r.hasPhoto) return
    const blob = await firstValueFrom(this.api.getBlob(`/receipts/${r.id}/photo`))
    this.photoObjectUrl = URL.createObjectURL(blob)
    this.photoUrl.set(this.photoObjectUrl)
  }
  private clearPhotoUrl(): void {
    if (this.photoObjectUrl) URL.revokeObjectURL(this.photoObjectUrl)
    this.photoObjectUrl = null
    this.photoUrl.set(null)
  }
  private toDateTimeLocal(value: string): string {
    return value.slice(0, 16)
  }
  async saveContext(): Promise<void> {
    await this.action(async () => {
      const payload = {
        ...this.context,
        date: this.context.purchasedAtLocal ? this.dateFromLocal(this.context.purchasedAtLocal) : this.context.date,
        purchasedAt: this.receipt()?.hasPhoto && this.context.purchasedAtLocal
          ? `${this.context.purchasedAtLocal}:00Z`
          : null,
        storeId: this.context.storeId || null,
      }
      const saved = await firstValueFrom(this.api.put<ReceiptDto>(`/receipts/${this.receipt()!.id}/context`, payload))
      this.setReceipt(saved); await this.loadReferences()
    }, 'Date et magasin confirmés.')
  }
  private dateFromLocal(value: string): string {
    const [date] = value.split('T')
    if (!date) return ''
    const [year, month, day] = date.split('-')
    return `${day}/${month}/${year}`
  }
  openLine(l: ReceiptLineDto): void {
    this.line.set(l); this.lineDraft = { label: l.label, total: l.total, quantity: l.quantity, unit: l.unit, statedUnitPrice: l.statedUnitPrice, priceMismatchConfirmed: l.priceMismatchConfirmed }
    this.lineInitial = formSnapshot(this.lineDraft)
    const proposal = packageProposal(l.label)
    const savedChoice = this.photoChoices()[l.id]
    this.validation = savedChoice
      ? {
          ...savedChoice,
          productId: savedChoice.productId ?? '',
          foodItemId: savedChoice.foodItemId ?? '',
          newFoodName: savedChoice.newFoodName ?? '',
          newProductName: savedChoice.newProductName ?? '',
        }
      : { ...this.emptyValidation(), newProductName: l.label, packageQuantity: proposal.quantity, packageUnit: proposal.unit }
    this.validationInitial = formSnapshot(this.validation); this.dialogError.set('')
  }
  async saveLine(): Promise<void> {
    await this.action(async () => {
      const lineId = this.line()!.id
      const r = await firstValueFrom(this.api.put<ReceiptDto>(`/receipts/${this.receipt()!.id}/lines/${lineId}`, this.lineDraft))
      const choices = { ...this.photoChoices() }
      delete choices[lineId]
      this.photoChoices.set(choices)
      this.receipt.set(r); this.line.set(r.lines.find(l => l.id === this.line()!.id) ?? null); this.lineInitial = formSnapshot(this.lineDraft)
    }, 'Corrections enregistrées ; ligne toujours en attente.', true)
  }
  async validateLine(): Promise<void> {
    await this.action(async () => {
      const r = await firstValueFrom(this.api.post<ReceiptDto>(`/receipts/${this.receipt()!.id}/lines/${this.line()!.id}/validate`,
        { ...this.validation, productId: this.validation.productId || null, foodItemId: this.validation.foodItemId || null }))
      this.setReceipt(r); this.line.set(null); await this.loadReferences()
    }, 'Ligne validée : achat et prix enregistrés une seule fois.', true)
  }
  async submitValidation(): Promise<void> {
    if (this.isPhotoDraft()) await this.preparePhotoLine()
    else await this.validateLine()
  }
  async preparePhotoLine(): Promise<void> {
    const current = this.line()
    if (!current || !this.isPhotoDraft()) return
    await this.action(async () => {
      const receipt = this.receipt()!
      const updated = await firstValueFrom(this.api.put<ReceiptDto>(`/receipts/${receipt.id}/lines/${current.id}`, this.lineDraft))
      const line = updated.lines.find(item => item.id === current.id)
      if (!line) throw new Error('La ligne corrigée n’a pas été retrouvée.')
      this.receipt.set(updated)
      this.lineInitial = formSnapshot(this.lineDraft)
      if (line.diagnostics.length) throw new Error(line.diagnostics.join(' '))
      const choice = {
        ...this.validation,
        productId: this.validation.productId || null,
        foodItemId: this.validation.foodItemId || null,
      }
      this.photoChoices.set({ ...this.photoChoices(), [line.id]: choice })
      this.line.set(null)
    }, 'Ligne corrigée et prête. Aucun prix n’est encore enregistré.', true)
  }
  async addPhotoLine(): Promise<void> {
    const receipt = this.receipt()
    if (!receipt) return
    await this.action(async () => {
      const updated = await firstValueFrom(this.api.post<ReceiptDto>(`/receipts/${receipt.id}/lines`, { label: '' }))
      this.receipt.set(updated)
      const added = updated.lines.reduce((latest, current) => current.position > latest.position ? current : latest)
      this.openLine(added)
    }, 'Ligne ajoutée au brouillon.', true)
  }
  async finalizePhoto(): Promise<void> {
    const receipt = this.receipt()
    if (!receipt || !this.canFinalizePhoto()) return
    await this.action(async () => {
      const pending = receipt.lines.filter(line => line.status === 'pending')
      const r = await firstValueFrom(this.api.post<ReceiptDto>(`/receipts/${receipt.id}/finalize`, {
        lines: pending.map(line => ({ lineId: line.id, validation: this.photoChoices()[line.id] })),
      }))
      this.photoChoices.set({})
      this.setReceipt(r)
      await this.loadReferences()
    }, 'Ticket validé en une transaction : les achats et prix sont enregistrés.', true)
  }
  async ignoreLine(): Promise<void> {
    if (this.lineDirty() && !window.confirm('Ignorer la ligne et abandonner la saisie non enregistrée ?')) return
    await this.action(async () => {
      const r = await firstValueFrom(this.api.post<ReceiptDto>(`/receipts/${this.receipt()!.id}/lines/${this.line()!.id}/ignore`))
      const choices = { ...this.photoChoices() }
      delete choices[this.line()!.id]
      this.photoChoices.set(choices)
      this.receipt.set(r); this.line.set(null); await this.loadReferences()
    }, 'Ligne ignorée sans création.', true)
  }
  private async action(fn: () => Promise<void>, notice: string, dialog = false): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set(''); this.dialogError.set(''); this.notice.set('')
    try { await fn(); this.notice.set(notice) }
    catch (e) { (dialog ? this.dialogError : this.error).set(apiErrorMessage(e)) }
    finally { this.busy.set(false) }
  }
}
