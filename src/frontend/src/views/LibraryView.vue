<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Tag from 'primevue/tag'
import { activities, compositeDishes, mealComponents } from '@/data/localLibrary'
import { useGroceryStoresStore } from '@/stores/groceryStores'
import type { ComponentType, CompositeDish, GroceryStore } from '@/types'

type LibraryTab = 'components' | 'dishes' | 'activities' | 'stores'

const activeTab = ref<LibraryTab>('components')
const searchQuery = ref('')
const storeName = ref('')
const storeAddress = ref('')
const editingStoreId = ref<string | null>(null)
const storeFormError = ref('')

const groceryStoresStore = useGroceryStoresStore()
const { stores } = storeToRefs(groceryStoresStore)

const tabs = computed<{ id: LibraryTab; label: string; count: number }[]>(() => [
  { id: 'components', label: 'Composants', count: mealComponents.length },
  { id: 'dishes', label: 'Plats composés', count: compositeDishes.length },
  { id: 'activities', label: 'Activités', count: activities.length },
  { id: 'stores', label: 'Magasins', count: stores.value.length },
])

const componentTypeLabels: Record<ComponentType, string> = {
  protein: 'Protéines',
  starch: 'Féculents',
  vegetable: 'Légumes',
  optional: 'Optionnels',
}

const componentTypes: ComponentType[] = ['protein', 'starch', 'vegetable', 'optional']

const normalizedSearch = computed(() => searchQuery.value.trim().toLocaleLowerCase('fr-FR'))

const filteredComponents = computed(() =>
  mealComponents.filter((component) => matchesSearch(component.name)),
)

const filteredDishes = computed(() => compositeDishes.filter((dish) => matchesSearch(dish.name)))

const filteredActivities = computed(() =>
  activities.filter((activity) => matchesSearch(activity.name)),
)

const filteredStores = computed(() =>
  stores.value.filter(
    (store) => matchesSearch(store.name) || matchesSearch(store.address),
  ),
)

const groupedComponents = computed(() =>
  componentTypes.map((componentType) => ({
    componentType,
    label: componentTypeLabels[componentType],
    items: filteredComponents.value.filter((component) => component.componentType === componentType),
  })),
)

function matchesSearch(name: string): boolean {
  return !normalizedSearch.value || name.toLocaleLowerCase('fr-FR').includes(normalizedSearch.value)
}

function portionLabel(quantity: number, unit: string): string {
  return `${quantity} ${unit}`
}

function dishMealLabels(dish: CompositeDish): string[] {
  return [
    dish.suitableForBreakfast ? 'Petit-déjeuner' : null,
    dish.suitableForLunch ? 'Déjeuner' : null,
    dish.suitableForDinner ? 'Dîner' : null,
  ].filter((label): label is string => Boolean(label))
}

function resetStoreForm(): void {
  editingStoreId.value = null
  storeName.value = ''
  storeAddress.value = ''
  storeFormError.value = ''
}

function startStoreEdition(store: GroceryStore): void {
  editingStoreId.value = store.id
  storeName.value = store.name
  storeAddress.value = store.address
  storeFormError.value = ''
}

function submitStoreForm(): void {
  const payload = {
    name: storeName.value,
    address: storeAddress.value,
  }
  const success = editingStoreId.value
    ? groceryStoresStore.updateStore(editingStoreId.value, payload)
    : groceryStoresStore.createStore(payload)

  if (!success) {
    storeFormError.value = 'Le nom du magasin est requis.'
    return
  }

  resetStoreForm()
}

function removeStore(id: string): void {
  groceryStoresStore.deleteStore(id)

  if (editingStoreId.value === id) {
    resetStoreForm()
  }
}
</script>

