import { DecimalPipe } from '@angular/common'
import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import type {
  ArticleDto,
  ShoppingListItemDto,
  StockItemDto,
  StockItemRequest,
  WeekDto,
} from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'

@Component({
  selector: 'app-stock-shopping-page',
  standalone: true,
  imports: [DecimalPipe, FormsModule],
  template: `
    <div class="page-stack stores-page stock-page">
      <header class="page-hero">
        <div>
          <h1>Stock et liste de courses</h1>
          <p>
            Suivez vos provisions, puis générez une liste de courses à cocher à partir d’une semaine
            planifiée.
          </p>
        </div>
      </header>

      @if (notice()) {
        <p class="stock-feedback is-success" role="status">{{ notice() }}</p>
      }

      <section class="stock-section" aria-labelledby="stock-heading">
        <div class="stock-section-heading">
          <div>
            <h2 id="stock-heading">En stock</h2>
            <p>Indiquez les quantités disponibles avant de générer la liste de courses.</p>
          </div>
          @if (!loadingStock() && !stockError()) {
            <span class="stores-count"
              >{{ stock().length }} {{ stock().length === 1 ? 'article' : 'articles' }}</span
            >
          }
        </div>
        <div class="stores-workspace">
          <form class="stores-form stock-form" (ngSubmit)="saveStock()">
            <div>
              <h2>{{ editingId() ? 'Modifier le stock' : 'Ajouter au stock' }}</h2>
              <p>
                {{
                  editingId()
                    ? 'Modifiez la quantité disponible.'
                    : 'Choisissez un article et indiquez la quantité disponible.'
                }}
              </p>
            </div>
            @if (articlesError()) {
              <p class="stock-feedback is-error" role="alert">
                Impossible de charger les articles : {{ articlesError() }}
              </p>
              <button
                class="stock-button"
                type="button"
                (click)="loadArticles()"
                [disabled]="loadingArticles()"
              >
                Réessayer de charger les articles
              </button>
            }
            <label class="stores-field">
              <span>Article</span>
              <select
                class="stock-input"
                name="groceryItemId"
                required
                [(ngModel)]="editor.groceryItemId"
                (ngModelChange)="articleChanged()"
                [disabled]="busy() !== '' || loadingArticles() || !!editingId()"
              >
                <option value="" disabled>Choisir un article</option>
                @if (editingId() && !hasArticle(editor.groceryItemId)) {
                  <option [value]="editor.groceryItemId">{{ editor.groceryItemId }}</option>
                }
                @for (article of articles(); track article.id) {
                  <option [value]="article.id">{{ article.name }}</option>
                }
              </select>
            </label>
            @if (!loadingArticles() && !articlesError() && articles().length === 0) {
              <p class="stock-hint">
                Aucun article disponible. Ajoutez-en un dans la rubrique Magasins et articles.
              </p>
            }
            <label class="stores-field">
              <span>Quantité</span>
              <input
                class="stock-input"
                type="number"
                name="quantity"
                min="0.001"
                step="any"
                inputmode="decimal"
                required
                [(ngModel)]="editor.quantity"
                [disabled]="busy() !== ''"
              />
            </label>
            <label class="stores-field">
              <span>Unité</span>
              <input
                class="stock-input"
                type="text"
                name="unit"
                required
                maxlength="40"
                [(ngModel)]="editor.unit"
                [disabled]="busy() !== ''"
              />
            </label>
            <div class="stores-form-actions">
              <button
                class="stock-button is-primary"
                type="submit"
                [disabled]="
                  busy() !== '' ||
                  loadingStock() ||
                  !!stockError() ||
                  !editor.groceryItemId ||
                  (!editingId() && (loadingArticles() || !!articlesError() || !articles().length))
                "
              >
                {{
                  busy() === 'stock'
                    ? 'Enregistrement…'
                    : editingId()
                      ? 'Enregistrer la quantité'
                      : 'Ajouter au stock'
                }}
              </button>
              @if (editingId()) {
                <button
                  class="stock-button"
                  type="button"
                  (click)="resetEditor()"
                  [disabled]="busy() !== ''"
                >
                  Annuler
                </button>
              }
            </div>
            @if (stockActionError()) {
              <p class="stock-feedback is-error" role="alert">
                Impossible d’enregistrer le stock : {{ stockActionError() }}
              </p>
            }
          </form>

          <section class="stores-directory" aria-label="Stock actuel">
            <div class="stores-directory-heading">
              <div>
                <h2>Stock actuel</h2>
                <p>Modifier le stock ne coche pas les articles de la liste de courses.</p>
              </div>
            </div>
            @if (stockError()) {
              <p class="stock-feedback is-error" role="alert">
                Impossible de charger le stock : {{ stockError() }}
              </p>
              <button
                class="stock-button"
                type="button"
                (click)="loadStock()"
                [disabled]="loadingStock()"
              >
                Réessayer de charger le stock
              </button>
            } @else if (loadingStock()) {
              <p class="stock-hint" role="status">Chargement du stock…</p>
            } @else if (!stock().length) {
              <p class="stock-hint">Aucun article en stock. Ajoutez-en un pour commencer.</p>
            } @else {
              <div class="stores-list">
                @for (item of stock(); track item.id) {
                  <article class="store-row">
                    <span class="store-row-icon" aria-hidden="true">▤</span>
                    <div class="store-row-content">
                      <h3>{{ articleName(item.groceryItemId) }}</h3>
                      <p>{{ item.quantity | number: '1.0-2' }} {{ unitLabel(item.unit) }}</p>
                    </div>
                    <div class="store-row-actions">
                      <button
                        class="stock-button"
                        type="button"
                        (click)="editStock(item)"
                        [disabled]="busy() !== ''"
                      >
                        Modifier
                      </button>
                      @if (deleteConfirmationId() === item.id) {
                        <button
                          class="stock-button is-danger"
                          type="button"
                          (click)="removeStock(item)"
                          [disabled]="busy() !== ''"
                        >
                          Supprimer cet article
                        </button>
                        <button
                          class="stock-button"
                          type="button"
                          (click)="deleteConfirmationId.set('')"
                          [disabled]="busy() !== ''"
                        >
                          Conserver
                        </button>
                      } @else {
                        <button
                          class="stock-button is-danger"
                          type="button"
                          (click)="deleteConfirmationId.set(item.id)"
                          [disabled]="busy() !== ''"
                        >
                          Supprimer
                        </button>
                      }
                    </div>
                  </article>
                }
              </div>
            }
            @if (stockDeleteError()) {
              <p class="stock-feedback is-error" role="alert">
                Impossible de supprimer cet article du stock : {{ stockDeleteError() }}
              </p>
            }
          </section>
        </div>
      </section>

      <section class="stock-section" aria-labelledby="shopping-heading">
        <div class="stock-section-heading">
          <div>
            <h2 id="shopping-heading">Liste de courses</h2>
            <p>
              Choisissez une semaine planifiée, générez sa liste, puis cochez les articles pendant
              vos courses.
            </p>
          </div>
        </div>
        <div class="stores-workspace">
          <div class="stores-form stock-form">
            <div>
              <h2>Préparer les courses</h2>
              <p>
                La génération recalcule la liste de la semaine choisie à partir des repas prévus et
                du stock actuel.
              </p>
            </div>
            @if (weeksError()) {
              <p class="stock-feedback is-error" role="alert">
                Impossible de charger les semaines : {{ weeksError() }}
              </p>
              <button
                class="stock-button"
                type="button"
                (click)="loadWeeks()"
                [disabled]="loadingWeeks()"
              >
                Réessayer de charger les semaines
              </button>
            }
            <label class="stores-field">
              <span>Semaine planifiée</span>
              <select
                class="stock-input"
                name="selectedWeek"
                [ngModel]="selectedWeekId()"
                (ngModelChange)="selectWeek($event)"
                [disabled]="loadingWeeks() || busy() !== '' || !weeks().length"
              >
                @if (!weeks().length) {
                  <option value="">Aucune semaine disponible</option>
                }
                @for (week of weeks(); track week.id) {
                  <option [value]="week.id">Semaine du {{ week.startsOn }}</option>
                }
              </select>
            </label>
            @if (loadingWeeks()) {
              <p class="stock-hint" role="status">Chargement des semaines…</p>
            } @else if (!weeksError() && !weeks().length) {
              <p class="stock-hint">
                Planifiez d’abord une semaine pour générer une liste de courses.
              </p>
            }
            @if (isManualWeek()) {
              <p class="stock-hint">Les courses automatiques sont hors MVP manuel. Les listes historiques restent consultables.</p>
            }
            <button
              class="stock-button is-primary"
              type="button"
              (click)="generateList()"
              [disabled]="isManualWeek() || !selectedWeekId() || loadingList() || busy() !== ''"
            >
              {{ busy() === 'generate' ? 'Génération…' : 'Générer la liste de courses' }}
            </button>
            @if (shoppingActionError()) {
              <p class="stock-feedback is-error" role="alert">{{ shoppingActionError() }}</p>
            }
          </div>

          <section class="stores-directory" aria-label="Liste de courses de la semaine choisie">
            <div class="stores-directory-heading">
              <div>
                <h2>Pour cette semaine</h2>
                <p>Cocher un article ne modifie pas le stock.</p>
              </div>
              @if (!loadingList() && !shoppingError() && selectedWeekId()) {
                <span class="stores-count"
                  >{{ checkedCount() }} / {{ shoppingList().length }} cochés</span
                >
              }
            </div>
            @if (shoppingError()) {
              <p class="stock-feedback is-error" role="alert">
                Impossible de charger la liste de courses : {{ shoppingError() }}
              </p>
              <button
                class="stock-button"
                type="button"
                (click)="loadShoppingList()"
                [disabled]="loadingList()"
              >
                Réessayer de charger la liste
              </button>
            } @else if (loadingList()) {
              <p class="stock-hint" role="status">Chargement de la liste de courses…</p>
            } @else if (!selectedWeekId()) {
              <p class="stock-hint">
                Choisissez une semaine planifiée pour consulter sa liste de courses.
              </p>
            } @else if (!shoppingList().length) {
              <p class="stock-hint">
                Aucun article pour cette semaine. Planifiez vos repas, puis générez la liste.
              </p>
            } @else {
              <ul class="stock-shopping-list">
                @for (item of shoppingList(); track item.id) {
                  <li class="stock-shopping-row" [class.is-checked]="item.checked">
                    <label class="stock-check">
                      <input
                        type="checkbox"
                        [checked]="item.checked"
                        (change)="setChecked(item, $event)"
                        [disabled]="busy() !== ''"
                      />
                      <span class="stock-check-content">
                        <strong>{{ articleName(item.groceryItemId) }}</strong>
                        <span
                          >{{ item.quantityNeeded - item.quantityFromStock | number: '1.0-2' }} à
                          acheter · {{ item.quantityFromStock | number: '1.0-2' }} en stock ·
                          {{ item.quantityNeeded | number: '1.0-2' }} nécessaires au total</span
                        >
                      </span>
                    </label>
                  </li>
                }
              </ul>
            }
          </section>
        </div>
      </section>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .stock-page {
        max-width: 1180px;
      }
      .stock-section {
        display: grid;
        gap: 16px;
      }
      .stock-section-heading {
        display: flex;
        align-items: end;
        justify-content: space-between;
        gap: 16px;
      }
      .stock-section-heading h2 {
        margin: 0;
        font-size: clamp(1.45rem, 2.4vw, 2rem);
      }
      .stock-section-heading p {
        margin: 7px 0 0;
        color: var(--lifeos-text-soft);
        line-height: 1.5;
      }
      .stock-input {
        width: 100%;
        min-height: 44px;
        padding: 9px 11px;
        border: 1px solid var(--lifeos-line);
        border-radius: 10px;
        background: var(--lifeos-surface);
        color: var(--lifeos-text);
        font: inherit;
      }
      .stock-input:focus-visible,
      .stock-button:focus-visible,
      .stock-check input:focus-visible {
        outline: 3px solid var(--lifeos-accent);
        outline-offset: 2px;
      }
      .stock-button {
        min-height: 40px;
        padding: 8px 12px;
        border: 1px solid var(--lifeos-line);
        border-radius: 10px;
        background: var(--lifeos-surface-soft);
        color: var(--lifeos-text);
        font: inherit;
        font-weight: 750;
        cursor: pointer;
      }
      .stock-button:hover:not(:disabled) {
        background: var(--lifeos-accent-soft);
      }
      .stock-button:disabled {
        cursor: not-allowed;
        opacity: 0.6;
      }
      .stock-button.is-primary {
        background: var(--lifeos-accent);
        border-color: var(--lifeos-accent);
        color: white;
      }
      .stock-button.is-primary:hover:not(:disabled) {
        background: var(--lifeos-accent-strong);
      }
      .stock-button.is-danger {
        color: #8b2c20;
      }
      .stock-feedback {
        margin: 0;
        padding: 12px 14px;
        border-radius: 12px;
        line-height: 1.45;
        overflow-wrap: anywhere;
      }
      .stock-feedback.is-error {
        background: #fff0ed;
        color: #8b2c20;
      }
      .stock-feedback.is-success {
        background: var(--lifeos-accent-soft);
        color: var(--lifeos-accent-strong);
      }
      .stock-hint {
        margin: 0;
        color: var(--lifeos-text-soft);
        line-height: 1.5;
      }
      .stock-shopping-list {
        margin: 0;
        padding: 0;
        list-style: none;
        border: 1px solid var(--lifeos-line);
        border-radius: 18px;
        overflow: hidden;
      }
      .stock-shopping-row + .stock-shopping-row {
        border-top: 1px solid var(--lifeos-line);
      }
      .stock-check {
        display: flex;
        align-items: center;
        gap: 14px;
        min-height: 68px;
        padding: 12px 16px;
        cursor: pointer;
      }
      .stock-check:hover {
        background: var(--lifeos-surface-soft);
      }
      .stock-check input {
        width: 22px;
        height: 22px;
        flex: 0 0 auto;
        accent-color: var(--lifeos-accent);
      }
      .stock-check-content {
        display: grid;
        gap: 3px;
        min-width: 0;
        overflow-wrap: anywhere;
      }
      .stock-check-content > span {
        color: var(--lifeos-text-soft);
        font-size: 0.88rem;
      }
      .stock-shopping-row.is-checked .stock-check-content > strong {
        color: var(--lifeos-text-soft);
        text-decoration: line-through;
      }
      @media (max-width: 800px) {
        .stock-page .stores-workspace {
          grid-template-columns: 1fr;
        }
        .stock-page .stores-form {
          position: static;
        }
      }
      @media (max-width: 620px) {
        .stock-section-heading {
          align-items: start;
          flex-direction: column;
        }
        .stock-page .store-row-actions {
          justify-content: flex-start;
        }
        .stock-check {
          min-height: 60px;
        }
      }
    `,
  ],
})
export class StockShoppingPageComponent {
  private readonly api = inject(LifeosApiService)

