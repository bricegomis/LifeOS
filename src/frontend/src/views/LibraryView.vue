<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Tag from 'primevue/tag'
import { activities, compositeDishes, mealComponents } from '@/data/localLibrary'
import type { ComponentType, CompositeDish } from '@/types'

type LibraryTab = 'components' | 'dishes' | 'activities'

const activeTab = ref<LibraryTab>('components')
const searchQuery = ref('')

const tabs = computed<{ id: LibraryTab; label: string; count: number }[]>(() => [
  { id: 'components', label: 'Composants', count: mealComponents.length },
  { id: 'dishes', label: 'Plats composés', count: compositeDishes.length },
  { id: 'activities', label: 'Activités', count: activities.length },
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
