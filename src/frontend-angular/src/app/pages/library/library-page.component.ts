import { DecimalPipe } from '@angular/common'
import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import type {
  ActivityDto,
  LibraryCompositeDishDto,
  LibraryMealComponentDto,
} from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'

type LibraryCategory = 'all' | 'components' | 'dishes' | 'activities'

@Component({
  selector: 'app-library-page',
  standalone: true,
  imports: [DecimalPipe, FormsModule],
  template: `
    <div class="page-stack library-page">
      <header class="page-hero library-hero">
        <div>
          <h1>Bibliothèque partagée</h1>
          <p>
            Retrouvez les composants de repas, les plats complets et les activités. Ce catalogue
            partagé est en lecture seule.
          </p>
        </div>
      </header>

      <div class="library-toolbar">
        <label class="library-search">
          <span class="sr-only">Rechercher dans la bibliothèque</span>
          <input
            class="library-native-input"
            type="search"
            name="librarySearch"
            placeholder="Rechercher dans la bibliothèque"
            [(ngModel)]="search"
          />
        </label>
        <div class="library-tabs" role="group" aria-label="Catégories de la bibliothèque">
          <button
            class="library-tab"
            type="button"
            [attr.aria-pressed]="category() === 'all'"
            (click)="category.set('all')"
          >
            Tout
          </button>
          <button
            class="library-tab"
            type="button"
            [attr.aria-pressed]="category() === 'components'"
            (click)="category.set('components')"
          >
            Composants
          </button>
          <button
            class="library-tab"
            type="button"
            [attr.aria-pressed]="category() === 'dishes'"
            (click)="category.set('dishes')"
          >
            Plats
          </button>
          <button
            class="library-tab"
            type="button"
            [attr.aria-pressed]="category() === 'activities'"
            (click)="category.set('activities')"
          >
            Activités
          </button>
        </div>
      </div>

      <div class="library-content">
        @if (category() === 'all' || category() === 'components') {
          <section class="library-group" aria-labelledby="components-heading">
            <div class="library-group-heading">
              <h2 id="components-heading">Composants de repas</h2>
              @if (!componentsLoading() && !componentsError()) {
                <span class="library-count">{{ filteredComponents().length }} affichés</span>
              }
            </div>
            @if (componentsError()) {
              <p class="library-feedback" role="alert">
                Impossible de charger les composants de repas : {{ componentsError() }}
              </p>
              <button class="library-retry" type="button" (click)="loadComponents()">
                Réessayer de charger les composants
              </button>
            } @else if (componentsLoading()) {
              <p class="library-empty" role="status">Chargement des composants de repas…</p>
            } @else if (!filteredComponents().length) {
              <p class="library-empty">
                {{
                  search
                    ? 'Aucun composant ne correspond à votre recherche.'
                    : 'Aucun composant de repas disponible pour le moment.'
                }}
              </p>
            } @else {
              <div class="library-card-grid">
                @for (item of filteredComponents(); track item.id) {
                  <article class="library-card">
                    <div class="library-card-title">
                      <span class="library-item-icon" aria-hidden="true">{{ item.icon }}</span>
                      <div>
                        <h3>{{ item.name }}</h3>
                        <p>{{ componentTypeLabel(item.componentType) }}</p>
                      </div>
                    </div>
                    <div class="library-metadata">
                      <span
                        >{{ item.defaultPortionQuantity | number: '1.0-2' }}
                        {{ unitLabel(item.unit) }} par portion</span
                      >
                      <span>{{ item.estimatedCalories | number: '1.0-0' }} kcal</span>
                      <span>{{ item.estimatedProteinGrams | number: '1.0-1' }} g de protéines</span>
                      <span
                        >{{ item.estimatedCarbohydrateGrams | number: '1.0-1' }} g de glucides</span
                      >
                      <span>{{ item.estimatedFatGrams | number: '1.0-1' }} g de lipides</span>
                      @if (!item.active) {
                        <span>Inactif</span>
                      }
                    </div>
                  </article>
                }
              </div>
            }
          </section>
        }

        @if (category() === 'all' || category() === 'dishes') {
          <section class="library-group" aria-labelledby="dishes-heading">
            <div class="library-group-heading">
              <h2 id="dishes-heading">Plats composés</h2>
              @if (!dishesLoading() && !dishesError()) {
                <span class="library-count">{{ filteredDishes().length }} affichés</span>
              }
            </div>
            @if (dishesError()) {
              <p class="library-feedback" role="alert">
                Impossible de charger les plats composés : {{ dishesError() }}
              </p>
              <button class="library-retry" type="button" (click)="loadDishes()">
                Réessayer de charger les plats
              </button>
            } @else if (dishesLoading()) {
              <p class="library-empty" role="status">Chargement des plats composés…</p>
            } @else if (!filteredDishes().length) {
              <p class="library-empty">
                {{
                  search
                    ? 'Aucun plat ne correspond à votre recherche.'
                    : 'Aucun plat composé disponible pour le moment.'
                }}
              </p>
            } @else {
              <div class="library-card-grid library-card-grid-wide">
                @for (dish of filteredDishes(); track dish.id) {
                  <article class="library-card">
                    <div class="library-card-title">
                      <span class="library-item-icon" aria-hidden="true">{{ dish.icon }}</span>
                      <div>
                        <h3>{{ dish.name }}</h3>
                        <p>Plat complet</p>
                      </div>
                    </div>
                    <div class="library-metadata">
                      <span>{{ dish.estimatedCalories | number: '1.0-0' }} kcal</span>
                      <span>{{ dish.estimatedProteinGrams | number: '1.0-1' }} g de protéines</span>
                      <span>{{ dish.preparationTimeMinutes }} min de préparation</span>
                    </div>
                    <div class="library-tags" aria-label="Repas adaptés">
                      @if (dish.suitableForBreakfast) {
                        <span>Petit-déjeuner</span>
                      }
                      @if (dish.suitableForLunch) {
                        <span>Déjeuner</span>
                      }
                      @if (dish.suitableForDinner) {
                        <span>Dîner</span>
                      }
                      @if (!dish.active) {
                        <span>Inactif</span>
                      }
                    </div>
                  </article>
                }
              </div>
            }
          </section>
        }

        @if (category() === 'all' || category() === 'activities') {
          <section class="library-group" aria-labelledby="activities-heading">
            <div class="library-group-heading">
              <h2 id="activities-heading">Activités</h2>
              @if (!activitiesLoading() && !activitiesError()) {
                <span class="library-count">{{ filteredActivities().length }} affichées</span>
              }
            </div>
            @if (activitiesError()) {
              <p class="library-feedback" role="alert">
                Impossible de charger les activités : {{ activitiesError() }}
              </p>
              <button class="library-retry" type="button" (click)="loadActivities()">
                Réessayer de charger les activités
              </button>
            } @else if (activitiesLoading()) {
              <p class="library-empty" role="status">Chargement des activités…</p>
            } @else if (!filteredActivities().length) {
              <p class="library-empty">
                {{
                  search
                    ? 'Aucune activité ne correspond à votre recherche.'
                    : 'Aucune activité disponible pour le moment.'
                }}
              </p>
            } @else {
              <div class="library-card-grid">
                @for (activity of filteredActivities(); track activity.id) {
                  <article class="library-card">
                    <div class="library-card-title">
                      <span class="library-item-icon" aria-hidden="true">{{ activity.icon }}</span>
                      <div>
                        <h3>{{ activity.name }}</h3>
                        @if (typeof activity.defaultDurationMinutes === 'number') {
                          <p>Durée habituelle : {{ activity.defaultDurationMinutes }} min</p>
                        }
                      </div>
                    </div>
                  </article>
                }
              </div>
            }
          </section>
        }
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .library-native-input {
        width: 100%;
        min-height: 44px;
        border: 0;
        background: transparent;
        color: var(--lifeos-text);
        font: inherit;
      }
      .library-native-input:focus-visible,
      .library-tab:focus-visible,
      .library-retry:focus-visible {
        outline: 3px solid var(--lifeos-accent);
        outline-offset: 2px;
      }
      .library-tab,
      .library-retry {
        min-height: 42px;
        padding: 8px 13px;
        border: 1px solid var(--lifeos-line);
        border-radius: 12px;
        background: var(--lifeos-surface);
        color: var(--lifeos-text);
        font: inherit;
        font-weight: 750;
        cursor: pointer;
      }
      .library-tab:hover,
      .library-retry:hover {
        background: var(--lifeos-accent-soft);
      }
      .library-tab[aria-pressed='true'] {
        background: var(--lifeos-accent);
        border-color: var(--lifeos-accent);
        color: white;
      }
      .library-retry {
        width: fit-content;
      }
      .library-count {
        color: var(--lifeos-text-soft);
        font-size: 0.85rem;
        font-weight: 750;
      }
      .library-feedback {
        margin: 0;
        padding: 12px 14px;
        border-radius: 12px;
        background: #fff0ed;
        color: #8b2c20;
        overflow-wrap: anywhere;
      }
      .library-tags span {
        padding: 6px 9px;
        border-radius: 999px;
        background: var(--lifeos-accent-soft);
        color: var(--lifeos-accent-strong);
        font-size: 0.8rem;
        font-weight: 750;
      }
      @media (max-width: 620px) {
        .library-search {
          width: 100%;
          flex-basis: auto;
        }
        .library-tab {
          flex: 1 1 auto;
        }
      }
    `,
  ],
})
export class LibraryPageComponent {
  private readonly api = inject(LifeosApiService)

