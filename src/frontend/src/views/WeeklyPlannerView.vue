<script setup lang="ts">
import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { activities, componentsByType, dishesForMealType } from '@/data/mockLibrary'
import { useWeekPlannerStore } from '@/stores/weekPlanner'
import type { ComponentType, MealType } from '@/types'

const plannerStore = useWeekPlannerStore()
const { weekPlan } = storeToRefs(plannerStore)

const selectedMeal = ref<{ dayId: string; mealType: MealType } | null>(null)
const selectedComponentType = ref<ComponentType | null>(null)
const showDishChoices = ref(false)

const mealTypes: MealType[] = ['breakfast', 'lunch', 'dinner']

const mealTypeLabels: Record<MealType, string> = {
  breakfast: 'Petit-déjeuner',
  lunch: 'Déjeuner',
  dinner: 'Dîner',
}

const componentLabels: Record<ComponentType, string> = {
  protein: 'Sources de protéines',
  starch: 'Féculents',
  vegetable: 'Légumes',
  optional: 'Compléments',
}

const selectedDay = computed(() => {
  if (!selectedMeal.value) {
    return null
  }

  return weekPlan.value.days.find((day) => day.id === selectedMeal.value?.dayId) ?? null
})

const selectedMealSlot = computed(() => {
  if (!selectedMeal.value || !selectedDay.value) {
    return null
  }

  return selectedDay.value[selectedMeal.value.mealType]
})

const today = computed(() => weekPlan.value.days[0] ?? null)

function openMeal(dayId: string, mealType: MealType): void {
  selectedMeal.value = { dayId, mealType }
  selectedComponentType.value = null
  showDishChoices.value = false
}

function closeEditor(): void {
  selectedMeal.value = null
  selectedComponentType.value = null
  showDishChoices.value = false
}

function replaceComponent(componentType: ComponentType, componentId: string): void {
  if (!selectedMeal.value) {
    return
  }

  plannerStore.replaceComponent(
    selectedMeal.value.dayId,
    selectedMeal.value.mealType,
    componentType,
    componentId,
  )
  selectedComponentType.value = null
}

function replaceDish(dishId: string): void {
  if (!selectedMeal.value) {
    return
  }

  plannerStore.replaceDish(selectedMeal.value.dayId, selectedMeal.value.mealType, dishId)
  showDishChoices.value = false
}

function generateWeek(): void {
  plannerStore.generateWeek()
  closeEditor()
}
</script>