<template>
  <section class="page-stack library-page">
    <header class="page-hero library-hero">
      <div>
        <p class="eyebrow">Bibliothèque</p>
        <h1>Éléments disponibles</h1>
        <p>La source locale utilisée par Today, le planning et l'éditeur de repas.</p>
      </div>
    </header>

    <section class="library-toolbar" aria-label="Recherche et catégories">
      <span class="library-search">
        <i class="pi pi-search" aria-hidden="true"></i>
        <InputText v-model="searchQuery" placeholder="Rechercher par nom" aria-label="Rechercher dans la bibliothèque" />
      </span>

      <div class="library-tabs" role="tablist" aria-label="Catégories de bibliothèque">
        <Button
          v-for="tab in tabs"
          :key="tab.id"
          :label="`${tab.label} (${tab.count})`"
          :severity="activeTab === tab.id ? 'primary' : 'secondary'"
          :outlined="activeTab !== tab.id"
          size="small"
          role="tab"
          :aria-selected="activeTab === tab.id"
          @click="activeTab = tab.id"
        />
      </div>
    </section>

    <section v-if="activeTab === 'components'" class="library-content" aria-label="Composants de repas">
      <section
        v-for="group in groupedComponents"
        :key="group.componentType"
        class="library-group"
        :aria-label="group.label"
      >
        <div class="library-group-heading">
          <h2>{{ group.label }}</h2>
          <Tag :value="`${group.items.length}`" rounded severity="secondary" />
        </div>

        <div v-if="group.items.length" class="library-card-grid">
          <article v-for="component in group.items" :key="component.id" class="library-card">
            <div class="library-card-title">
              <span class="library-item-icon" aria-hidden="true">{{ component.icon }}</span>
              <div>
                <h3>{{ component.name }}</h3>
                <p>{{ componentTypeLabels[component.componentType] }}</p>
              </div>
            </div>

            <div class="library-metadata">
              <span>{{ component.estimatedCalories }} kcal</span>
              <span>{{ component.estimatedProteinGrams }} g prot.</span>
              <span>{{ portionLabel(component.defaultPortionQuantity, component.unit) }}</span>
            </div>
          </article>
        </div>

        <p v-else class="library-empty">Aucun composant trouvé.</p>
      </section>
    </section>

    <section v-else-if="activeTab === 'dishes'" class="library-content" aria-label="Plats composés">
      <div v-if="filteredDishes.length" class="library-card-grid library-card-grid-wide">
        <article v-for="dish in filteredDishes" :key="dish.id" class="library-card">
          <div class="library-card-title">
            <span class="library-item-icon" aria-hidden="true">{{ dish.icon }}</span>
            <div>
              <h3>{{ dish.name }}</h3>
              <p>{{ dish.estimatedCalories }} kcal · {{ dish.estimatedProteinGrams }} g prot. · {{ dish.preparationTimeMinutes }} min</p>
            </div>
          </div>

          <div class="library-tags">
            <Tag
              v-for="mealLabel in dishMealLabels(dish)"
              :key="mealLabel"
              :value="mealLabel"
              rounded
              severity="secondary"
            />
          </div>
        </article>
      </div>

      <p v-else class="library-empty">Aucun plat composé trouvé.</p>
    </section>

    <section v-else-if="activeTab === 'stores'" class="library-content" aria-label="Magasins">
      <article class="library-card library-store-form">
        <div class="library-group-heading">
          <h2>{{ editingStoreId ? 'Modifier un magasin' : 'Ajouter un magasin' }}</h2>
        </div>
        <div class="library-store-fields">
          <InputText
            v-model="storeName"
            placeholder="Nom du magasin"
            aria-label="Nom du magasin"
          />
          <InputText
            v-model="storeAddress"
            placeholder="Adresse (optionnelle)"
            aria-label="Adresse du magasin"
          />
        </div>
        <p v-if="storeFormError" class="library-empty">{{ storeFormError }}</p>
        <div class="library-store-actions">
          <Button
            :label="editingStoreId ? 'Enregistrer' : 'Ajouter'"
            size="small"
            @click="submitStoreForm"
          />
          <Button
            v-if="editingStoreId"
            label="Annuler"
            severity="secondary"
            outlined
            size="small"
            @click="resetStoreForm"
          />
        </div>
      </article>

      <div v-if="filteredStores.length" class="library-card-grid library-card-grid-wide">
        <article v-for="store in filteredStores" :key="store.id" class="library-card">
          <div class="library-card-title">
            <span class="library-item-icon" aria-hidden="true">🏪</span>
            <div>
              <h3>{{ store.name }}</h3>
              <p>{{ store.address || 'Adresse non renseignée' }}</p>
            </div>
          </div>

          <div class="library-store-actions">
            <Button label="Modifier" severity="secondary" outlined size="small" @click="startStoreEdition(store)" />
            <Button label="Supprimer" severity="danger" text size="small" @click="removeStore(store.id)" />
          </div>
        </article>
      </div>

      <p v-else class="library-empty">Aucun magasin trouvé.</p>
    </section>

    <section v-else class="library-content" aria-label="Activités physiques">
      <div v-if="filteredActivities.length" class="library-card-grid library-card-grid-wide">
        <article v-for="activity in filteredActivities" :key="activity.id" class="library-card library-activity-card">
          <div class="library-card-title">
            <span class="library-item-icon" aria-hidden="true">{{ activity.icon }}</span>
            <div>
              <h3>{{ activity.name }}</h3>
              <p v-if="activity.defaultDurationMinutes">{{ activity.defaultDurationMinutes }} min par défaut</p>
              <p v-else>Sans durée planifiée</p>
            </div>
          </div>
        </article>
      </div>

      <p v-else class="library-empty">Aucune activité trouvée.</p>
    </section>
  </section>
</template>