  readonly stock = signal<StockItemDto[]>([])
  readonly shoppingList = signal<ShoppingListItemDto[]>([])
  readonly articles = signal<ArticleDto[]>([])
  readonly weeks = signal<WeekDto[]>([])
  readonly selectedWeekId = signal('')
  readonly editingId = signal('')
  readonly deleteConfirmationId = signal('')
  readonly busy = signal('')
  readonly loadingStock = signal(true)
  readonly loadingArticles = signal(true)
  readonly loadingWeeks = signal(true)
  readonly loadingList = signal(false)
  readonly stockError = signal('')
  readonly articlesError = signal('')
  readonly weeksError = signal('')
  readonly shoppingError = signal('')
  readonly stockActionError = signal('')
  readonly stockDeleteError = signal('')
  readonly shoppingActionError = signal('')
  readonly notice = signal('')

  editor: { groceryItemId: string; quantity: number | null; unit: string } = {
    groceryItemId: '',
    quantity: null,
    unit: '',
  }

  constructor() {
    void Promise.all([this.loadArticles(), this.loadStock(), this.loadWeeks()])
  }

  articleName(id: string): string {
    return this.articles().find((article) => article.id === id)?.name ?? id
  }

  unitLabel(unit: string): string {
    const labels: Record<string, string> = { kilogram: 'kg', liter: 'L', unit: 'unité' }
    return labels[unit.toLowerCase()] ?? unit
  }