  readonly components = signal<LibraryMealComponentDto[]>([])
  readonly dishes = signal<LibraryCompositeDishDto[]>([])
  readonly activities = signal<ActivityDto[]>([])
  readonly category = signal<LibraryCategory>('all')
  readonly componentsLoading = signal(true)
  readonly dishesLoading = signal(true)
  readonly activitiesLoading = signal(true)
  readonly componentsError = signal('')
  readonly dishesError = signal('')
  readonly activitiesError = signal('')
  search = ''

  componentTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      protein: 'Protéines',
      starch: 'Féculents',
      vegetable: 'Légumes',
      optional: 'Complément',
    }
    return labels[type.toLowerCase()] ?? type
  }

  unitLabel(unit: string): string {
    const labels: Record<string, string> = { kilogram: 'kg', liter: 'L', unit: 'unité' }
    return labels[unit.toLowerCase()] ?? unit
  }

  constructor() {
    void Promise.all([this.loadComponents(), this.loadDishes(), this.loadActivities()])
  }

  filteredComponents(): LibraryMealComponentDto[] {
    const query = this.search.trim().toLocaleLowerCase()
    return query
      ? this.components().filter((item) => item.name.toLocaleLowerCase().includes(query))
      : this.components()
  }

  filteredDishes(): LibraryCompositeDishDto[] {
    const query = this.search.trim().toLocaleLowerCase()
    return query
      ? this.dishes().filter((dish) => dish.name.toLocaleLowerCase().includes(query))
      : this.dishes()
  }

  filteredActivities(): ActivityDto[] {
    const query = this.search.trim().toLocaleLowerCase()
    return query
      ? this.activities().filter((activity) => activity.name.toLocaleLowerCase().includes(query))
      : this.activities()
  }

  async loadComponents(): Promise<void> {
    this.componentsLoading.set(true)
    this.componentsError.set('')
    try {
      this.components.set(
        await firstValueFrom(this.api.get<LibraryMealComponentDto[]>('/meal-components')),
      )
    } catch (error) {
      this.componentsError.set(apiErrorMessage(error))
    } finally {
      this.componentsLoading.set(false)
    }
  }

  async loadDishes(): Promise<void> {
    this.dishesLoading.set(true)
    this.dishesError.set('')
    try {
      this.dishes.set(
        await firstValueFrom(this.api.get<LibraryCompositeDishDto[]>('/composite-dishes')),
      )
    } catch (error) {
      this.dishesError.set(apiErrorMessage(error))
    } finally {
      this.dishesLoading.set(false)
    }
  }

  async loadActivities(): Promise<void> {
    this.activitiesLoading.set(true)
    this.activitiesError.set('')
    try {
      this.activities.set(await firstValueFrom(this.api.get<ActivityDto[]>('/activities')))
    } catch (error) {
      this.activitiesError.set(apiErrorMessage(error))
    } finally {
      this.activitiesLoading.set(false)
    }
  }
}
