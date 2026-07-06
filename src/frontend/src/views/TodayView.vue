<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'
import Tag from 'primevue/tag'
import { componentsByType, dishesForMealType } from '@/data/mockLibrary'
import { useWeekPlannerStore } from '@/stores/weekPlanner'
import type { ComponentType, MealComponent, MealSlot, MealType } from '@/types'

const plannerStore = useWeekPlannerStore()
const { weekPlan } = storeToRefs(plannerStore)

const today = computed(() => weekPlan.value.days[0] ?? null)
const isMobile = ref(false)
const selectedMealType = ref<MealType | null>(null)
const selectedComponent = ref<{ componentIndex: number; componentType: ComponentType } | null>(null)
const showDishChoices = ref(false)

const mealTypeLabels: Record<MealType, string> = {
  breakfast: 'Petit-déjeuner',
  lunch: 'Déjeuner',
  dinner: 'Dîner',
}

const activityDurationByName: Record<string, string | undefined> = {
  Repos: undefined,
  Marche: '30 min',
  Running: '35 min',
  'Running long': '55 min',
  Renforcement: '40 min',
  Mobilité: '20 min',
  Vélo: '45 min',
  'Activité familiale': '60 min',
}

const displayedMeal = computed(() => {
  if (!today.value || !selectedMealType.value) {
    return null
  }

  return today.value[selectedMealType.value]
})

const drawerVisible = computed({
  get: () => Boolean(displayedMeal.value),
  set: (visible: boolean) => {
    if (!visible) {
      closeEditor()
    }
  },
})

const drawerPosition = computed(() => (isMobile.value ? 'bottom' : 'right'))

const drawerStyle = computed(() =>
  isMobile.value ? { height: '88vh' } : { width: '460px', maxWidth: '100vw' },
)

const todaySummary = computed(() => {
  if (!today.value) {
    return { calories: 0, protein: 0 }
  }

  const meals = [today.value.breakfast, today.value.lunch, today.value.dinner]

  return meals.reduce(
    (totals, slot) => ({
      calories: totals.calories + slot.estimatedCalories,
      protein: totals.protein + slot.estimatedProteinGrams,
    }),
    { calories: 0, protein: 0 },
  )
})

const compatibleComponents = computed<MealComponent[]>(() => {
  if (!selectedComponent.value) {
    return []
  }

  return componentsByType(selectedComponent.value.componentType)
})

const availableDishes = computed(() => {
  if (!selectedMealType.value) {
    return []
  }

  return dishesForMealType(selectedMealType.value)
})

const mealEditorTitle = computed(() => {
  if (!today.value || !selectedMealType.value) {
    return ''
  }

  return `${mealTypeLabels[selectedMealType.value]} · ${today.value.dateLabel} ${today.value.shortDateLabel}`
})

const activityDuration = computed(() => {
  if (!today.value) {
    return undefined
  }

  return activityDurationByName[today.value.activity.name]
})

function mealName(slot: MealSlot): string {
  if (slot.mealDefinition.kind === 'composite') {
    return slot.mealDefinition.name
  }

  return slot.mealDefinition.components.map((component) => component.name).join(' + ')
}

function openMeal(mealType: MealType): void {
  selectedMealType.value = mealType
  selectedComponent.value = null
  showDishChoices.value = false
}

function closeEditor(): void {
  selectedMealType.value = null
  selectedComponent.value = null
  showDishChoices.value = false
}

function openComponentChoices(componentIndex: number, componentType: ComponentType): void {
  selectedComponent.value = { componentIndex, componentType }
  showDishChoices.value = false
}

function replaceComponent(componentId: string): void {
  if (!today.value || !selectedMealType.value || !selectedComponent.value) {
    return
  }

  plannerStore.replaceComponent(
    today.value.id,
    selectedMealType.value,
    selectedComponent.value.componentIndex,
    selectedComponent.value.componentType,
    componentId,
  )
  selectedComponent.value = null
}

function replaceDish(dishId: string): void {
  if (!today.value || !selectedMealType.value) {
    return
  }

  plannerStore.replaceDish(today.value.id, selectedMealType.value, dishId)
  showDishChoices.value = false
}