  hasArticle(id: string): boolean {
    return this.articles().some((article) => article.id === id)
  }

  checkedCount(): number {
    return this.shoppingList().filter((item) => item.checked).length
  }

  async loadArticles(): Promise<void> {
    this.loadingArticles.set(true)
    this.articlesError.set('')
    try {
      this.articles.set(await firstValueFrom(this.api.get<ArticleDto[]>('/articles')))
    } catch (error) {
      this.articlesError.set(apiErrorMessage(error))
    } finally {
      this.loadingArticles.set(false)
    }
  }

  async loadStock(): Promise<void> {
    this.loadingStock.set(true)
    this.stockError.set('')
    try {
      this.stock.set(await firstValueFrom(this.api.get<StockItemDto[]>('/stock-items')))
    } catch (error) {
      this.stockError.set(apiErrorMessage(error))
    } finally {
      this.loadingStock.set(false)
    }
  }

  async loadWeeks(): Promise<void> {
    this.loadingWeeks.set(true)
    this.weeksError.set('')
    try {
      const weeks = await firstValueFrom(this.api.get<WeekDto[]>('/weeks'))
      this.weeks.set([...weeks].sort((a, b) => b.startsOn.localeCompare(a.startsOn)))
      const selected = this.weeks().some((week) => week.id === this.selectedWeekId())
        ? this.selectedWeekId()
        : (this.weeks()[0]?.id ?? '')
      this.selectWeek(selected)
    } catch (error) {
      this.weeksError.set(apiErrorMessage(error))
    } finally {
      this.loadingWeeks.set(false)
    }
  }

