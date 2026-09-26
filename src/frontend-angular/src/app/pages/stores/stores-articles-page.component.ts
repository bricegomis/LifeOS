import { firstValueFrom } from 'rxjs'
import { Component, inject, signal } from '@angular/core'
import { DecimalPipe } from '@angular/common'
import { FormsModule } from '@angular/forms'
import {
  AddPriceEntryRequest,
  ArticleDto,
  ArticleRequest,
  ArticlePriceEntryDto,
  GroceryItemUnit,
  StoreDto,
  StoreRequest,
} from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'

type StoreEditor = {
  name: string
  address: string
  isOrganic: boolean
  isLocal: boolean
}

type ArticleEditor = {
  name: string
  description: string
  unit: GroceryItemUnit
}

const emptyStoreEditor = (): StoreEditor => ({
  name: '',
  address: '',
  isOrganic: false,
  isLocal: false,
})

const emptyArticleEditor = (): ArticleEditor => ({
  name: '',
  description: '',
  unit: 'unit',
})

@Component({
  selector: 'app-stores-articles-page',
  standalone: true,
  imports: [DecimalPipe, FormsModule],
  template: `
    <section class="page-stack stores-page" aria-labelledby="stores-page-title">
      <header class="page-hero">
        <div>
          <h1 id="stores-page-title">Magasins et articles</h1>
          <p>Retrouvez les magasins où vous faites vos courses et les articles dont vous comparez les prix.</p>
        </div>
        <nav class="stores-page-nav" aria-label="Navigation dans la page">
          <a href="#stores-section">Magasins</a>
          <a href="#articles-section">Articles</a>
        </nav>
      </header>

      @if (error()) {
        <p class="stores-status stores-status-error" role="alert">{{ error() }}</p>
      }
      @if (notice()) {
        <p class="stores-status stores-status-success" role="status">{{ notice() }}</p>
      }

      <section id="stores-section" class="stores-area" aria-labelledby="stores-heading">
        <div class="stores-area-heading">
          <div>
            <h2 id="stores-heading">Magasins</h2>
            <p>Enregistrez les lieux où vous faites vos courses pour associer les prix aux bons magasins.</p>
          </div>
          <span class="stores-count">{{ filteredStores().length }} affiché{{ filteredStores().length > 1 ? 's' : '' }}</span>
        </div>

        <div class="stores-workspace">
          <form class="stores-form" (ngSubmit)="saveStore()">
            <div>
              <h2>{{ editingStoreId() ? 'Modifier un magasin' : 'Ajouter un magasin' }}</h2>
              <p>{{ editingStoreId() ? 'Modifiez les informations de ce magasin.' : 'Ajoutez les magasins où vous faites vos courses.' }}</p>
            </div>
            <label class="stores-field">
              <span>Nom du magasin</span>
              <input class="stores-native-input" name="storeName" type="text" required maxlength="160"
                [(ngModel)]="storeEditor.name" />
            </label>
            <label class="stores-field">
              <span>Adresse (facultatif)</span>
              <input class="stores-native-input" name="storeAddress" type="text" maxlength="300"
                [(ngModel)]="storeEditor.address" />
            </label>
            <fieldset class="stores-options">
              <legend>Caractéristiques du magasin</legend>
              <label><input name="isOrganic" type="checkbox" [(ngModel)]="storeEditor.isOrganic" /> Magasin bio</label>
              <label><input name="isLocal" type="checkbox" [(ngModel)]="storeEditor.isLocal" /> Commerce local</label>
            </fieldset>
            <div class="stores-form-actions">
              <button class="stores-native-button is-primary" type="submit" [disabled]="busy()">
                {{ busy() ? 'Enregistrement…' : editingStoreId() ? 'Enregistrer le magasin' : 'Ajouter le magasin' }}
              </button>
              @if (editingStoreId()) {
                <button class="stores-native-button" type="button" [disabled]="busy()" (click)="resetStoreEditor()">Annuler</button>
              }
            </div>
          </form>

          <section class="stores-directory" aria-label="Liste des magasins">
            <div class="stores-directory-heading">
              <div>
                <h2>Liste des magasins</h2>
                <p>{{ stores().length }} {{ stores().length === 1 ? 'magasin enregistré' : 'magasins enregistrés' }}</p>
              </div>
              <label class="stores-search">
                <span class="sr-only">Filtrer les magasins</span>
                <input class="stores-native-input" type="search" name="storeSearch" placeholder="Filtrer les magasins"
                  [(ngModel)]="storeSearch" />
              </label>
            </div>
            @if (loading() && stores().length === 0) {
              <p class="stores-empty-inline" role="status">Chargement des magasins…</p>
            } @else if (filteredStores().length === 0) {
              <div class="stores-empty">
                <div>
                  <h3>{{ storeSearch ? 'Aucun magasin correspondant' : 'Aucun magasin pour le moment' }}</h3>
                  <p>{{ storeSearch ? 'Essayez un autre nom ou effacez le filtre.' : 'Ajoutez un magasin pour commencer à relever les prix par lieu.' }}</p>
                </div>
              </div>
            } @else {
              <div class="stores-list">
                @for (store of filteredStores(); track store.id) {
                  <article class="store-row">
                    <span class="store-row-icon" aria-hidden="true">⌂</span>
                    <div class="store-row-content">
                      <h3>{{ store.name }}</h3>
                      @if (store.address) {
                        <p>{{ store.address }}</p>
                      }
                      <div class="store-badges">
                        @if (store.isOrganic) { <span>Bio</span> }
                        @if (store.isLocal) { <span>Local</span> }
                      </div>
                    </div>
                    <div class="store-row-actions">
                      <button class="stores-native-button" type="button" [disabled]="busy()" (click)="editStore(store)">Modifier</button>
                      @if (storeDeleteConfirmationId() === store.id) {
                        <button class="stores-native-button is-danger" type="button" [disabled]="busy()" (click)="deleteStore(store)">Confirmer la suppression</button>
                        <button class="stores-native-button" type="button" (click)="storeDeleteConfirmationId.set('')">Conserver</button>
                      } @else {
                        <button class="stores-native-button is-danger" type="button" [disabled]="busy()"
                          (click)="storeDeleteConfirmationId.set(store.id)">Supprimer</button>
                      }
                    </div>
                  </article>
                }
              </div>
            }
          </section>
        </div>
      </section>

      <section id="articles-section" class="stores-area articles-area" aria-labelledby="articles-heading">
        <div class="stores-area-heading">
          <div>
            <h2 id="articles-heading">Articles d’épicerie</h2>
            <p>Enregistrez les produits que vous achetez et comparez leurs prix au fil du temps.</p>
          </div>
          <span class="stores-count">{{ filteredArticles().length }} affiché{{ filteredArticles().length > 1 ? 's' : '' }}</span>
        </div>
        <div class="stores-workspace">
          <form class="stores-form" (ngSubmit)="saveArticle()">
            <div>
              <h2>{{ editingArticleId() ? 'Modifier un article' : 'Ajouter un article' }}</h2>
              <p>{{ editingArticleId() ? 'Modifiez les informations de cet article.' : 'Créez un article avant d’y associer ses prix en magasin.' }}</p>
            </div>
            <label class="stores-field">
              <span>Nom de l’article</span>
              <input class="stores-native-input" name="articleName" type="text" required maxlength="180"
                [(ngModel)]="articleEditor.name" />
            </label>
            <label class="stores-field">
              <span>Description (facultatif)</span>
              <textarea class="stores-native-input" name="articleDescription" rows="3" maxlength="500"
                [(ngModel)]="articleEditor.description"></textarea>
            </label>
            <label class="stores-field">
              <span>Unité de prix</span>
              <select class="stores-native-input" name="articleUnit" [(ngModel)]="articleEditor.unit">
                <option value="unit">À la pièce</option>
                <option value="kilogram">Au kilogramme</option>
                <option value="liter">Au litre</option>
              </select>
            </label>
            <div class="stores-form-actions">
              <button class="stores-native-button is-primary" type="submit" [disabled]="busy()">
                {{ busy() ? 'Enregistrement…' : editingArticleId() ? 'Enregistrer l’article' : 'Ajouter l’article' }}
              </button>
              @if (editingArticleId()) {
                <button class="stores-native-button" type="button" [disabled]="busy()" (click)="resetArticleEditor()">Annuler</button>
              }
            </div>
          </form>

          <section class="stores-directory" aria-label="Liste des articles d’épicerie">
            <div class="stores-directory-heading">
              <div>
                <h2>Liste des articles</h2>
                <p>{{ articles().length }} {{ articles().length === 1 ? 'article enregistré' : 'articles enregistrés' }}</p>
              </div>
              <label class="stores-search">
                <span class="sr-only">Filtrer les articles</span>
                <input class="stores-native-input" type="search" name="articleSearch" placeholder="Filtrer les articles"
                  [(ngModel)]="articleSearch" />
              </label>
            </div>
            @if (loading() && articles().length === 0) {
              <p class="stores-empty-inline" role="status">Chargement des articles…</p>
            } @else if (filteredArticles().length === 0) {
              <div class="stores-empty">
                <div>
                  <h3>{{ articleSearch ? 'Aucun article correspondant' : 'Aucun article pour le moment' }}</h3>
                  <p>{{ articleSearch ? 'Essayez un autre nom ou effacez le filtre.' : 'Ajoutez un article pour regrouper ses prix.' }}</p>
                </div>
              </div>
            } @else {
              <div class="stores-list articles-list">
                @for (article of filteredArticles(); track article.id) {
                  <article class="store-row article-row">
                    <div class="article-row-main">
                      <span class="store-row-icon" aria-hidden="true">▤</span>
                      <div class="store-row-content">
                        <h3>{{ article.name }}</h3>
                        <p>{{ unitLabel(article.unit) }}@if (article.description) { · {{ article.description }} }</p>
                      </div>
                      <span class="article-price-count">{{ article.priceHistory.length }} prix</span>
                    </div>
                    <div class="store-row-actions">
                      <button class="stores-native-button" type="button" [disabled]="busy()" (click)="editArticle(article)">Modifier</button>
                      <button class="stores-native-button" type="button" [attr.aria-expanded]="expandedArticleId() === article.id"
                        (click)="togglePrices(article.id)">{{ expandedArticleId() === article.id ? 'Masquer les prix' : 'Voir les prix' }}</button>
                      @if (articleDeleteConfirmationId() === article.id) {
                        <button class="stores-native-button is-danger" type="button" [disabled]="busy()" (click)="deleteArticle(article)">Confirmer la suppression</button>
                        <button class="stores-native-button" type="button" (click)="articleDeleteConfirmationId.set('')">Conserver</button>
                      } @else {
                        <button class="stores-native-button is-danger" type="button" [disabled]="busy()"
                          (click)="articleDeleteConfirmationId.set(article.id)">Supprimer</button>
                      }
                    </div>
                    @if (expandedArticleId() === article.id) {
                      <div class="article-price-panel">
                        <form class="article-price-form" (ngSubmit)="addPrice(article)">
                          <label class="stores-field">
                            <span>Magasin</span>
                            <select class="stores-native-input" name="priceStore-{{ article.id }}" required [(ngModel)]="priceStoreId">
                              <option value="" disabled>Choisir un magasin</option>
                              @for (store of stores(); track store.id) {
                                <option [value]="store.id">{{ store.name }}</option>
                              }
                            </select>
                          </label>
                          <label class="stores-field">
                            <span>Prix</span>
                            <input class="stores-native-input" name="priceAmount-{{ article.id }}" type="number" min="0" step="0.01"
                              inputmode="decimal" required [(ngModel)]="priceAmount" />
                          </label>
                          <label class="stores-field">
                            <span>Relevé le</span>
                            <input class="stores-native-input" name="priceDate-{{ article.id }}" type="date" required
                              [(ngModel)]="priceObservedAt" />
                          </label>
                          <button class="stores-native-button is-primary" type="submit" [disabled]="busy() || !stores().length">
                            {{ busy() ? 'Enregistrement…' : 'Ajouter le prix' }}
                          </button>
                        </form>
                        @if (stores().length === 0) {
                          <p class="stores-empty-inline">Ajoutez d’abord un magasin pour pouvoir relever un prix.</p>
                        }
                        @if (article.priceHistory.length === 0) {
                          <p class="stores-empty-inline">Aucun prix relevé pour le moment.</p>
                        } @else {
                          <ul class="article-price-list">
                            @for (entry of article.priceHistory; track entry.id) {
                              <li class="article-price-entry">
                                <div>
                                  <strong>{{ storeLabel(entry.storeId) }}</strong>
                                  <span>{{ formatDate(entry.observedAt) }} · {{ entry.price | number:'1.2-2':'fr-FR' }}</span>
                                </div>
                                <button class="stores-native-button is-danger" type="button" [disabled]="busy()"
                                  [attr.aria-label]="'Supprimer le prix de ' + storeLabel(entry.storeId) + ' relevé le ' + formatDate(entry.observedAt)"
                                  (click)="removePrice(article, entry)">Supprimer</button>
                              </li>
                            }
                          </ul>
                        }
                      </div>
                    }
                  </article>
                }
              </div>
            }
          </section>
        </div>
      </section>
    </section>
  `,
  styles: [`
    :host { display: block; }
    .stores-page-nav { display: flex; flex-wrap: wrap; gap: 8px; }
    .stores-page-nav a { padding: 9px 13px; border: 1px solid var(--lifeos-line); border-radius: 999px; background: var(--lifeos-surface); color: var(--lifeos-accent-strong); font-weight: 750; }
    .stores-page-nav a:hover, .stores-page-nav a:focus-visible { background: var(--lifeos-accent-soft); outline: 2px solid var(--lifeos-accent); outline-offset: 2px; }
    .stores-area { display: grid; gap: 16px; scroll-margin-top: 20px; }
    .stores-area-heading { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; }
    .stores-area-heading h2 { margin: 0; font-size: clamp(1.45rem, 2.4vw, 2rem); }
    .stores-area-heading p { margin: 7px 0 0; color: var(--lifeos-text-soft); line-height: 1.5; }
    .stores-area .stores-workspace { grid-template-columns: minmax(260px, .7fr) minmax(0, 1.6fr); }
    .stores-native-input { width: 100%; min-height: 42px; padding: 9px 11px; border: 1px solid var(--lifeos-line); border-radius: 10px; background: var(--lifeos-surface); color: var(--lifeos-text); font: inherit; }
    textarea.stores-native-input { resize: vertical; }
    .stores-native-input:focus-visible { border-color: var(--lifeos-accent); outline: 3px solid rgb(53 109 79 / 24%); }
    .stores-native-button { min-height: 38px; padding: 8px 11px; border: 1px solid var(--lifeos-line); border-radius: 9px; background: var(--lifeos-surface-soft); color: var(--lifeos-text); font: inherit; font-size: .86rem; font-weight: 750; cursor: pointer; }
    .stores-native-button:hover:not(:disabled) { background: var(--lifeos-accent-soft); }
    .stores-native-button:focus-visible { outline: 3px solid rgb(53 109 79 / 28%); outline-offset: 1px; }
    .stores-native-button:disabled { cursor: wait; opacity: .65; }
    .stores-native-button.is-primary { border-color: var(--lifeos-accent); background: var(--lifeos-accent); color: white; }
    .stores-native-button.is-primary:hover:not(:disabled) { background: var(--lifeos-accent-strong); }
    .stores-native-button.is-danger { color: #8b2c20; }
    .stores-form textarea { min-height: 86px; }
    .stores-form .stores-field > span { color: var(--lifeos-text-soft); font-size: .82rem; font-weight: 800; }
    .stores-options label { min-height: 28px; }
    .stores-search { min-width: 190px; }
    .stores-search .stores-native-input { min-height: 40px; }
    .stores-list { width: 100%; }
    .store-row-actions { justify-content: flex-end; }
    .article-row { min-width: 0; }
    .article-price-count { white-space: nowrap; color: var(--lifeos-text-soft); font-size: .8rem; font-weight: 800; }
    .article-price-entry span { color: var(--lifeos-text-soft); font-size: .87rem; }
    .stores-status { margin: 0; padding: 12px 14px; border-radius: 12px; font-weight: 750; line-height: 1.45; }
    .stores-status-error { background: #fff0ed; color: #8b2c20; }
    .stores-status-success { background: var(--lifeos-accent-soft); color: var(--lifeos-accent-strong); }
    @media (max-width: 960px) {
      .stores-area .stores-workspace { grid-template-columns: 1fr; }
    }
    @media (max-width: 620px) {
      .stores-area-heading { align-items: flex-start; }
      .stores-page-nav { width: 100%; }
      .stores-search { width: 100%; min-width: 0; }
      .article-row-main { grid-template-columns: auto minmax(0, 1fr); }
      .article-price-count { grid-column: 2; }
      .store-row-actions { justify-content: flex-start; }
    }
  `],
})
export class StoresArticlesPageComponent {
  private readonly api = inject(LifeosApiService)

