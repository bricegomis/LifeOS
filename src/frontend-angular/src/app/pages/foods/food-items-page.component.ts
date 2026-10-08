import { firstValueFrom } from 'rxjs'
import { Component, computed, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import {
  FoodItemDto,
  FoodItemRequest,
  NutritionPerUnitDto,
  ArticleDto,
  GroceryItemUnit,
} from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'

type FoodEditor = {
  name: string
  referenceUnit: string
  caloriesPerUnit: number | null
  proteinsPerUnit: number | null
  carbsPerUnit: number | null
  fatsPerUnit: number | null
}

const emptyEditor = (): FoodEditor => ({
  name: '',
  referenceUnit: '100 g',
  caloriesPerUnit: null,
  proteinsPerUnit: null,
  carbsPerUnit: null,
  fatsPerUnit: null,
})

@Component({
  selector: 'app-food-items-page',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent],
  template: `
    <section class="page-stack foods-page" aria-labelledby="foods-page-title">
      <header class="page-hero foods-hero">
        <div>
          <h1 id="foods-page-title">Catalogue alimentaire</h1>
          <p>Retrouvez des aliments et leurs valeurs nutritionnelles pour planifier vos repas.</p>
        </div>
        <div class="foods-hero-actions">
          <div class="foods-count" aria-label="Nombre d’aliments dans le catalogue">
            <strong>{{ items().length }}</strong>
            <span>{{ items().length === 1 ? 'aliment' : 'aliments' }}</span>
          </div>
          <button class="lifeos-button" type="button" [disabled]="busy()" (click)="openCreate()">
            <i class="pi pi-plus" aria-hidden="true"></i> Ajouter un aliment
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
              Une recherche dans Open Food Facts peut ajouter automatiquement un aliment au catalogue de votre foyer.
              Les résultats sont déjà enregistrés : sélectionnez-en un pour modifier ses informations.
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
                <h2 id="catalog-heading">Vos aliments</h2>
                <p>Les aliments disponibles pour la planification de vos repas.</p>
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
              <p class="foods-empty-inline" role="status">Chargement du catalogue alimentaire…</p>
            } @else if (items().length === 0) {
              <div class="foods-empty">
                <h3>Votre catalogue ne contient pas encore d’aliments</h3>
                <p>Ajoutez un aliment avec le bouton « Ajouter un aliment » ou recherchez-le dans Open Food Facts ci-dessus.</p>
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
                      <p>{{ item.referenceUnit }} · {{ nutritionSummary(item.nutrition) }}</p>
                      @if (item.isArchived) { <p>Archivé — historique conservé</p> }
                      <p>Article d’achat : {{ articleName(item.articleId) }}</p>
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
                      @if (!item.isArchived) {
                        <button class="foods-secondary" type="button" [disabled]="busy()"
                          (click)="openPurchase(item)">Achats</button>
                      }
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
      [description]="correctionOf() ? 'La correction sera liée à l’aliment d’origine.' : 'Valeurs nutritionnelles par unité de référence.'"
      formId="food-dialog-form" [submitLabel]="editingId() || correctionOf() ? 'Enregistrer' : 'Créer'"
      [busy]="busy()" [dirty]="isEditorDirty()" [error]="dialogError()" (dismissed)="closeEditor()">
      <form id="food-dialog-form" class="dialog-form" ngNativeValidate (ngSubmit)="saveFood()">
        <label class="dialog-field">
          <span>Nom</span>
          <input class="text-input" name="foodName" type="text" autocomplete="off" required maxlength="180" [(ngModel)]="editor.name" />
        </label>
        <div class="dialog-row">
          <label class="dialog-field">
            <span>Unité de référence</span>
            <input class="text-input" name="referenceUnit" type="text" required maxlength="40" placeholder="100 g, 1 tasse, 1 pièce" [(ngModel)]="editor.referenceUnit" />
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
      </form>
    </app-lifeos-dialog>

    <app-lifeos-dialog [open]="purchaseItem() !== null" heading="Article d’achat"
      [description]="purchaseItem()?.name ?? ''" formId="food-purchase-form" submitLabel="Enregistrer le raccord"
      [busy]="busy()" [dirty]="isPurchaseDirty()" [error]="dialogError()" (dismissed)="closePurchase()">
      @if (purchaseItem(); as item) {
        <form id="food-purchase-form" class="dialog-form" ngNativeValidate (ngSubmit)="linkArticle(item)">
          <p class="dialog-hint">Actuellement : {{ articleName(item.articleId) }}. Les prix et achats existants sont conservés.</p>
          <label class="dialog-field"><span>Raccord explicite aux achats</span>
            <select class="text-input" name="articleChoice" [(ngModel)]="purchaseArticleId">
              <option value="">Sans raccord</option>
              @for (article of articles(); track article.id) { <option [value]="article.id">{{ article.name }}</option> }
            </select>
          </label>
          @if (!item.articleId) {
            <details class="dialog-more">
              <summary>Aucun article ne convient ? En créer un</summary>
              <div>
                <label class="dialog-field"><span>Unité d’achat du nouvel article « {{ item.name }} »</span>
                  <select class="text-input" name="purchaseUnit" [(ngModel)]="purchaseUnitChoice">
                    <option value="unit">À l’unité</option><option value="kilogram">Au kilogramme</option><option value="liter">Au litre</option>
                  </select>
                </label>
                <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="createArticle(item)">Créer l’article et le relier</button>
              </div>
            </details>
          }
        </form>
      }
    </app-lifeos-dialog>
  `,
  styles: [`
    :host { display: block; }
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
    .foods-section-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 14px; }
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
  readonly articles = signal<ArticleDto[]>([])
  catalogSearch = ''
  showArchived = false
  purchaseArticleId = ''
  purchaseUnitChoice: GroceryItemUnit = 'unit'
  readonly purchaseItem = signal<FoodItemDto | null>(null)
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
    this.api.get<ArticleDto[]>('/articles').subscribe({
      next: articles => this.articles.set(articles),
      error: (error: unknown) => this.error.set(apiErrorMessage(error)),
    })
  }

  articleName(id: string | null): string {
    return this.articles().find(a => a.id === id)?.name ?? 'Non relié'
  }

  openPurchase(item: FoodItemDto): void {
    this.dialogError.set(''); this.notice.set('')
    this.purchaseArticleId = item.articleId ?? ''
    this.purchaseUnitChoice = 'unit'
    this.purchaseItem.set(item)
  }

  closePurchase(): void { this.purchaseItem.set(null); this.dialogError.set('') }

  isPurchaseDirty(): boolean {
    const item = this.purchaseItem()
    return Boolean(item && this.purchaseArticleId !== (item.articleId ?? ''))
  }

  async linkArticle(item: FoodItemDto): Promise<void> {
    this.busy.set(true)
    this.dialogError.set('')
    try {
      const updated = await firstValueFrom(this.api.put<FoodItemDto>(`/food-items/${item.id}/article`,
        { articleId: this.purchaseArticleId || null }))
      this.items.update(items => items.map(i => i.id === item.id ? updated : i))
      this.closePurchase()
      this.notice.set('Raccord enregistré. Les prix et achats sont conservés.')
    } catch (error) { this.dialogError.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  async createArticle(item: FoodItemDto): Promise<void> {
    if (this.busy()) return
    this.busy.set(true)
    this.dialogError.set('')
    try {
      const article = await firstValueFrom(this.api.post<ArticleDto>('/articles',
        { name: item.name, description: '', unit: this.purchaseUnitChoice }))
      this.articles.update(items => [...items, article])
      this.purchaseArticleId = article.id
    } catch (error) {
      this.dialogError.set(apiErrorMessage(error))
      this.busy.set(false)
      return
    }
    await this.linkArticle(item)
  }

  async loadItems(): Promise<void> {
    this.loading.set(true)
    try {
      this.items.set(await firstValueFrom(this.api.get<FoodItemDto[]>('/food-items')))
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
        this.api.get<FoodItemDto[]>('/food-items/search-off', { name }),
      ))
      this.searched.set(true)
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
        this.api.get<FoodItemDto>('/food-items/search-off-barcode', { barcode }),
      )
      this.searchResults.set([result])
      this.searched.set(true)
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
    return this.correctionOf() ? 'Corriger un aliment' : this.editingId() ? 'Modifier un aliment' : 'Nouvel aliment'
  }

  openCreate(): void { this.openEditor('', '', emptyEditor()) }

  editFood(item: FoodItemDto): void { this.openEditor(item.id, '', this.editorFromItem(item)) }

  startCorrection(item: FoodItemDto): void { this.openEditor('', item.id, this.editorFromItem(item)) }

  isEditorDirty(): boolean { return hasChanges(this.initialEditor, this.editor) }

  closeEditor(): void { this.editorOpen.set(false); this.dialogError.set(''); this.resetEditor() }

  private openEditor(editingId: string, correctionOf: string, editor: FoodEditor): void {
    this.editingId.set(editingId)
    this.correctionOf.set(correctionOf)
    this.editor = editor
    this.initialEditor = formSnapshot(editor)
    this.showMacros = [editor.proteinsPerUnit, editor.carbsPerUnit, editor.fatsPerUnit].some(v => v !== null)
    this.dialogError.set('')
    this.notice.set('')
    this.editorOpen.set(true)
  }

  async saveFood(): Promise<void> {
    const request = this.toRequest()
    if (!request.name.trim() || !request.referenceUnit.trim()) {
      this.dialogError.set('Indiquez un nom et une unité de référence.')
      return
    }

    this.busy.set(true)
    this.dialogError.set('')
    this.notice.set('')
    let persisted = false
    try {
      if (this.correctionOf()) {
        await firstValueFrom(this.api.post<FoodItemDto>(
          `/food-items/${this.correctionOf()}/correction`,
          request,
        ))
      } else if (this.editingId()) {
        await firstValueFrom(this.api.put<FoodItemDto>(
          `/food-items/${this.editingId()}`,
          request,
        ))
      } else {
        await firstValueFrom(this.api.post<FoodItemDto>('/food-items', request))
      }
      persisted = true
      this.closeEditor()
      await this.reloadAfterMutation('Aliment enregistré dans votre catalogue.')
    } catch (error) {
      if (persisted) this.error.set(`Aliment enregistré, mais l’actualisation a échoué. ${apiErrorMessage(error)}`)
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
      await firstValueFrom(this.api.delete<void>(`/food-items/${item.id}`))
      this.deleteConfirmationId.set('')
      await this.reloadAfterMutation(`L’aliment « ${item.name} » a été archivé. Les événements sont conservés.`)
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

  private editorFromItem(item: FoodItemDto): FoodEditor {
    return {
      name: item.name,
      referenceUnit: item.referenceUnit,
      caloriesPerUnit: item.nutrition?.caloriesPerUnit ?? null,
      proteinsPerUnit: item.nutrition?.proteinsPerUnit ?? null,
      carbsPerUnit: item.nutrition?.carbsPerUnit ?? null,
      fatsPerUnit: item.nutrition?.fatsPerUnit ?? null,
    }
  }

  private formatNumber(value: number): string {
    return new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 1 }).format(value)
  }

  private toRequest(): FoodItemRequest {
    const macroValues = [
      this.editor.caloriesPerUnit,
      this.editor.proteinsPerUnit,
      this.editor.carbsPerUnit,
      this.editor.fatsPerUnit,
    ]
    const hasNutrition = macroValues.some((value) => value !== null && value !== undefined)
    const nutrition = hasNutrition ? {
      caloriesPerUnit: this.editor.caloriesPerUnit,
      proteinsPerUnit: this.editor.proteinsPerUnit,
      carbsPerUnit: this.editor.carbsPerUnit,
      fatsPerUnit: this.editor.fatsPerUnit,
    } : null
    return {
      name: this.editor.name.trim(),
      referenceUnit: this.editor.referenceUnit.trim(),
      nutrition,
    }
  }

  private async reloadAfterMutation(message: string): Promise<void> {
    this.items.set(await firstValueFrom(this.api.get<FoodItemDto[]>('/food-items')))
    this.error.set('')
    this.notice.set(message)
  }
}