  selectWeek(id: string): void {
    this.selectedWeekId.set(id)
    this.shoppingList.set([])
    this.shoppingError.set('')
    this.shoppingActionError.set('')
    if (id) void this.loadShoppingList()
  }

  async loadShoppingList(): Promise<void> {
    const weekId = this.selectedWeekId()
    if (!weekId) return
    this.loadingList.set(true)
    this.shoppingError.set('')
    try {
      const items = await firstValueFrom(
        this.api.get<ShoppingListItemDto[]>('/shopping-list/items', { weekId }),
      )
      if (this.selectedWeekId() === weekId) this.shoppingList.set(items)
    } catch (error) {
      if (this.selectedWeekId() === weekId) this.shoppingError.set(apiErrorMessage(error))
    } finally {
      if (this.selectedWeekId() === weekId) this.loadingList.set(false)
    }
  }

  articleChanged(): void {
    const article = this.articles().find((entry) => entry.id === this.editor.groceryItemId)
    this.editor.unit = article?.unit ?? ''
  }

  editStock(item: StockItemDto): void {
    this.editingId.set(item.id)
    this.editor = { groceryItemId: item.groceryItemId, quantity: item.quantity, unit: item.unit }
    this.stockActionError.set('')
    this.notice.set('')
    this.deleteConfirmationId.set('')
  }