<template>
  <main class="app-shell">
    <header class="planner-header">
      <div>
        <p class="eyebrow">LifeOS V0</p>
        <h1>Weekly Planner</h1>
      </div>

      <div class="week-controls" aria-label="Navigation semaine">
        <button class="icon-button" type="button" aria-label="Semaine précédente">←</button>
        <strong>Semaine du 6 au 12 juillet 2026</strong>
        <button class="icon-button" type="button" aria-label="Semaine suivante">→</button>
        <button class="primary-button" type="button" @click="generateWeek">Générer la semaine</button>
      </div>
    </header>

    <section v-if="today" class="mobile-daily-focus" aria-label="Consultation du jour">
      <p class="eyebrow">Aujourd'hui</p>
      <h2>{{ today.dateLabel }} · {{ today.shortDateLabel }}</h2>

      <div class="daily-focus-grid">
        <button class="daily-focus-item" type="button" @click="openMeal(today.id, 'breakfast')">
          <span>Petit-déjeuner</span>
          <strong v-if="today.breakfast.mealDefinition.kind === 'composite'">
            {{ today.breakfast.mealDefinition.name }}
          </strong>
          <strong v-else>
            {{ today.breakfast.mealDefinition.components.map((component) => component.name).join(' + ') }}
          </strong>
        </button>
        <button class="daily-focus-item" type="button" @click="openMeal(today.id, 'lunch')">
          <span>Déjeuner</span>
          <strong v-if="today.lunch.mealDefinition.kind === 'composite'">
            {{ today.lunch.mealDefinition.name }}
          </strong>
          <strong v-else>
            {{ today.lunch.mealDefinition.components.map((component) => component.name).join(' + ') }}
          </strong>
        </button>
        <button class="daily-focus-item" type="button" @click="openMeal(today.id, 'dinner')">
          <span>Dîner</span>
          <strong v-if="today.dinner.mealDefinition.kind === 'composite'">
            {{ today.dinner.mealDefinition.name }}
          </strong>
          <strong v-else>
            {{ today.dinner.mealDefinition.components.map((component) => component.name).join(' + ') }}
          </strong>
        </button>
        <div class="daily-focus-item is-activity">
          <span>Activité</span>
          <strong>{{ today.activity.name }}</strong>
        </div>
      </div>
    </section>

    <section class="planner-panel" aria-label="Planning hebdomadaire">
      <div class="planner-scroll">
        <table class="planner-table">
          <thead>
            <tr>
              <th>Jour</th>
              <th>Petit-déjeuner</th>
              <th>Déjeuner</th>
              <th>Dîner</th>
              <th>Activité physique</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="day in weekPlan.days" :key="day.id">
              <th scope="row" class="day-cell">
                <strong>{{ day.dateLabel }}</strong>
                <span>{{ day.shortDateLabel }}</span>
              </th>

              <td v-for="mealType in mealTypes" :key="mealType">
                <button class="meal-card" type="button" @click="openMeal(day.id, mealType)">
                  <template v-if="day[mealType].mealDefinition.kind === 'assembled'">
                    <span
                      v-for="component in day[mealType].mealDefinition.components"
                      :key="component.id"
                      class="component-line"
                    >
                      <span aria-hidden="true">{{ component.icon }}</span>
                      <span>{{ component.name }}</span>
                    </span>
                  </template>

                  <template v-else>
                    <span class="dish-name">
                      <span aria-hidden="true">{{ day[mealType].mealDefinition.icon }}</span>
                      <span>{{ day[mealType].mealDefinition.name }}</span>
                    </span>
                  </template>

                  <span class="meal-meta">
                    {{ day[mealType].estimatedCalories }} kcal ·
                    {{ day[mealType].estimatedProteinGrams }} g prot.
                  </span>
                  <span class="meal-meta">⏱ {{ day[mealType].preparationTimeMinutes }} min</span>
                </button>
              </td>

              <td>
                <label class="activity-select">
                  <span class="visually-hidden">Activité pour {{ day.dateLabel }}</span>
                  <select
                    :value="day.activity.id"
                    @change="
                      plannerStore.updateActivity(day, ($event.target as HTMLSelectElement).value)
                    "
                  >
                    <option v-for="activity in activities" :key="activity.id" :value="activity.id">
                      {{ activity.name }}
                    </option>
                  </select>
                </label>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <div v-if="selectedMealSlot && selectedDay" class="editor-overlay" @click.self="closeEditor">
      <aside class="meal-editor" aria-label="Éditeur de repas">
        <header class="editor-header">
          <div>
            <p class="eyebrow">{{ mealTypeLabels[selectedMealSlot.mealType] }}</p>
            <h2>{{ selectedDay.dateLabel }} · {{ selectedDay.shortDateLabel }}</h2>
          </div>
          <button class="icon-button" type="button" aria-label="Fermer l'éditeur" @click="closeEditor">
            ×
          </button>
        </header>

        <div class="editor-content">
          <template v-if="selectedMealSlot.mealDefinition.kind === 'assembled'">
            <button
              v-for="component in selectedMealSlot.mealDefinition.components"
              :key="component.componentType"
              class="editor-row"
              type="button"
              @click="selectedComponentType = component.componentType"
            >
              <span>
                <span aria-hidden="true">{{ component.icon }}</span>
                {{ component.name }}
              </span>
              <span aria-hidden="true">›</span>
            </button>

            <section v-if="selectedComponentType" class="choice-list" aria-label="Alternatives composant">
              <h3>{{ componentLabels[selectedComponentType] }}</h3>
              <button
                v-for="component in componentsByType(selectedComponentType)"
                :key="component.id"
                class="choice-row"
                type="button"
                @click="replaceComponent(selectedComponentType, component.id)"
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
                <span aria-hidden="true">{{ selectedMealSlot.mealDefinition.icon }}</span>
                {{ selectedMealSlot.mealDefinition.name }}
              </span>
              <span aria-hidden="true">›</span>
            </button>

            <section v-if="showDishChoices" class="choice-list" aria-label="Alternatives plat composé">
              <h3>Plats compatibles</h3>
              <button
                v-for="dish in dishesForMealType(selectedMealSlot.mealType)"
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

        <footer class="editor-footer">
          <p>
            {{ selectedMealSlot.estimatedCalories }} kcal ·
            {{ selectedMealSlot.estimatedProteinGrams }} g prot.
          </p>
          <p>⏱ {{ selectedMealSlot.preparationTimeMinutes }} min</p>
          <button class="primary-button" type="button" @click="closeEditor">Valider</button>
        </footer>
      </aside>
    </div>
  </main>
</template>