function syncViewport(): void {
  isMobile.value = window.matchMedia('(max-width: 760px)').matches
}

onMounted(() => {
  syncViewport()
  window.addEventListener('resize', syncViewport)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', syncViewport)
})
</script>

<template>
  <section class="page-stack today-page">
    <header v-if="today" class="page-hero today-hero">
      <div>
        <p class="eyebrow">Aujourd'hui</p>
        <h1>{{ today.dateLabel }} {{ today.shortDateLabel }}</h1>
        <p>Une journée claire, sans bruit inutile.</p>
      </div>
    </header>

    <div v-if="today" class="today-content">
      <section class="today-summary" aria-label="Résumé quotidien">
        <div>
          <span class="today-summary-label">Charge nutritionnelle</span>
          <strong>{{ todaySummary.calories }} kcal</strong>
        </div>
        <div>
          <span class="today-summary-label">Protéines totales</span>
          <strong>{{ todaySummary.protein }} g</strong>
        </div>
      </section>

      <section class="today-meals" aria-label="Repas du jour">
        <button class="today-card" type="button" @click="openMeal('breakfast')">
          <div class="today-card-heading">
            <span>{{ mealTypeLabels.breakfast }}</span>
            <strong>{{ mealName(today.breakfast) }}</strong>
          </div>

          <div class="today-meal-body">
            <template v-if="today.breakfast.mealDefinition.kind === 'assembled'">
              <p
                v-for="component in today.breakfast.mealDefinition.components"
                :key="component.id"
                class="meal-component"
              >
                <span aria-hidden="true">{{ component.icon }}</span>
                <span>{{ component.name }}</span>
              </p>
            </template>

            <p v-else class="meal-dish">
              <span aria-hidden="true">{{ today.breakfast.mealDefinition.icon }}</span>
              <span>{{ today.breakfast.mealDefinition.name }}</span>
            </p>
          </div>

          <p class="meal-metadata">
            {{ today.breakfast.estimatedCalories }} kcal ·
            {{ today.breakfast.estimatedProteinGrams }} g prot. ·
            {{ today.breakfast.preparationTimeMinutes }} min
          </p>
        </button>

        <button class="today-card" type="button" @click="openMeal('lunch')">
          <div class="today-card-heading">
            <span>{{ mealTypeLabels.lunch }}</span>
            <strong>{{ mealName(today.lunch) }}</strong>
          </div>

          <div class="today-meal-body">
            <template v-if="today.lunch.mealDefinition.kind === 'assembled'">
              <p
                v-for="component in today.lunch.mealDefinition.components"
                :key="component.id"
                class="meal-component"
              >
                <span aria-hidden="true">{{ component.icon }}</span>
                <span>{{ component.name }}</span>
              </p>
            </template>

            <p v-else class="meal-dish">
              <span aria-hidden="true">{{ today.lunch.mealDefinition.icon }}</span>
              <span>{{ today.lunch.mealDefinition.name }}</span>
            </p>
          </div>

          <p class="meal-metadata">
            {{ today.lunch.estimatedCalories }} kcal ·
            {{ today.lunch.estimatedProteinGrams }} g prot. ·
            {{ today.lunch.preparationTimeMinutes }} min
          </p>
        </button>

        <button class="today-card" type="button" @click="openMeal('dinner')">
          <div class="today-card-heading">
            <span>{{ mealTypeLabels.dinner }}</span>
            <strong>{{ mealName(today.dinner) }}</strong>
          </div>

          <div class="today-meal-body">
            <template v-if="today.dinner.mealDefinition.kind === 'assembled'">
              <p
                v-for="component in today.dinner.mealDefinition.components"
                :key="component.id"
                class="meal-component"
              >
                <span aria-hidden="true">{{ component.icon }}</span>
                <span>{{ component.name }}</span>
              </p>
            </template>

            <p v-else class="meal-dish">
              <span aria-hidden="true">{{ today.dinner.mealDefinition.icon }}</span>
              <span>{{ today.dinner.mealDefinition.name }}</span>
            </p>
          </div>

          <p class="meal-metadata">
            {{ today.dinner.estimatedCalories }} kcal ·
            {{ today.dinner.estimatedProteinGrams }} g prot. ·
            {{ today.dinner.preparationTimeMinutes }} min
          </p>
        </button>
      </section>

      <section class="today-activity" aria-label="Activité du jour">
        <span class="activity-icon" aria-hidden="true">
          <i class="pi pi-heart"></i>
        </span>
        <div>
          <p class="eyebrow">Activité physique</p>
          <h2>{{ today.activity.name }}</h2>
          <p v-if="activityDuration" class="activity-duration">{{ activityDuration }}</p>
        </div>
      </section>

      <RouterLink class="today-planner-link" to="/planner">
        <span>Voir toute la semaine</span>
        <i class="pi pi-arrow-right" aria-hidden="true"></i>
      </RouterLink>
    </div>

    <Drawer
      v-model:visible="drawerVisible"
      class="planner-editor-drawer"
      :position="drawerPosition"
      :style="drawerStyle"
      modal
      block-scroll
      :dismissable="true"
      :header="mealEditorTitle"
    >
      <template #header>
        <div class="editor-heading">
          <p class="eyebrow">Édition repas</p>
          <h2>{{ mealEditorTitle }}</h2>
        </div>
      </template>

      <template v-if="displayedMeal && today && selectedMealType">
        <div class="editor-content">
          <p class="editor-summary">{{ mealName(displayedMeal) }}</p>

          <template v-if="displayedMeal.mealDefinition.kind === 'assembled'">
            <button
              v-for="(component, componentIndex) in displayedMeal.mealDefinition.components"
              :key="`${component.componentType}-${component.id}-${componentIndex}`"
              class="editor-row"
              type="button"
              @click="openComponentChoices(componentIndex, component.componentType)"
            >
              <span>
                <span aria-hidden="true">{{ component.icon }}</span>
                {{ component.name }}
              </span>
              <i class="pi pi-chevron-right" aria-hidden="true"></i>
            </button>

            <section v-if="selectedComponent" class="choice-list" aria-label="Alternatives composant">
              <div class="choice-list-header">
                <h3>Choisir un remplacement</h3>
                <Button
                  label="Retour"
                  icon="pi pi-arrow-left"
                  severity="secondary"
                  text
                  size="small"
                  @click="selectedComponent = null"
                />
              </div>
              <button
                v-for="component in compatibleComponents"
                :key="component.id"
                class="choice-row"
                type="button"
                @click="replaceComponent(component.id)"
              >
                <span>
                  <span aria-hidden="true">{{ component.icon }}</span>
                  {{ component.name }}
                </span>
                <small>{{ component.estimatedCalories }} kcal · {{ component.estimatedProteinGrams }} g prot.</small>
              </button>
            </section>
          </template>

          <template v-else>
            <button class="editor-row" type="button" @click="showDishChoices = !showDishChoices">
              <span>
                <span aria-hidden="true">{{ displayedMeal.mealDefinition.icon }}</span>
                {{ displayedMeal.mealDefinition.name }}
              </span>
              <i class="pi pi-chevron-right" aria-hidden="true"></i>
            </button>

            <section v-if="showDishChoices" class="choice-list" aria-label="Alternatives plat composé">
              <h3>Plats compatibles</h3>
              <button
                v-for="dish in availableDishes"
                :key="dish.id"
                class="choice-row"
                type="button"
                @click="replaceDish(dish.id)"
              >
                <span>
                  <span aria-hidden="true">{{ dish.icon }}</span>
                  {{ dish.name }}
                </span>
                <small>{{ dish.estimatedCalories }} kcal · {{ dish.estimatedProteinGrams }} g prot.</small>
              </button>
            </section>
          </template>
        </div>
      </template>

      <template #footer>
        <footer v-if="displayedMeal" class="editor-footer">
          <div class="editor-tags">
            <Tag
              :value="`${displayedMeal.estimatedCalories} kcal · ${displayedMeal.estimatedProteinGrams} g prot.`"
              severity="secondary"
              rounded
            />
            <Tag
              :value="`${displayedMeal.preparationTimeMinutes} min`"
              icon="pi pi-clock"
              severity="info"
              rounded
            />
          </div>
          <Button class="editor-validate" label="Valider" icon="pi pi-check" @click="closeEditor" />
        </footer>
      </template>
    </Drawer>
  </section>
</template>