  resetEditor(): void {
    this.editingId.set('')
    this.editor = { groceryItemId: '', quantity: null, unit: '' }
    this.stockActionError.set('')
  }

  async saveStock(): Promise<void> {
    if (this.busy()) return
    const quantity = this.editor.quantity
    const unit = this.editor.unit.trim()
    if (
      !this.editor.groceryItemId ||
      quantity === null ||
      !Number.isFinite(quantity) ||
      quantity <= 0 ||
      !unit
    ) {
      this.stockActionError.set(
        'Choisissez un article, puis indiquez une quantité positive et une unité.',
      )
      return
    }
    this.busy.set('stock')
    this.stockActionError.set('')
    this.notice.set('')
    try {
      const id = this.editingId()
      const item = id
        ? await firstValueFrom(this.api.put<StockItemDto>(`/stock-items/${id}`, { quantity, unit }))
        : await firstValueFrom(
            this.api.post<StockItemDto>('/stock-items', {
              groceryItemId: this.editor.groceryItemId,
              quantity,
              unit,
            } satisfies StockItemRequest),
          )
      this.stock.update((items) =>
        id ? items.map((entry) => (entry.id === id ? item : entry)) : [...items, item],
      )
      this.resetEditor()
      this.notice.set(id ? 'Stock modifié.' : 'Article ajouté au stock.')
    } catch (error) {
      this.stockActionError.set(apiErrorMessage(error))
    } finally {
      this.busy.set('')
    }
  }