  readonly stores = signal<StoreDto[]>([])
  readonly articles = signal<ArticleDto[]>([])
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly editingStoreId = signal('')
  readonly editingArticleId = signal('')
  readonly storeDeleteConfirmationId = signal('')
  readonly articleDeleteConfirmationId = signal('')
  readonly expandedArticleId = signal('')

  storeEditor = emptyStoreEditor()
  articleEditor = emptyArticleEditor()
  storeSearch = ''
  articleSearch = ''
  priceStoreId = ''
  priceAmount: number | null = null
  priceObservedAt = new Date().toISOString().slice(0, 10)

  constructor() {
    void this.loadData()
  }

  filteredStores(): StoreDto[] {
    const query = this.storeSearch.trim().toLocaleLowerCase()
    return query
      ? this.stores().filter((store) =>
        `${store.name} ${store.address ?? ''}`.toLocaleLowerCase().includes(query),
      )
      : this.stores()
  }

  filteredArticles(): ArticleDto[] {
    const query = this.articleSearch.trim().toLocaleLowerCase()
    return query
      ? this.articles().filter((article) =>
        `${article.name} ${article.description}`.toLocaleLowerCase().includes(query),
      )
      : this.articles()
  }

  async loadData(): Promise<void> {
    this.loading.set(true)
    try {
      const [stores, articles] = await Promise.all([
        firstValueFrom(this.api.get<StoreDto[]>('/stores')),
        firstValueFrom(this.api.get<ArticleDto[]>('/articles')),
      ])
      this.stores.set(stores)
      this.articles.set(articles)
      const firstStore = stores.at(0)
      if (!this.priceStoreId && firstStore) this.priceStoreId = firstStore.id
      this.error.set('')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  editStore(store: StoreDto): void {
    this.editingStoreId.set(store.id)
    this.storeEditor = {
      name: store.name,
      address: store.address ?? '',
      isOrganic: store.isOrganic,
      isLocal: store.isLocal,
    }
    this.notice.set('')
  }

  async saveStore(): Promise<void> {
    const request: StoreRequest = {
      name: this.storeEditor.name.trim(),
      address: this.storeEditor.address.trim() || null,
      isOrganic: this.storeEditor.isOrganic,
      isLocal: this.storeEditor.isLocal,
    }
    if (!request.name) return
    this.beginRequest()
    try {
      if (this.editingStoreId()) {
        await firstValueFrom(this.api.put<StoreDto>(`/stores/${this.editingStoreId()}`, request))
      } else {
        await firstValueFrom(this.api.post<StoreDto>('/stores', request))
      }
      this.resetStoreEditor()
      await this.reloadAfterMutation('Magasin enregistré.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async deleteStore(store: StoreDto): Promise<void> {
    this.beginRequest()
    try {
      await firstValueFrom(this.api.delete<void>(`/stores/${store.id}`))
      this.storeDeleteConfirmationId.set('')
      if (this.editingStoreId() === store.id) this.resetStoreEditor()
      await this.reloadAfterMutation(`Le magasin « ${store.name} » a été supprimé.`)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  resetStoreEditor(): void {
    this.storeEditor = emptyStoreEditor()
    this.editingStoreId.set('')
  }

  editArticle(article: ArticleDto): void {
    this.editingArticleId.set(article.id)
    this.articleEditor = {
      name: article.name,
      description: article.description,
      unit: article.unit,
    }
    this.notice.set('')
  }

  async saveArticle(): Promise<void> {
    const request: ArticleRequest = {
      name: this.articleEditor.name.trim(),
      description: this.articleEditor.description.trim(),
      unit: this.articleEditor.unit,
    }
    if (!request.name) return
    this.beginRequest()
    try {
      if (this.editingArticleId()) {
        await firstValueFrom(this.api.put<ArticleDto>(
          `/articles/${this.editingArticleId()}`,
          request,
        ))
      } else {
        await firstValueFrom(this.api.post<ArticleDto>('/articles', request))
      }
      this.resetArticleEditor()
      await this.reloadAfterMutation('Article enregistré.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async deleteArticle(article: ArticleDto): Promise<void> {
    this.beginRequest()
    try {
      await firstValueFrom(this.api.delete<void>(`/articles/${article.id}`))
      this.articleDeleteConfirmationId.set('')
      if (this.editingArticleId() === article.id) this.resetArticleEditor()
      if (this.expandedArticleId() === article.id) this.expandedArticleId.set('')
      await this.reloadAfterMutation(`L’article « ${article.name} » a été supprimé.`)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  resetArticleEditor(): void {
    this.articleEditor = emptyArticleEditor()
    this.editingArticleId.set('')
  }

  togglePrices(articleId: string): void {
    this.expandedArticleId.update((current) => current === articleId ? '' : articleId)
    this.error.set('')
    this.notice.set('')
  }

  async addPrice(article: ArticleDto): Promise<void> {
    if (!this.priceStoreId || this.priceAmount === null || this.priceAmount < 0) return
    const request: AddPriceEntryRequest = {
      storeId: this.priceStoreId,
      price: this.priceAmount,
      observedAt: new Date(`${this.priceObservedAt}T12:00:00`).toISOString(),
    }
    this.beginRequest()
    try {
      await firstValueFrom(this.api.post<ArticlePriceEntryDto>(
        `/articles/${article.id}/price-entries`,
        request,
      ))
      this.priceAmount = null
      await this.reloadAfterMutation('Prix ajouté à l’historique.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async removePrice(article: ArticleDto, entry: ArticlePriceEntryDto): Promise<void> {
    this.beginRequest()
    try {
      await firstValueFrom(this.api.delete<void>(
        `/articles/${article.id}/price-entries/${entry.id}`,
      ))
      await this.reloadAfterMutation('Prix supprimé de l’historique.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  storeLabel(storeId: string): string {
    return this.stores().find((store) => store.id === storeId)?.name
      ?? `Magasin inconnu (${storeId})`
  }

  unitLabel(unit: GroceryItemUnit): string {
    if (unit === 'kilogram') return 'Prix au kilogramme'
    if (unit === 'liter') return 'Prix au litre'
    return 'Prix à la pièce'
  }

  formatDate(value: string): string {
    const date = new Date(value)
    return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('fr-FR')
  }

  private beginRequest(): void {
    this.busy.set(true)
    this.error.set('')
    this.notice.set('')
  }

  private async reloadAfterMutation(message: string): Promise<void> {
    const [stores, articles] = await Promise.all([
      firstValueFrom(this.api.get<StoreDto[]>('/stores')),
      firstValueFrom(this.api.get<ArticleDto[]>('/articles')),
    ])
    this.stores.set(stores)
    this.articles.set(articles)
    this.error.set('')
    this.notice.set(message)
  }
}
