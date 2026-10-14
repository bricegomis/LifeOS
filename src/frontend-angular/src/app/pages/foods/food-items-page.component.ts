import { Component, HostListener, inject, signal, viewChild } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import type { FoodItemDto, FoodPricesDto, ProductDto, ProductRequest, StoreDto, MemberProfileDto, HouseholdGroupDto, ReviewDto } from '@/app/core/api/api.models'
import { LifeosApiService, apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { HouseholdReviewsComponent } from '@/app/shared/reviews/household-reviews.component'
import { newFoodDraft, missingFoodFields } from './food-draft'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { summarizeFoodReviews, type FoodReviewSummary } from './food-review-summary'

export const newProductDraft = (foodItemId = ''): ProductRequest => ({
  foodItemId, name: '', description: '', purchaseUnit: 'unit', brand: null, barcode: null,
  packageQuantity: null, packageUnit: null, packageConfirmed: false, piecesComparable: false,
})

@Component({
  selector: 'app-food-items-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent, HouseholdReviewsComponent],
  template: `
    <section class="page-stack">
      <header class="page-hero">
        <div><h1>Aliments</h1><p>Vos références culinaires, indépendantes des marques. Nutrition et informations digestives saisies à la main.</p></div>
        <button class="lifeos-button" [disabled]="busy()" (click)="editFood(undefined, $event)">Nouvel aliment</button>
      </header>
      @if (error()) { <p class="lifeos-dialog-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      <div class="catalog-toolbar">
        <label class="dialog-field"><span>Rechercher un aliment</span><input class="text-input" type="search" [(ngModel)]="search" /></label>
        <label><input type="checkbox" [(ngModel)]="incompleteOnly" /> À compléter ({{ incompleteCount() }})</label>
        <label><input type="checkbox" [(ngModel)]="archivedOnly" /> Archives uniquement</label>
        <button class="lifeos-button lifeos-button-secondary" [disabled]="loading()" (click)="load()">Actualiser</button>
      </div>
      @if (loading()) { <p role="status">Chargement des aliments et des prix…</p> }
      @else if (!visibleFoods().length) { <p>Aucun aliment dans cette sélection. Créez votre première référence ou modifiez les filtres.</p> }
      <ul class="food-grid">
        @for (food of visibleFoods(); track food.id) {
          <li class="food-card" [class.food-card-archived]="food.isArchived">
            <div class="food-card-heading">
              <h2><button type="button" class="food-card-open" [disabled]="busy()" aria-haspopup="dialog"
                [attr.aria-label]="'Fiche aliment : ' + food.name" (click)="select(food, $event)">{{ food.name }}</button></h2>
              <i class="pi pi-arrow-up-right food-card-arrow" aria-hidden="true"></i>
            </div>
            <div class="food-card-meta">
              <span class="food-card-status">{{ food.isArchived ? 'Archivé' : missing(food).length ? 'À compléter' : 'Informations renseignées' }}</span>
              @if (reviewSummaries()[food.id]; as summary) {
                @if (summary.average !== null) {
                  <span class="food-card-rating" role="img" [attr.aria-label]="'Note moyenne du foyer : ' + rating(summary.average) + ' sur 10, sur ' + summary.scoredCount + ' avis notés'">
                    <i class="pi pi-star-fill" aria-hidden="true"></i>
                    <strong>{{ rating(summary.average) }}<span> / 10</span></strong>
                    <span>({{ summary.scoredCount }} avis)</span>
                  </span>
                } @else if (summary.count) { <span class="food-card-review-count">{{ summary.count }} avis · sans note</span> }
              }
            </div>
            <div class="food-card-info">
              @if (food.isArchived) { <p>Historique conservé</p> }
              @if (missing(food).length) { <p>À compléter : {{ missing(food).join(', ') }}</p> }
              <p>Base nutritionnelle : {{ food.referenceUnit || 'à renseigner' }}</p>
            </div>
            <div class="food-card-prices">
              @for (price of pricesFor(food.id); track price.unit) {
                <div class="food-card-price">
                  <p><strong>{{ number(price.price) }} €</strong><span> / {{ unitLabel(price.unit) }}</span></p>
                  <p class="price-note">{{ price.productName }} · {{ storeName(price.storeId) }} · {{ date(price.observedAt) }} · {{ price.provenance }}</p>
                </div>
              }
              @if (!pricesFor(food.id).length) { <p class="price-note">Prix à renseigner ou non comparable</p> }
            </div>
          </li>
        }
      </ul>
      <p class="dialog-hint">Prix observés historiques, non garantis aujourd'hui : minimum des derniers relevés par produit et magasin, par base comparable. Aucun produit n'est choisi pour vos recettes.</p>
    </section>
    @if (selected(); as food) {
      <app-lifeos-dialog [open]="dialog() === 'detail'" [heading]="food.name" cancelLabel="Fermer"
        [wide]="true" [centered]="true" [focusFields]="false" [busy]="detailBusy()" [dirty]="reviews()?.dirty() ?? false"
        [restoreFocusTo]="detailTrigger" [error]="error()" (dismissed)="closeDetail()">
        @if (notice()) { <p role="status">{{ notice() }}</p> }
        <section class="food-detail">
          <header class="catalog-row"><div><p>Nutrition générique / {{ food.referenceUnit || 'base à renseigner' }}</p>
            @if (food.isArchived) { <p>Archivé — historique conservé</p> }
          </div>
            <button class="lifeos-button" [disabled]="detailBusy()" (click)="editFood(food)">Modifier les informations</button>
          </header>
          <p>Calories : {{ number(food.nutrition?.caloriesPerUnit) }} kcal · Protéines : {{ number(food.nutrition?.proteinsPerUnit) }} g · Glucides : {{ number(food.nutrition?.carbsPerUnit) }} g · Lipides : {{ number(food.nutrition?.fatsPerUnit) }} g</p>
          <p>FODMAP : {{ fodmapLabel(food.fodmap) }} · NOVA : {{ food.nova ?? 'non renseigné' }}</p>
          @if (food.digestiveNotes) { <p>{{ food.digestiveNotes }}</p> }
          <p class="dialog-hint">Informations descriptives, sans recommandation médicale. Source historique : {{ food.source }}.</p>
          <app-household-reviews #householdReviews targetType="food" [targetId]="food.id" [profiles]="profiles()" [groups]="groups()"
            (reviewsLoaded)="updateReviewSummary(food.id, $event)" />
          <h3>Produits et prix</h3>
          <p>Un aliment reste utilisable dans les recettes sans produit associé.</p>
          <button class="lifeos-button lifeos-button-secondary" [disabled]="detailBusy() || food.isArchived" (click)="editProduct()">Associer un nouveau produit</button>
          <ul class="catalog-list">
            @for (product of productsFor(food.id); track product.id) {
              <li class="catalog-row"><div><strong>{{ product.name }}</strong><p>{{ product.brand || 'Sans marque renseignée' }} · {{ unitLabel(product.purchaseUnit) }} @if (product.isArchived) { · archivé }</p></div>
                <button class="lifeos-button lifeos-button-secondary" [disabled]="detailBusy()" (click)="editProduct(product)">Fiche et prix</button>
              </li>
            }
          </ul>
          @if (!food.isArchived) {
            <div><button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="detailBusy()" (click)="archive(food)">Archiver cet aliment</button></div>
          }
        </section>
      </app-lifeos-dialog>
    }
    <app-lifeos-dialog [open]="dialog() === 'food'" heading="Informations de l'aliment"
      description="Une saisie partielle peut être enregistrée. Zéro est une valeur ; vide signifie inconnu."
      formId="food-form" submitLabel="Enregistrer" [centered]="true" [busy]="busy()" [dirty]="dirty()" [error]="dialogError()" (dismissed)="closeEditor()">
      <form id="food-form" class="dialog-form" ngNativeValidate (ngSubmit)="saveFood()">
        <label class="dialog-field"><span>Nom générique</span><input class="text-input" name="name" required maxlength="500" [(ngModel)]="foodDraft.name" /></label>
        <label class="dialog-field"><span>Base nutritionnelle</span><input class="text-input" name="base" maxlength="50" placeholder="100 g, 100 ml, 1 pièce" [(ngModel)]="foodDraft.referenceUnit" /></label>
        @if (foodDraft.nutrition; as nutrition) {
          <div class="dialog-row">
            <label class="dialog-field"><span>Calories (kcal)</span><input class="text-input" name="kcal" type="number" min="0" step="any" [(ngModel)]="nutrition.caloriesPerUnit" /></label>
            <label class="dialog-field"><span>Protéines (g)</span><input class="text-input" name="protein" type="number" min="0" step="any" [(ngModel)]="nutrition.proteinsPerUnit" /></label>
            <label class="dialog-field"><span>Glucides (g)</span><input class="text-input" name="carbs" type="number" min="0" step="any" [(ngModel)]="nutrition.carbsPerUnit" /></label>
            <label class="dialog-field"><span>Lipides (g)</span><input class="text-input" name="fat" type="number" min="0" step="any" [(ngModel)]="nutrition.fatsPerUnit" /></label>
          </div>
        }
        <div class="dialog-row">
          <label class="dialog-field"><span>FODMAP</span><select class="text-input" name="fodmap" [(ngModel)]="foodDraft.fodmap">
            <option [ngValue]="null">Non renseigné</option><option value="low">Bas</option><option value="moderate">Modéré</option><option value="high">Élevé</option>
          </select></label>
          <label class="dialog-field"><span>Groupe NOVA (AUT)</span><select class="text-input" name="nova" [(ngModel)]="foodDraft.nova">
            <option [ngValue]="null">Non renseigné</option><option [ngValue]="1">1 · Peu/non transformé</option><option [ngValue]="2">2 · Ingrédient culinaire transformé</option><option [ngValue]="3">3 · Transformé</option><option [ngValue]="4">4 · Ultra-transformé</option>
          </select></label>
        </div>
        <label class="dialog-field"><span>Notes digestives (facultatif)</span><textarea class="text-input" name="notes" maxlength="2000" rows="3" [(ngModel)]="foodDraft.digestiveNotes"></textarea></label>
      </form>
    </app-lifeos-dialog>
    <app-lifeos-dialog [open]="dialog() === 'product'" heading="Produit et prix"
      description="Référence commerciale liée à cet aliment. Aucune nutrition par marque."
      formId="product-form" submitLabel="Enregistrer le produit" [centered]="true" [busy]="busy()" [dirty]="dirty()" [error]="dialogError()" (dismissed)="closeEditor()">
      @if (error()) { <p class="lifeos-dialog-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      <form id="product-form" class="dialog-form" ngNativeValidate (ngSubmit)="saveProduct()">
        <label class="dialog-field"><span>Nom commercial</span><input class="text-input" name="productName" required maxlength="500" [(ngModel)]="productDraft.name" /></label>
        <label class="dialog-field"><span>Description</span><textarea class="text-input" name="description" maxlength="1000" [(ngModel)]="productDraft.description"></textarea></label>
        <div class="dialog-row">
          <label class="dialog-field"><span>Marque</span><input class="text-input" name="brand" maxlength="200" [(ngModel)]="productDraft.brand" /></label>
          <label class="dialog-field"><span>Code-barres</span><input class="text-input" name="barcode" maxlength="50" [(ngModel)]="productDraft.barcode" /></label>
        </div>
        <label class="dialog-field"><span>Unité d'achat (confirmation à l'enregistrement)</span><select class="text-input" name="unit" required [disabled]="!!currentProduct()?.priceHistory?.length && !!currentProduct()?.purchaseUnitConfirmed" [(ngModel)]="productDraft.purchaseUnit">
          <option value="unknown" disabled>À confirmer</option>
          @for (unit of units; track unit.value) { <option [value]="unit.value">{{ unit.label }}</option> }
        </select></label>
        <div class="dialog-row">
          <label class="dialog-field"><span>Contenance par paquet</span><input class="text-input" name="size" type="number" min="0.000001" step="any" [(ngModel)]="productDraft.packageQuantity" /></label>
          <label class="dialog-field"><span>Unité de contenance</span><select class="text-input" name="sizeUnit" [(ngModel)]="productDraft.packageUnit"><option [ngValue]="null">Inconnue</option><option value="g">g</option><option value="kg">kg</option><option value="ml">ml</option><option value="L">L</option></select></label>
        </div>
        <label><input type="checkbox" name="confirmed" [(ngModel)]="productDraft.packageConfirmed" /> Contenance vérifiée et confirmée manuellement</label>
        <label><input type="checkbox" name="pieces" [(ngModel)]="productDraft.piecesComparable" /> Pièces réellement comparables pour cet aliment (même taille)</label>
        <p class="dialog-hint">Pas de conversion entre masse, volume et pièces. Les futures observations utilisent ces confirmations ; les anciens relevés ne sont jamais réinterprétés.</p>
      </form>
      @if (priceDirty()) { <p class="dialog-hint">Le relevé en cours reste à enregistrer avec « Ajouter le relevé » ; enregistrer le produit ne l’enregistre pas.</p> }
      @if (currentProduct(); as product) {
        @if (product.legacyPurchaseName) { <p>Libellé d'achat historique : {{ product.legacyPurchaseName }}</p> }
        @if (product.migrationOrigin) { <p>Provenance historique : {{ product.migrationOrigin }}</p> }
        <h3>Historique des prix</h3>
        <ul class="catalog-list">@for (price of product.priceHistory; track price.id) {
          <li><p>{{ number(price.price) }} € / {{ unitLabel(price.unit) }} · {{ storeName(price.storeId) }} · {{ date(price.observedAt) }} · {{ price.provenance }}</p>
            <p>Contenance du relevé : {{ number(price.packageQuantity) }} {{ price.packageUnit ?? '' }} · {{ price.packageConfirmed ? 'confirmée' : 'non confirmée' }} · {{ price.piecesComparable ? 'pièces comparables' : 'pièces non comparées' }}</p></li>
        }</ul>
        @if (!product.priceHistory.length) { <p>Prix à renseigner.</p> }
        <form class="dialog-form" ngNativeValidate (ngSubmit)="addPrice()">
          <label class="dialog-field"><span>Magasin</span><select class="text-input" name="store" required [(ngModel)]="priceDraft.storeId"><option value="">Choisir</option>@for (store of stores(); track store.id) { <option [value]="store.id">{{ store.name }}</option> }</select></label>
          <label class="dialog-field"><span>Prix par {{ unitLabel(product.purchaseUnit) }} (€)</span><input class="text-input" name="price" type="number" required min="0" step="0.01" [(ngModel)]="priceDraft.price" /></label>
          <label class="dialog-field"><span>Date du relevé</span><input class="text-input" name="date" type="date" required [(ngModel)]="priceDraft.date" /></label>
          <button class="lifeos-button lifeos-button-secondary" [disabled]="busy() || productDirty() || !product.purchaseUnitConfirmed">Ajouter le relevé</button>
          @if (!product.purchaseUnitConfirmed) { <p>Confirmez l'unité d'achat et enregistrez le produit avant un nouveau relevé.</p> }
          @if (productDirty()) { <p>Enregistrez la fiche avant d'ajouter un prix.</p> }
        </form>
        <a routerLink="/stores">Gérer les magasins</a>
        @if (!product.isArchived) { <button class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="archiveProduct()">Archiver ce produit</button> }
      } @else { <p>Enregistrez le produit pour relever ses prix.</p> }
    </app-lifeos-dialog>
  `,
  styleUrl: './catalog.css',
})
export class FoodItemsPageComponent {
  private readonly api = inject(LifeosApiService)
  readonly foods = signal<FoodItemDto[]>([])
  readonly products = signal<ProductDto[]>([])
  readonly prices = signal<FoodPricesDto[]>([])
  readonly stores = signal<StoreDto[]>([])
  readonly profiles = signal<MemberProfileDto[]>([])
  readonly groups = signal<HouseholdGroupDto[]>([])
  readonly reviewSummaries = signal<Record<string, FoodReviewSummary>>({})
  readonly selected = signal<FoodItemDto | null>(null)
  readonly currentProduct = signal<ProductDto | null>(null)
  readonly loading = signal(true)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly dialogError = signal('')
  readonly dialog = signal<'detail' | 'food' | 'product' | null>(null)
  readonly reviews = viewChild<HouseholdReviewsComponent>('householdReviews')
  detailTrigger: HTMLElement | null = null
  private returnToDetail = false
  search = ''
  incompleteOnly = false
  archivedOnly = false
  foodDraft = newFoodDraft()
  productDraft = newProductDraft()
  priceDraft: { storeId: string; price: number | null; date: string } = { storeId: '', price: null, date: '' }
  private editingFoodId = ''
  private initial = ''
  private initialPrice = ''
  private loadVersion = 0
  readonly units = purchaseUnits
  readonly missing = missingFoodFields
  readonly number = formatNumber
  readonly date = formatDate
  readonly unitLabel = unitLabel
  readonly rating = (value: number) => new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 1 }).format(value)
  constructor() { void this.load() }
  visibleFoods(): FoodItemDto[] { return this.foods().filter(f => f.isArchived === this.archivedOnly && (!this.incompleteOnly || this.missing(f).length) && f.name.toLocaleLowerCase('fr').includes(this.search.toLocaleLowerCase('fr'))) }
  incompleteCount(): number { return this.foods().filter(f => !f.isArchived && this.missing(f).length).length }
  pricesFor(id: string) { return this.prices().find(p => p.foodItemId === id)?.prices ?? [] }
  updateReviewSummary(id: string, reviews: ReviewDto[]): void {
    this.reviewSummaries.set({ ...this.reviewSummaries(), [id]: summarizeFoodReviews(reviews) })
  }
  productsFor(id: string) { return this.products().filter(p => p.foodItemId === id) }
  storeName(id: string): string { return this.stores().find(s => s.id === id)?.name ?? 'Magasin historique' }
  fodmapLabel(value: string | null): string { return value === 'low' ? 'bas' : value === 'moderate' ? 'modéré' : value === 'high' ? 'élevé' : 'non renseigné' }
  select(food: FoodItemDto, event?: Event): void {
    this.detailTrigger = event?.currentTarget instanceof HTMLElement ? event.currentTarget : null
    this.selected.set(food); this.dialog.set('detail')
  }
  detailBusy(): boolean { return this.busy() || !!this.reviews()?.busy() }
  closeDetail(): void { this.reviews()?.cancelEdit(); this.dialog.set(null) }
  closeEditor(): void { this.dialogError.set(''); this.dialog.set(this.returnToDetail && this.selected() ? 'detail' : null) }
  productDirty(): boolean { return hasChanges(this.initial, this.productDraft) }
  priceDirty(): boolean { return hasChanges(this.initialPrice, this.priceDraft) }
  dirty(): boolean {
    if (this.dialog() === 'food') return hasChanges(this.initial, this.foodDraft)
    if (this.dialog() === 'product') return this.productDirty() || this.priceDirty()
    return false
  }
  canLeave(): boolean { return !this.detailBusy() && (!(this.dirty() || this.reviews()?.dirty()) || window.confirm('Abandonner les informations non enregistrées ?')) }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void { if (this.detailBusy() || this.dirty() || this.reviews()?.dirty()) { event.preventDefault(); event.returnValue = '' } }
  async load(): Promise<void> {
    const version = ++this.loadVersion
    this.loading.set(true); this.error.set('')
    try {
      const [foods, products, prices, stores, household, groups] = await Promise.all([
        firstValueFrom(this.api.get<FoodItemDto[]>('/foods')), firstValueFrom(this.api.get<ProductDto[]>('/products')),
        firstValueFrom(this.api.get<FoodPricesDto[]>('/foods/prices')), firstValueFrom(this.api.get<StoreDto[]>('/stores')),
        firstValueFrom(this.api.get<{ memberProfiles: MemberProfileDto[] }>('/households/current')),
        firstValueFrom(this.api.get<HouseholdGroupDto[]>('/household-groups')),
      ])
      if (version !== this.loadVersion) return
      this.foods.set(foods); this.products.set(products); this.prices.set(prices); this.stores.set(stores)
      this.profiles.set(household.memberProfiles); this.groups.set(groups)
      if (this.selected()) this.selected.set(foods.find(f => f.id === this.selected()?.id) ?? null)
      this.reviewSummaries.set({})
      // Bound requests because the existing reviews API is scoped to one food.
      for (let start = 0; start < foods.length; start += 4) {
        await Promise.all(foods.slice(start, start + 4).map(async food => {
          const reviews = await firstValueFrom(this.api.get<ReviewDto[]>(`/reviews/foods/${encodeURIComponent(food.id)}`))
          if (version === this.loadVersion) this.updateReviewSummary(food.id, reviews)
        }))
        if (version !== this.loadVersion) return
      }
    } catch (e) {
      if (version === this.loadVersion) this.error.set(apiErrorMessage(e))
    } finally {
      if (version === this.loadVersion) this.loading.set(false)
    }
  }
  editFood(food?: FoodItemDto, event?: Event): void {
    if (this.detailBusy()) return
    this.returnToDetail = this.dialog() === 'detail'
    if (!food) this.detailTrigger = event?.currentTarget instanceof HTMLElement ? event.currentTarget : null
    this.editingFoodId = food?.id ?? ''
    this.foodDraft = food ? { name: food.name, referenceUnit: food.referenceUnit, nutrition: { ...newFoodDraft().nutrition!, ...food.nutrition }, fodmap: food.fodmap, nova: food.nova, digestiveNotes: food.digestiveNotes } : newFoodDraft()
    this.initial = formSnapshot(this.foodDraft); this.dialogError.set(''); this.dialog.set('food')
  }
  editProduct(product?: ProductDto): void {
    if (this.detailBusy()) return
    this.returnToDetail = this.dialog() === 'detail'
    this.currentProduct.set(product ?? null)
    this.productDraft = product ? { ...newProductDraft(), ...product, foodItemId: product.foodItemId ?? '' } : newProductDraft(this.selected()?.id)
    this.priceDraft = { storeId: '', price: null, date: '' }
    this.initialPrice = formSnapshot(this.priceDraft)
    this.initial = formSnapshot(this.productDraft); this.dialogError.set(''); this.dialog.set('product')
  }
  async saveFood(): Promise<void> {
    await this.action(async () => {
      const food = await firstValueFrom(this.editingFoodId ? this.api.put<FoodItemDto>(`/foods/${this.editingFoodId}`, this.foodDraft) : this.api.post<FoodItemDto>('/foods', this.foodDraft))
      this.selected.set(food); this.returnToDetail = true; await this.load(); this.closeEditor()
    }, true)
  }
  async saveProduct(): Promise<void> {
    await this.action(async () => {
      const product = await firstValueFrom(this.currentProduct() ? this.api.put<ProductDto>(`/products/${this.currentProduct()!.id}`, this.productDraft) : this.api.post<ProductDto>('/products', this.productDraft))
      this.currentProduct.set(product); this.initial = formSnapshot(this.productDraft); await this.load()
      if (!this.priceDirty()) this.closeEditor()
    }, true)
  }
  async archive(food: FoodItemDto): Promise<void> {
    if (!this.canLeave()) return
    if (!window.confirm(`Archiver « ${food.name} » ? Les recettes et événements historiques sont conservés.`)) return
    await this.action(async () => { await firstValueFrom(this.api.delete(`/foods/${food.id}`)); await this.load() })
  }
  async archiveProduct(): Promise<void> {
    const product = this.currentProduct()
    if (!product || !this.canLeave() || !window.confirm(`Archiver « ${product.name} » en conservant achats et prix ?`)) return
    await this.action(async () => { await firstValueFrom(this.api.delete(`/products/${product.id}`)); await this.load(); this.closeEditor() }, true)
  }
  async addPrice(): Promise<void> {
    if (this.productDirty()) { this.dialogError.set('Enregistrez la fiche avant le relevé de prix.'); return }
    await this.action(async () => {
      const product = await firstValueFrom(this.api.post<ProductDto>(`/products/${this.currentProduct()!.id}/price-entries`,
        { storeId: this.priceDraft.storeId, price: this.priceDraft.price, observedAt: `${this.priceDraft.date}T00:00:00Z` }))
      this.currentProduct.set(product); this.priceDraft.price = null; this.initialPrice = formSnapshot(this.priceDraft); await this.load()
    }, true)
  }
  private async action(fn: () => Promise<void>, inDialog = false): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.dialogError.set(''); this.error.set(''); this.notice.set('')
    try { await fn(); this.notice.set('Enregistrement effectué.') }
    catch (e) { (inDialog ? this.dialogError : this.error).set(apiErrorMessage(e)) }
    finally { this.busy.set(false) }
  }
}

export const purchaseUnits = [
  { value: 'kg', label: 'kg' }, { value: 'g', label: 'g' }, { value: 'L', label: 'L' }, { value: 'ml', label: 'ml' },
  { value: 'unit', label: 'unité' }, { value: 'bag', label: 'sachet' }, { value: 'box', label: 'boîte' }, { value: 'pot', label: 'pot' }, { value: 'pack', label: 'paquet' },
]
export function unitLabel(unit: string): string { return purchaseUnits.find(u => u.value === unit)?.label ?? (unit === 'piece' ? 'pièce comparable' : unit === 'unknown' ? 'unité à confirmer' : unit) }
export function formatNumber(value: number | null | undefined): string { return value == null ? 'inconnu' : new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 3 }).format(value) }
export function formatDate(value: string): string { return new Intl.DateTimeFormat('fr-FR', { timeZone: 'UTC' }).format(new Date(value)) }