  async removeStock(item: StockItemDto): Promise<void> {
    if (this.busy()) return
    this.busy.set('delete')
    this.stockDeleteError.set('')
    this.notice.set('')
    try {
      await firstValueFrom(this.api.delete(`/stock-items/${item.id}`))
      this.stock.update((items) => items.filter((entry) => entry.id !== item.id))
      if (this.editingId() === item.id) this.resetEditor()
      this.deleteConfirmationId.set('')
      this.notice.set('Article supprimé du stock.')
    } catch (error) {
      this.stockDeleteError.set(apiErrorMessage(error))
    } finally {
      this.busy.set('')
    }
  }

  async generateList(): Promise<void> {
    const weekId = this.selectedWeekId()
    if (!weekId || this.busy() || this.loadingList() || this.isManualWeek()) return
    this.busy.set('generate')
    this.shoppingActionError.set('')
    this.shoppingError.set('')
    this.notice.set('')
    try {
      const response = await firstValueFrom(
        this.api.post<{ shoppingList: ShoppingListItemDto[] }>(
          `/shopping-list/weeks/${weekId}/generate`,
        ),
      )
      this.shoppingList.set(response.shoppingList)
      this.notice.set('Liste de courses générée pour la semaine choisie.')
    } catch (error) {
      this.shoppingActionError.set(
        `Impossible de générer la liste de courses : ${apiErrorMessage(error)}`,
      )
    } finally {
      this.busy.set('')
    }

  }

  isManualWeek(): boolean {
    return this.weeks().find(w => w.id === this.selectedWeekId())?.isManual ?? false
  }

  async setChecked(item: ShoppingListItemDto, event: Event): Promise<void> {
    const checkbox = event.target
    if (!(checkbox instanceof HTMLInputElement)) return
    const checked = checkbox.checked
    if (this.busy()) {
      checkbox.checked = item.checked
      return
    }
    this.busy.set(`check-${item.id}`)
    this.shoppingActionError.set('')
    this.notice.set('')
    try {
      const updated = await firstValueFrom(
        this.api.patch<ShoppingListItemDto>(`/shopping-list/items/${item.id}`, { checked }),
      )
      this.shoppingList.update((items) =>
        items.map((entry) => (entry.id === item.id ? updated : entry)),
      )
    } catch (error) {
      checkbox.checked = item.checked
      this.shoppingActionError.set(
        `Impossible de modifier ${this.articleName(item.groceryItemId)} dans la liste : ${apiErrorMessage(error)}`,
      )
    } finally {
      this.busy.set('')
    }
  }
}
