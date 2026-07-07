<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import MealEditorDrawer from '@/components/MealEditorDrawer.vue'
import { useWeekContextStore, workLocationLabels } from '@/stores/weekContext'
import { useWeekPlannerStore } from '@/stores/weekPlanner'
import type { MealSlot, MealType } from '@/types'

const plannerStore = useWeekPlannerStore()
const weekContextStore = useWeekContextStore()
const { weekPlan } = storeToRefs(plannerStore)
const { weekContext } = storeToRefs(weekContextStore)

const today = computed(() => weekPlan.value.days[0] ?? null)
const isMobile = ref(false)
const selectedMealType = ref<MealType | null>(null)

const mealTypeLabels: Record<MealType, string> = {
  breakfast: 'Petit-déjeuner',
  lunch: 'Déjeuner',
  dinner: 'Dîner',
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

  return today.value.activity.defaultDurationMinutes
    ? `${today.value.activity.defaultDurationMinutes} min`
    : undefined
})

const todayContextLabel = computed(() => {
  const context = weekContext.value.days.monday
  const labels = [workLocationLabels[context.workLocation]]

  if (context.bikeCommute) {
    labels.push('Vélo')
  }

  labels.push(weekContext.value.weekMode === 'kids' ? 'Enfants' : 'Solo')

  return labels.join(' · ')
})

function mealName(slot: MealSlot): string {
  if (slot.mealDefinition.kind === 'composite') {
    return slot.mealDefinition.name
  }

  return slot.mealDefinition.components.map((component) => component.name).join(' + ')
}

function openMeal(mealType: MealType): void {
  selectedMealType.value = mealType
}

function closeEditor(): void {
  selectedMealType.value = null
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
        <p class="today-context">{{ todayContextLabel }}</p>
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
          {{ today.activity.icon }}
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

    <MealEditorDrawer
      v-model:visible="drawerVisible"
      :day-id="today?.id ?? null"
      :meal-slot="displayedMeal"
      :title="mealEditorTitle"
      :position="drawerPosition"
      :drawer-style="drawerStyle"
    />
  </section>
</template>
