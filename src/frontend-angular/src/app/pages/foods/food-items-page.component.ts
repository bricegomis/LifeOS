import { firstValueFrom } from 'rxjs'
import { Component, computed, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import {
  FoodItemDto,
  FoodItemRequest,
  NutritionPerUnitDto,
  StoreDto,
  ProductUsageDto,
  ArticlePriceEntryDto,
  GroceryItemUnit,
} from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { newProductEditor, productEditorFromItem, productRequest, productDraftProblem, type ProductEditor } from './product-draft'

const emptyEditor = newProductEditor

@Component({
  selector: 'app-food-items-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent],
  template: `
    <section class="page-stack foods-page" aria-labelledby="foods-page-title">
      <header class="page-hero foods-hero">
        <div>
          <h1 id="foods-page-title">Produits</h1>
          <p>Une seule fiche pour la nutrition, les achats et les prix par magasin. Les produits sans nutrition ont aussi leur place ici.</p>
        </div>
        <div class="foods-hero-actions">
          <div class="foods-count" aria-label="Nombre de produits dans le catalogue">
            <strong>{{ items().length }}</strong>
            <span>{{ items().length === 1 ? 'produit' : 'produits' }}</span>
          </div>
          <button class="lifeos-button" type="button" [disabled]="busy()" (click)="openCreate()">
            <i class="pi pi-plus" aria-hidden="true"></i> Nouveau produit
          </button>
        </div>
      </header>

      @if (error()) {
        <p class="foods-feedback foods-error" role="alert">{{ error() }}</p>
      }
      @if (notice()) {
        <p class="foods-feedback foods-notice" role="status">{{ notice() }}</p>
      }

      <div class="foods-main-column">
          <section class="foods-search-block" aria-labelledby="off-search-title">
            <div class="foods-section-heading">
              <div>
                <h2 id="off-search-title">Rechercher un produit</h2>
                <p>Recherchez un produit par son nom ou son code-barres.</p>
              </div>
            </div>
            <p class="foods-search-notice" role="note">
              Une recherche dans Open Food Facts peut ajouter automatiquement un produit au catalogue de votre foyer.
              Les résultats sont déjà enregistrés : ouvrez leur fiche pour préciser l’unité d’achat. Aucune conversion par nom ou par marque.
            </p>
            <div class="foods-searches">
              <form class="foods-search-form" (ngSubmit)="searchByName()">
                <label class="foods-field">
                  <span>Nom du produit</span>
                  <input name="offName" type="search" [(ngModel)]="offNameSearch"
                    placeholder="Rechercher des produits" required />
                </label>
                <button class="foods-secondary" type="submit" [disabled]="busy()">
                  {{ searchingName() ? 'Recherche…' : 'Rechercher' }}
                </button>
              </form>
              <form class="foods-search-form" (ngSubmit)="searchByBarcode()">
                <label class="foods-field">
                  <span>Code-barres</span>
                  <input name="offBarcode" type="search" inputmode="numeric"
                    [(ngModel)]="offBarcodeSearch" placeholder="Scannez ou saisissez un code-barres" required />
                </label>
                <button class="foods-secondary" type="submit" [disabled]="busy()">
                  {{ searchingBarcode() ? 'Recherche…' : 'Rechercher le code-barres' }}
                </button>
              </form>
            </div>
            @if (searched() && searchResults().length === 0 && !busy()) {
              <p class="foods-empty-inline">Aucun produit trouvé. Essayez un autre nom ou code-barres.</p>
            }
            @if (searchResults().length > 0) {
              <ul class="foods-results" aria-label="Résultats de recherche Open Food Facts enregistrés">
                @for (result of searchResults(); track result.id) {
                  <li class="foods-result">
                    <div class="foods-item-copy">
                      <h3>{{ result.name }}</h3>
                      <p>{{ result.referenceUnit }} · {{ nutritionSummary(result.nutrition) }}</p>
                      @if (result.offBarcode) {
                        <small>Code-barres : {{ result.offBarcode }}</small>
                      }
                    </div>
                    <button class="foods-secondary" type="button" [disabled]="busy()"
                      (click)="editFood(result)">Modifier</button>
                  </li>
                }
              </ul>
            }
          </section>

          <section class="foods-catalog" aria-labelledby="catalog-heading">
            <div class="foods-section-heading">
              <div>
                <h2 id="catalog-heading">Vos produits</h2>
                <p>Nutrition et achats partagent la même identité.</p>
              </div>
              <label class="foods-field"><span>Filtrer vos produits</span>
                <input type="search" [(ngModel)]="catalogSearch" placeholder="Nom du produit" />
              </label>
              <label><input type="checkbox" [(ngModel)]="showArchived" /> Afficher les produits archivés</label>
              <button class="foods-secondary" type="button" (click)="loadItems()" [disabled]="loading() || busy()">
                {{ loading() ? 'Actualisation…' : 'Actualiser' }}
              </button>
            </div>
            @if (loading() && items().length === 0) {
              <p class="foods-empty-inline" role="status">Chargement des produits…</p>
            } @else if (items().length === 0) {
              <div class="foods-empty">
                <h3>Votre catalogue ne contient pas encore de produits</h3>
                <p>Créez un produit ou recherchez-le dans Open Food Facts ci-dessus.</p>
              </div>
            } @else {
              <ul class="foods-list">
                @for (item of visibleItems(); track item.id) {
                  <li class="foods-item">
                    <div class="foods-item-copy">
                      <div class="foods-item-title">
                        <h3>{{ item.name }}</h3>
                        <span class="foods-source" [class.is-off]="isOffItem(item)">
                          {{ sourceLabel(item) }}
                        </span>
                        @if (item.isCorrectionOf) {
                          <span class="foods-correction">Correction</span>
                        }
                      </div>
                      <p>Nutrition@if (item.referenceUnit) { / {{ item.referenceUnit }} } : {{ nutritionSummary(item.nutrition) }}</p>
                      @if (item.isArchived) { <p>Archivé — historique conservé</p> }
                      <p>Achats : {{ item.purchaseUnitConfirmed ? unitLabel(item.unit) : 'unité à préciser' }} · {{ item.priceHistory.length }} prix relevé(s)</p>
                      @if (item.legacyPurchaseName) { <small>Ancien nom d’achat conservé : {{ item.legacyPurchaseName }}</small> }
                      @if (item.migrationOrigin === 'food-only' || item.migrationOrigin === 'article-only') {
                        <small>Entrée historique sans association : conservée séparément, jamais fusionnée par nom.</small>
                      }
                      @if (item.offBarcode) {
                        <small>Code-barres : {{ item.offBarcode }}</small>
                      }
                      @if (item.isCorrectionOf) {
                        <small>Corrige l’aliment {{ item.isCorrectionOf }}</small>
                      }
                    </div>
                    <div class="foods-item-actions">
                      <button class="foods-secondary" type="button" [disabled]="busy()"
                        (click)="editFood(item)">Modifier</button>
                      <button class="foods-secondary" type="button" [disabled]="busy()"
                        (click)="startCorrection(item)">Corriger</button>
                      @if (deleteConfirmationId() === item.id) {
                        <button class="foods-danger" type="button" [disabled]="busy()"
                          (click)="deleteFood(item)">Confirmer l’archivage</button>
                        <button class="foods-secondary" type="button"
                          (click)="deleteConfirmationId.set('')">Conserver</button>
                      } @else {
                        <button class="foods-danger" type="button" [disabled]="busy()"
                          (click)="deleteConfirmationId.set(item.id)">Archiver</button>
                      }
                    </div>
                  </li>
                }
              </ul>
            }
          </section>
      </div>
    </section>

    <app-lifeos-dialog [open]="editorOpen()" [heading]="editorHeading()"
      [description]="correctionOf() ? 'Nouveau produit corrigé, lié à l’original. Ses anciens prix restent sur l’original.' : 'Informations générales, nutrition facultative et achats du même produit.'"
      formId="food-dialog-form" [submitLabel]="editingId() || correctionOf() ? 'Enregistrer' : 'Créer'"
      [busy]="busy()" [dirty]="isEditorDirty()" [error]="dialogError()" (dismissed)="closeEditor()">
      <form id="food-dialog-form" class="dialog-form" ngNativeValidate (ngSubmit)="saveFood()">
        <label class="dialog-field">
          <span>Nom</span>
          <input class="text-input" name="foodName" type="text" autocomplete="off" required maxlength="180" [(ngModel)]="editor.name" />
        </label>
        <label class="dialog-field">
          <span>Description (facultatif)</span>
          <textarea class="text-input" name="description" rows="2" maxlength="1000" [(ngModel)]="editor.description"></textarea>
        </label>
        <h2 class="product-section-title">Nutrition</h2>
        <p class="dialog-hint">Laissez les valeurs vides pour un produit sans nutrition. L’unité décrit exactement la quantité de ces valeurs, pas l’unité d’achat.</p>
        <div class="dialog-row">
          <label class="dialog-field">
            <span>Quantité de référence nutritionnelle</span>
            <input class="text-input" name="referenceUnit" type="text" maxlength="50" placeholder="100 g, 100 ml, 1 pièce" [(ngModel)]="editor.referenceUnit" />
          </label>
          <label class="dialog-field">
            <span>Calories (kcal)</span>
            <input class="text-input" name="calories" type="number" min="0" step="0.1" inputmode="decimal" [(ngModel)]="editor.caloriesPerUnit" />
          </label>
        </div>
        <details class="dialog-more" [open]="showMacros">
          <summary>Protéines, glucides, lipides</summary>
          <div class="dialog-row">
            <label class="dialog-field"><span>Protéines (g)</span>
              <input class="text-input" name="protein" type="number" min="0" step="0.1" inputmode="decimal" [(ngModel)]="editor.proteinsPerUnit" /></label>
            <label class="dialog-field"><span>Glucides (g)</span>
              <input class="text-input" name="carbs" type="number" min="0" step="0.1" inputmode="decimal" [(ngModel)]="editor.carbsPerUnit" /></label>
            <label class="dialog-field"><span>Lipides (g)</span>
              <input class="text-input" name="fat" type="number" min="0" step="0.1" inputmode="decimal" [(ngModel)]="editor.fatsPerUnit" /></label>
          </div>
        </details>
        <h2 class="product-section-title">Achats</h2>
        <label class="dialog-field">
          <span>Unité d’achat et de prix</span>
          <select class="text-input" name="purchaseUnit" required [(ngModel)]="editor.unit"
            [disabled]="(currentProduct()?.priceHistory?.length ?? 0) > 0">
            <option value="" disabled>À préciser</option>
            <option value="unit">À l’unité</option><option value="kilogram">Au kilogramme</option><option value="liter">Au litre</option>
          </select>
        </label>
        <p class="dialog-hint">Aucune conversion entre pièces, masse et volume. L’unité d’achat est figée après le premier prix pour ne pas réinterpréter l’historique.</p>
      </form>
      <section class="product-prices" aria-labelledby="product-prices-title">
        <h2 id="product-prices-title" class="product-section-title">Prix par magasin</h2>
        @if (priceNotice()) { <p class="dialog-hint" role="status">{{ priceNotice() }}</p> }
        @if (currentProduct(); as product) {
          @if (product.priceHistory.length === 0) { <p class="dialog-hint">Aucun prix relevé.</p> }
          <ul class="foods-list">
            @for (entry of product.priceHistory; track entry.id) {
              <li class="foods-item">
                <span>{{ storeLabel(entry.storeId) }} · {{ formatNumber(entry.price) }} € / {{ unitLabel(product.unit) }} · {{ formatDate(entry.observedAt) }}</span>
                <button type="button" class="foods-danger" [disabled]="busy()"
                  [attr.aria-label]="'Supprimer le relevé de ' + storeLabel(entry.storeId)"
                  (click)="removePrice(product, entry)">Supprimer le relevé</button>
              </li>
            }
          </ul>
          @if (!product.isArchived && product.purchaseUnitConfirmed) {
            <form class="dialog-form" ngNativeValidate (ngSubmit)="addPrice(product)">
              <label class="dialog-field"><span>Magasin</span>
                <select class="text-input" name="priceStore" required [(ngModel)]="priceDraft.storeId">
                  <option value="" disabled>Choisir un magasin</option>
                  @for (store of stores(); track store.id) { <option [value]="store.id">{{ store.name }}</option> }
                </select>
              </label>
              <div class="dialog-row">
                <label class="dialog-field"><span>Prix (€ / {{ unitLabel(product.unit) }})</span>
                  <input class="text-input" name="priceAmount" type="number" min="0" max="99999999.99" step="0.01" required [(ngModel)]="priceDraft.amount" /></label>
                <label class="dialog-field"><span>Relevé le</span>
                  <input class="text-input" name="priceDate" type="date" required [(ngModel)]="priceDraft.observedAt" /></label>
              </div>
              <button class="lifeos-button lifeos-button-secondary" type="submit" [disabled]="busy() || !stores().length || hasUnsavedProductChanges()">Ajouter ce prix</button>
              @if (hasUnsavedProductChanges()) { <p class="dialog-hint">Enregistrez les modifications de la fiche avant de relever un prix.</p> }
              @if (!stores().length) { <p class="dialog-hint">Créez d’abord un <a routerLink="/stores">magasin</a>.</p> }
            </form>
          } @else if (!product.purchaseUnitConfirmed) {
            <p class="dialog-hint">Enregistrez une unité d’achat pour relever un prix.</p>
          }
        } @else { <p class="dialog-hint">Enregistrez le produit pour commencer son historique de prix. Aucun article séparé à créer.</p> }
      </section>
      @if (currentProduct()) {
        <section class="product-prices" aria-labelledby="product-usage-title">
          <h2 id="product-usage-title" class="product-section-title">Usages et historique</h2>
          @if (usageLoading()) { <p class="dialog-hint" role="status">Chargement des usages…</p> }
          @if (usageError()) { <p class="foods-error" role="alert">{{ usageError() }}</p> }
          @if (usage(); as value) {
            <p class="dialog-hint">{{ value.mealOccurrences }} événement(s) figé(s) · {{ value.historicalShoppingLines }} ligne(s) de courses historique(s).
              Modifier cette fiche ne réécrit pas les événements.</p>
            <p><a routerLink="/recipes">{{ value.recipes.length }} recette(s)</a> · <a routerLink="/stock">Stock et courses</a> · <a routerLink="/planning">Semainier</a></p>
            @for (recipe of value.recipes; track recipe.id) { <p>{{ recipe.name }}{{ recipe.isArchived ? ' (archivée)' : '' }}</p> }
            @for (stock of value.stock; track $index) { <p>Stock disponible : {{ formatNumber(stock.quantity) }} {{ stock.unit }}</p> }
          }
        </section>
      }
    </app-lifeos-dialog>
  `,
  styles: [`
    :host { display: block; }
    .product-section-title { margin: 12px 0 0; font-size: 1.05rem; }
    .product-prices { display: grid; gap: 14px; margin-top: 24px; padding-top: 18px; border-top: 1px solid var(--lifeos-line); }
    .foods-page { --food-line: var(--lifeos-line); }
    .foods-count { display: flex; align-items: baseline; gap: 8px; padding: 12px 16px; border-radius: 999px; background: var(--lifeos-accent-soft); color: var(--lifeos-accent-strong); }
    .foods-count strong { font-size: 1.3rem; }
    .foods-count span { font-size: .84rem; font-weight: 750; }
    .foods-hero-actions { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: 12px; }
    .foods-search-block, .foods-catalog { display: grid; gap: 18px; min-width: 0; padding: 22px; border-radius: 22px; background: var(--lifeos-surface); box-shadow: var(--lifeos-shadow); }
    .foods-main-column { display: grid; gap: 22px; min-width: 0; }
    .foods-section-heading h2, .foods-empty h3, .foods-item h3 { margin: 0; }
    .foods-section-heading p { margin: 7px 0 0; color: var(--lifeos-text-soft); font-size: .9rem; line-height: 1.5; }
    .foods-field { display: grid; gap: 7px; min-width: 0; }
    .foods-field > span { color: var(--lifeos-text-soft); font-size: .82rem; font-weight: 800; }
    .foods-field input { width: 100%; min-height: 2.7rem; padding: .65rem .75rem; border: 1px solid var(--lifeos-line); border-radius: 10px; background: var(--lifeos-surface); color: var(--lifeos-text); font: inherit; }
    .foods-field input:focus-visible { outline: 3px solid rgb(53 109 79 / 28%); border-color: var(--lifeos-accent); }
    .foods-item-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .foods-secondary, .foods-danger { min-height: 40px; padding: 9px 13px; border: 1px solid var(--lifeos-line); border-radius: 10px; background: var(--lifeos-surface-soft); color: var(--lifeos-text); font: inherit; font-weight: 750; cursor: pointer; }
    .foods-danger { color: #8b2c20; }
    .foods-secondary:hover:not(:disabled), .foods-danger:hover:not(:disabled) { filter: brightness(.96); }
    .foods-secondary:focus-visible, .foods-danger:focus-visible { outline: 3px solid rgb(53 109 79 / 28%); outline-offset: 2px; }
    button:disabled { cursor: wait; opacity: .65; }
    .foods-section-heading { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 14px; }
    .foods-searches { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; }
    .foods-search-form { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: end; gap: 8px; }
    .foods-results, .foods-list { display: grid; margin: 0; padding: 0; list-style: none; }
    .foods-results { gap: 8px; }
    .foods-result, .foods-item { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 15px 0; border-top: 1px solid var(--lifeos-line); }
    .foods-result { padding: 12px; border: 1px solid var(--lifeos-line); border-radius: 13px; }
    .foods-item-copy { min-width: 0; overflow-wrap: anywhere; }
    .foods-item-copy p { margin: 5px 0 0; color: var(--lifeos-text-soft); font-size: .9rem; }
    .foods-item-copy small { display: block; margin-top: 4px; color: var(--lifeos-text-soft); }
    .foods-item-title { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .foods-source, .foods-correction { padding: 3px 8px; border-radius: 999px; background: var(--lifeos-surface-muted); color: var(--lifeos-text-soft); font-size: .72rem; font-weight: 800; }
    .foods-source.is-off { background: var(--lifeos-accent-soft); color: var(--lifeos-accent-strong); }
    .foods-correction { background: #f4ead7; color: #705123; }
    .foods-feedback { margin: 0; padding: 12px 14px; border-radius: 12px; font-weight: 700; }
    .foods-error { background: #fff0ed; color: #8b2c20; }
    .foods-notice { background: var(--lifeos-accent-soft); color: var(--lifeos-accent-strong); }
    .foods-search-notice { margin: 0; padding: 12px 14px; border-radius: 11px; background: var(--lifeos-accent-soft); color: var(--lifeos-accent-strong); font-size: .9rem; line-height: 1.5; }
    .foods-empty, .foods-empty-inline { color: var(--lifeos-text-soft); line-height: 1.5; }
    .foods-empty { padding: 28px 4px 8px; }
    .foods-empty p { margin: 7px 0 0; }
    .foods-empty-inline { margin: 0; font-size: .9rem; }
    @media (max-width: 620px) {
      .foods-hero { flex-wrap: wrap; align-items: flex-start; }
      .foods-hero-actions { justify-content: flex-start; }
      .foods-searches { grid-template-columns: 1fr; }
      .foods-item, .foods-result { align-items: flex-start; flex-direction: column; }
      .foods-item-actions { width: 100%; }
      .foods-section-heading { align-items: flex-start; }
      .foods-search-form { grid-template-columns: 1fr; }
      .foods-search-form button { justify-self: start; }
    }
  `],
})
export class FoodItemsPageComponent {
  private readonly api = inject(LifeosApiService)

  readonly items = signal<FoodItemDto[]>([])
  readonly stores = signal<StoreDto[]>([])
  catalogSearch = ''
  showArchived = false
  readonly currentProduct = signal<FoodItemDto | null>(null)
  readonly usage = signal<ProductUsageDto | null>(null)
  readonly usageLoading = signal(false)
  readonly usageError = signal('')
  priceDraft: { storeId: string; amount: number | null; observedAt: string } = { storeId: '', amount: null, observedAt: this.today() }
  readonly priceNotice = signal('')
  private initialPriceDraft = formSnapshot(this.priceDraft)
  readonly editorOpen = signal(false)
  readonly dialogError = signal('')
  showMacros = false
  private initialEditor = formSnapshot(emptyEditor())
  visibleItems(): FoodItemDto[] {
    return this.items().filter(i => (this.showArchived || !i.isArchived)
      && i.name.toLocaleLowerCase('fr').includes(this.catalogSearch.toLocaleLowerCase('fr')))
  }
  readonly activeCount = computed(() => this.items().filter(i => !i.isArchived).length)
  readonly searchResults = signal<FoodItemDto[]>([])
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly searchingName = signal(false)
  readonly searchingBarcode = signal(false)
  readonly searched = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly editingId = signal('')
  readonly correctionOf = signal('')
  readonly deleteConfirmationId = signal('')

  editor = emptyEditor()
  offNameSearch = ''
  offBarcodeSearch = ''

  constructor() {
    void this.loadItems()
    this.api.get<StoreDto[]>('/stores').subscribe({
      next: stores => this.stores.set(stores),
      error: (error: unknown) => this.error.set(apiErrorMessage(error)),
    })
  }

  async addPrice(item: FoodItemDto): Promise<void> {
    if (this.busy()) return
    if (this.hasUnsavedProductChanges()) {
      this.dialogError.set('Enregistrez la fiche avant de relever un prix.')
      return
    }
    if (!this.priceDraft.storeId || this.priceDraft.amount === null || !Number.isFinite(this.priceDraft.amount)
      || this.priceDraft.amount < 0 || !this.priceDraft.observedAt) {
      this.dialogError.set('Choisissez un magasin, une date et un prix positif ou nul.')
      return
    }
    this.busy.set(true)
    this.dialogError.set('')
    this.priceNotice.set('')
    let persisted = false
    try {
      await firstValueFrom(this.api.post(`/products/${item.id}/price-entries`, {
        storeId: this.priceDraft.storeId, price: this.priceDraft.amount,
        observedAt: new Date(`${this.priceDraft.observedAt}T12:00:00`).toISOString(),
      }))
      persisted = true
      this.priceDraft = { storeId: this.priceDraft.storeId, amount: null, observedAt: this.today() }
      this.initialPriceDraft = formSnapshot(this.priceDraft)
      await this.refreshProduct(item.id)
      this.priceNotice.set('Prix ajouté à l’historique.')
    } catch (error) {
      this.dialogError.set(`${persisted ? 'Prix ajouté, mais l’actualisation a échoué. ' : ''}${apiErrorMessage(error)}`)
    } finally { this.busy.set(false) }
  }

  async removePrice(item: FoodItemDto, entry: ArticlePriceEntryDto): Promise<void> {
    this.busy.set(true)
    this.dialogError.set('')
    this.priceNotice.set('')
    let persisted = false
    try {
      await firstValueFrom(this.api.delete(`/products/${item.id}/price-entries/${entry.id}`))
      persisted = true
      await this.refreshProduct(item.id)
      this.priceNotice.set('Relevé supprimé.')
    } catch (error) { this.dialogError.set(`${persisted ? 'Relevé supprimé, mais l’actualisation a échoué. ' : ''}${apiErrorMessage(error)}`) }
    finally { this.busy.set(false) }
  }

  private async refreshProduct(id: string): Promise<void> {
    const product = await firstValueFrom(this.api.get<FoodItemDto>(`/products/${id}`))
    this.currentProduct.set(product)
    this.items.update(items => items.map(item => item.id === id ? product : item))
    this.searchResults.update(items => items.map(item => item.id === id ? product : item))
  }

  unitLabel(unit: GroceryItemUnit): string {
    return unit === 'kilogram' ? 'kg' : unit === 'liter' ? 'L' : 'unité'
  }

  storeLabel(id: string): string {
    return this.stores().find(store => store.id === id)?.name ?? `Magasin historique (${id})`
  }

  formatDate(value: string): string { return new Date(value).toLocaleDateString('fr-FR') }

  private today(): string {
    const date = new Date()
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
  }

  async loadItems(): Promise<void> {
    this.loading.set(true)
    try {
      this.items.set(await firstValueFrom(this.api.get<FoodItemDto[]>('/products')))
      this.error.set('')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  async searchByName(): Promise<void> {
    const name = this.offNameSearch.trim()
    if (!name) return
    this.busy.set(true)
    this.searchingName.set(true)
    this.error.set('')
    this.notice.set('')
    this.searched.set(false)
    this.searchResults.set([])
    try {
      this.searchResults.set(await firstValueFrom(
        this.api.get<FoodItemDto[]>('/products/search-off', { name }),
      ))
      this.searched.set(true)
      await this.loadItems()
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
      this.searchingName.set(false)
    }
  }

  async searchByBarcode(): Promise<void> {
    const barcode = this.offBarcodeSearch.trim()
    if (!barcode) return
    this.busy.set(true)
    this.searchingBarcode.set(true)
    this.error.set('')
    this.notice.set('')
    this.searched.set(false)
    this.searchResults.set([])
    try {
      const result = await firstValueFrom(
        this.api.get<FoodItemDto>('/products/search-off-barcode', { barcode }),
      )
      this.searchResults.set([result])
      this.searched.set(true)
      await this.loadItems()
    } catch (error) {
      this.searchResults.set([])
      this.error.set(apiErrorMessage(error))
      this.searched.set(true)
    } finally {
      this.busy.set(false)
      this.searchingBarcode.set(false)
    }
  }

  editorHeading(): string {
    return this.correctionOf() ? 'Créer un produit corrigé' : this.editingId() ? 'Fiche produit' : 'Nouveau produit'
  }

  openCreate(): void { this.currentProduct.set(null); this.openEditor('', '', emptyEditor()) }

  editFood(item: FoodItemDto): void {
    this.currentProduct.set(item)
    this.openEditor(item.id, '', this.editorFromItem(item))
    void this.loadUsage(item.id)
  }

  private async loadUsage(id: string): Promise<void> {
    this.usage.set(null)
    this.usageError.set('')
    this.usageLoading.set(true)
    try {
      const usage = await firstValueFrom(this.api.get<ProductUsageDto>(`/products/${id}/usage`))
      if (this.currentProduct()?.id === id) this.usage.set(usage)
    } catch (error) {
      if (this.currentProduct()?.id === id) this.usageError.set(apiErrorMessage(error))
    } finally {
      if (this.currentProduct()?.id === id) this.usageLoading.set(false)
    }
  }

  startCorrection(item: FoodItemDto): void { this.currentProduct.set(null); this.openEditor('', item.id, this.editorFromItem(item)) }

  hasUnsavedProductChanges(): boolean { return hasChanges(this.initialEditor, this.editor) }
  isEditorDirty(): boolean { return this.hasUnsavedProductChanges() || hasChanges(this.initialPriceDraft, this.priceDraft) }

  closeEditor(): void { this.editorOpen.set(false); this.dialogError.set(''); this.resetEditor() }

  private openEditor(editingId: string, correctionOf: string, editor: ProductEditor): void {
    this.editingId.set(editingId)
    this.correctionOf.set(correctionOf)
    this.editor = editor
    this.priceDraft = { storeId: '', amount: null, observedAt: this.today() }
    this.initialPriceDraft = formSnapshot(this.priceDraft)
    this.priceNotice.set('')
    this.initialEditor = formSnapshot(editor)
    this.showMacros = [editor.proteinsPerUnit, editor.carbsPerUnit, editor.fatsPerUnit].some(v => v !== null)
    this.dialogError.set('')
    this.notice.set('')
    this.editorOpen.set(true)
  }

  async saveFood(): Promise<void> {
    const request = this.toRequest()
    const problem = productDraftProblem(request)
    if (problem) {
      this.dialogError.set(problem)
      return
    }

    this.busy.set(true)
    this.dialogError.set('')
    this.notice.set('')
    let persisted = false
    try {
      let saved: FoodItemDto
      if (this.correctionOf()) {
        saved = await firstValueFrom(this.api.post<FoodItemDto>(
          `/products/${this.correctionOf()}/correction`,
          request,
        ))
      } else if (this.editingId()) {
        saved = await firstValueFrom(this.api.put<FoodItemDto>(
          `/products/${this.editingId()}`,
          request,
        ))
      } else {
        saved = await firstValueFrom(this.api.post<FoodItemDto>('/products', request))
      }
      persisted = true
      this.currentProduct.set(saved)
      this.editingId.set(saved.id)
      this.correctionOf.set('')
      this.editor = this.editorFromItem(saved)
      this.initialEditor = formSnapshot(this.editor)
      void this.loadUsage(saved.id)
      await this.reloadAfterMutation('Produit enregistré. Nutrition et achats réunis ; vous pouvez relever ses prix dans cette fiche.')
    } catch (error) {
      if (persisted) this.error.set(`Produit enregistré, mais l’actualisation a échoué. ${apiErrorMessage(error)}`)
      else this.dialogError.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async deleteFood(item: FoodItemDto): Promise<void> {
    this.busy.set(true)
    this.error.set('')
    this.notice.set('')
    try {
      await firstValueFrom(this.api.delete<void>(`/products/${item.id}`))
      this.deleteConfirmationId.set('')
      await this.reloadAfterMutation(`Le produit « ${item.name} » a été archivé. Les prix et événements sont conservés.`)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  resetEditor(): void {
    this.editor = emptyEditor()
    this.editingId.set('')
    this.correctionOf.set('')
  }

  nutritionSummary(nutrition: NutritionPerUnitDto | null): string {
    if (!nutrition) return 'Valeurs nutritionnelles non renseignées'
    const values = [
      nutrition.caloriesPerUnit === null ? null : `${this.formatNumber(nutrition.caloriesPerUnit)} kcal`,
      nutrition.proteinsPerUnit === null ? null : `${this.formatNumber(nutrition.proteinsPerUnit)} g de protéines`,
      nutrition.carbsPerUnit === null ? null : `${this.formatNumber(nutrition.carbsPerUnit)} g de glucides`,
      nutrition.fatsPerUnit === null ? null : `${this.formatNumber(nutrition.fatsPerUnit)} g de lipides`,
    ].filter((value): value is string => value !== null)
    return values.length ? values.join(' · ') : 'Valeurs nutritionnelles non renseignées'
  }

  isOffItem(item: FoodItemDto): boolean {
    return Boolean(item.offBarcode) || item.source.toLowerCase().includes('openfoodfacts')
      || item.source.toLowerCase().includes('open food facts')
  }

  sourceLabel(item: FoodItemDto): string {
    if (item.source.toLowerCase().includes('correction')) return 'Correction'
    if (this.isOffItem(item)) return 'Open Food Facts'
    if (!item.source || item.source.toLowerCase() === 'manual') return 'Saisie manuelle'
    return item.source
  }

  private editorFromItem(item: FoodItemDto): ProductEditor { return productEditorFromItem(item) }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 2 }).format(value)
  }

  private toRequest(): FoodItemRequest {
    return productRequest(this.editor)
  }

  private async reloadAfterMutation(message: string): Promise<void> {
    this.items.set(await firstValueFrom(this.api.get<FoodItemDto[]>('/products')))
    this.error.set('')
    this.notice.set(message)
  }
}
