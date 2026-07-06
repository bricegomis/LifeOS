<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import { activities, componentsByType, dishesForMealType } from '@/data/mockLibrary'
import { useWeekPlannerStore } from '@/stores/weekPlanner'
import type { ComponentType, MealComponent, MealSlot, MealType, WeekPlan } from '@/types'

const plannerStore = useWeekPlannerStore()
const { weekPlan } = storeToRefs(plannerStore)

const selectedMeal = ref<{ dayId: string; mealType: MealType } | null>(null)
const selectedActivityDayId = ref<string | null>(null)
const selectedComponent = ref<{ componentIndex: number; componentType: ComponentType } | null>(null)
const showDishChoices = ref(false)
const isMobile = ref(false)
const weekOffset = ref(0)

const mealTypes: MealType[] = ['breakfast', 'lunch', 'dinner']

const mealTypeLabels: Record<MealType, string> = {
  breakfast: 'Petit-déjeuner',
  lunch: 'Déjeuner',
  dinner: 'Dîner',
}

const displayedWeekPlan = computed<WeekPlan>(() => shiftWeekPlan(weekPlan.value, weekOffset.value))

const displayedSelectedDay = computed(() => {
  if (!selectedMeal.value) {
    return null
  }

  return displayedWeekPlan.value.days.find((day) => day.id === selectedMeal.value?.dayId) ?? null
})

const displayedSelectedMealSlot = computed(() => {
  if (!selectedMeal.value || !displayedSelectedDay.value) {
    return null
  }

  return displayedSelectedDay.value[selectedMeal.value.mealType]
})

const displayedSelectedActivityDay = computed(() => {
  if (!selectedActivityDayId.value) {
    return null
  }

  return displayedWeekPlan.value.days.find((day) => day.id === selectedActivityDayId.value) ?? null
})

const mealEditorVisible = computed({
  get: () => Boolean(displayedSelectedMealSlot.value && displayedSelectedDay.value),
  set: (visible: boolean) => {
    if (!visible) {
      closeMealEditor()
    }
  },
})

const activityEditorVisible = computed({
  get: () => Boolean(displayedSelectedActivityDay.value),
  set: (visible: boolean) => {
    if (!visible) {
      closeActivityEditor()
    }
  },
})

const drawerPosition = computed(() => (isMobile.value ? 'bottom' : 'right'))

const drawerStyle = computed(() =>
  isMobile.value ? { height: '86vh' } : { width: '460px', maxWidth: '100vw' },
)

const mealEditorSubtitle = computed(() => {
  if (!displayedSelectedDay.value || !displayedSelectedMealSlot.value) {
    return ''
  }

  return `${mealTypeLabels[displayedSelectedMealSlot.value.mealType]} · ${displayedSelectedDay.value.dateLabel} ${displayedSelectedDay.value.shortDateLabel}`
})

const activityEditorSubtitle = computed(() => {
  if (!displayedSelectedActivityDay.value) {
    return ''
  }

  return `Activité · ${displayedSelectedActivityDay.value.dateLabel} ${displayedSelectedActivityDay.value.shortDateLabel}`
})

const weekLabel = computed(() => formatWeekLabel(weekPlan.value.startDate, weekOffset.value))

const compatibleComponents = computed<MealComponent[]>(() => {
  if (!selectedComponent.value) {
    return []
  }

  return componentsByType(selectedComponent.value.componentType)
})

function mealSummary(slot: MealSlot): string {
  if (slot.mealDefinition.kind === 'composite') {
    return slot.mealDefinition.name
  }

  return slot.mealDefinition.components.map((component) => component.name).join(' + ')
}

function openMeal(dayId: string, mealType: MealType): void {
  selectedActivityDayId.value = null
  selectedMeal.value = { dayId, mealType }
  selectedComponent.value = null
  showDishChoices.value = false
}

function closeMealEditor(): void {
  selectedMeal.value = null
  selectedComponent.value = null
  showDishChoices.value = false
}

function openActivity(dayId: string): void {
  closeMealEditor()
  selectedActivityDayId.value = dayId
}

function closeActivityEditor(): void {
  selectedActivityDayId.value = null
}

function closeEditors(): void {
  closeMealEditor()
  closeActivityEditor()
}

function openComponentChoices(componentIndex: number, componentType: ComponentType): void {
  selectedComponent.value = { componentIndex, componentType }
  showDishChoices.value = false
}

function replaceComponent(componentId: string): void {
  if (!selectedMeal.value || !selectedComponent.value) {
    return
  }

  plannerStore.replaceComponent(
    selectedMeal.value.dayId,
    selectedMeal.value.mealType,
    selectedComponent.value.componentIndex,
    selectedComponent.value.componentType,
    componentId,
  )
  selectedComponent.value = null
}

function replaceDish(dishId: string): void {
  if (!selectedMeal.value) {
    return
  }

  plannerStore.replaceDish(selectedMeal.value.dayId, selectedMeal.value.mealType, dishId)
  showDishChoices.value = false
}

function updateSelectedActivity(activityId: string): void {
  if (!selectedActivityDayId.value) {
    return
  }

  plannerStore.updateActivity(selectedActivityDayId.value, activityId)
}

function resetDemoWeek(): void {
  plannerStore.resetDemoWeek()
  weekOffset.value = 0
  closeEditors()
}

function previousWeek(): void {
  weekOffset.value -= 1
  closeEditors()
}

function nextWeek(): void {
  weekOffset.value += 1
  closeEditors()
}

function syncViewport(): void {
  isMobile.value = window.matchMedia('(max-width: 760px)').matches
}

function formatWeekLabel(startDateValue: string, offsetWeeks: number): string {
  const startDate = new Date(`${startDateValue}T00:00:00`)
  startDate.setDate(startDate.getDate() + offsetWeeks * 7)

  const endDate = new Date(startDate)
  endDate.setDate(endDate.getDate() + 6)

  const formatter = new Intl.DateTimeFormat('fr-FR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })

  return `Semaine du ${formatter.format(startDate)} au ${formatter.format(endDate)}`
}

function shiftWeekPlan(plan: WeekPlan, offsetWeeks: number): WeekPlan {
  const offsetDays = offsetWeeks * 7
  const weekdayFormatter = new Intl.DateTimeFormat('fr-FR', { weekday: 'long' })
  const shortFormatter = new Intl.DateTimeFormat('fr-FR', {
    day: 'numeric',
    month: 'short',
  })
  const baseStart = new Date(`${plan.startDate}T00:00:00`)

  return {
    ...plan,
    id: `${plan.id}-view-${offsetWeeks}`,
    startDate: shiftDate(plan.startDate, offsetDays),
    days: plan.days.map((day, index) => {
      const date = new Date(baseStart)
      date.setDate(date.getDate() + offsetDays + index)
      const dateLabel = weekdayFormatter.format(date)
      const shortDateLabel = shortFormatter.format(date)

      return {
        ...day,
        dateLabel: dateLabel.charAt(0).toUpperCase() + dateLabel.slice(1),
        shortDateLabel,
      }
    }),
  }
}

function shiftDate(startDate: string, offsetDays: number): string {
  const date = new Date(`${startDate}T00:00:00`)
  date.setDate(date.getDate() + offsetDays)

  return date.toISOString().slice(0, 10)
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
  <section class="page-stack planner-page">
    <header class="page-hero planner-hero">
      <div>
        <p class="eyebrow">Planning semaine</p>
        <h1>{{ weekLabel }}</h1>
        <p>Une vue claire pour préparer la semaine sans la transformer en tableur.</p>
      </div>

      <div class="week-controls" aria-label="Navigation semaine">
        <Button
          icon="pi pi-chevron-left"
          text
          rounded
          severity="secondary"
          aria-label="Semaine précédente"
          @click="previousWeek"
        />
        <Button
          icon="pi pi-chevron-right"
          text
          rounded
          severity="secondary"
          aria-label="Semaine suivante"
          @click="nextWeek"
        />
        <Button label="Générer la semaine" icon="pi pi-refresh" @click="resetDemoWeek" />
      </div>
    </header>

    <section class="week-planner-surface" aria-label="Planning hebdomadaire">
      <div class="planner-scroll">
        <table class="week-table">
          <thead>
            <tr>
              <th>Jour</th>
              <th>Petit-déjeuner</th>
              <th>Déjeuner</th>
              <th>Dîner</th>
              <th>Activité</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="day in displayedWeekPlan.days" :key="day.id">
              <th scope="row" class="week-day-cell">
                <span class="day-name">{{ day.dateLabel }}</span>
                <span class="day-date">{{ day.shortDateLabel }}</span>
              </th>

              <td v-for="mealType in mealTypes" :key="mealType">
                <button class="week-meal-card" type="button" @click="openMeal(day.id, mealType)">
                  <span class="meal-kind">{{ mealTypeLabels[mealType] }}</span>

                  <template v-if="day[mealType].mealDefinition.kind === 'assembled'">
                    <span
                      v-for="component in day[mealType].mealDefinition.components"
                      :key="component.id"
                      class="meal-component"
                    >
                      <span aria-hidden="true">{{ component.icon }}</span>
                      <span>{{ component.name }}</span>
                    </span>
                  </template>

                  <span v-else class="meal-dish">
                    <span aria-hidden="true">{{ day[mealType].mealDefinition.icon }}</span>
                    <span>{{ day[mealType].mealDefinition.name }}</span>
                  </span>

                  <span class="meal-metadata">
                    {{ day[mealType].estimatedCalories }} kcal ·
                    {{ day[mealType].estimatedProteinGrams }} g prot. ·
                    {{ day[mealType].preparationTimeMinutes }} min
                  </span>
                </button>
              </td>

              <td class="activity-cell">
                <button class="activity-button" type="button" @click="openActivity(day.id)">
                  <i class="pi pi-heart" aria-hidden="true"></i>
                  <span>{{ day.activity.name }}</span>
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <Drawer
      v-model:visible="mealEditorVisible"
      class="planner-editor-drawer"
      :position="drawerPosition"
      :style="drawerStyle"
      modal
      block-scroll
      :dismissable="true"
      :header="mealEditorSubtitle"
    >
      <template #header>
        <div class="editor-heading">
          <p class="eyebrow">Édition repas</p>
          <h2>{{ mealEditorSubtitle }}</h2>
        </div>
      </template>

      <template v-if="displayedSelectedMealSlot && displayedSelectedDay">
        <div class="editor-content">
          <p class="editor-summary">{{ mealSummary(displayedSelectedMealSlot) }}</p>

          <template v-if="displayedSelectedMealSlot.mealDefinition.kind === 'assembled'">
            <button
              v-for="(component, componentIndex) in displayedSelectedMealSlot.mealDefinition.components"
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
                <span aria-hidden="true">{{ displayedSelectedMealSlot.mealDefinition.icon }}</span>
                {{ displayedSelectedMealSlot.mealDefinition.name }}
              </span>
              <i class="pi pi-chevron-right" aria-hidden="true"></i>
            </button>

            <section v-if="showDishChoices" class="choice-list" aria-label="Alternatives plat composé">
              <h3>Plats compatibles</h3>
              <button
                v-for="dish in dishesForMealType(displayedSelectedMealSlot.mealType)"
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
        <footer v-if="displayedSelectedMealSlot" class="editor-footer">
          <div class="editor-tags">
            <Tag
              :value="`${displayedSelectedMealSlot.estimatedCalories} kcal · ${displayedSelectedMealSlot.estimatedProteinGrams} g prot.`"
              severity="secondary"
              rounded
            />
            <Tag
              :value="`${displayedSelectedMealSlot.preparationTimeMinutes} min`"
              icon="pi pi-clock"
              severity="info"
              rounded
            />
          </div>
          <Button class="editor-validate" label="Valider" icon="pi pi-check" @click="closeMealEditor" />
        </footer>
      </template>
    </Drawer>

    <Drawer
      v-model:visible="activityEditorVisible"
      class="planner-editor-drawer"
      :position="drawerPosition"
      :style="drawerStyle"
      modal
      block-scroll
      :dismissable="true"
      :header="activityEditorSubtitle"
    >
      <template #header>
        <div class="editor-heading">
          <p class="eyebrow">Édition activité</p>
          <h2>{{ activityEditorSubtitle }}</h2>
        </div>
      </template>

      <div v-if="displayedSelectedActivityDay" class="editor-content">
        <div class="activity-editor-current">
          <span class="activity-icon" aria-hidden="true">
            <i class="pi pi-heart"></i>
          </span>
          <div>
            <span>Activité prévue</span>
            <strong>{{ displayedSelectedActivityDay.activity.name }}</strong>
          </div>
        </div>

        <label class="editor-field">
          <span>Remplacer par</span>
          <Select
            :model-value="displayedSelectedActivityDay.activity.id"
            :options="activities"
            option-label="name"
            option-value="id"
            fluid
            @update:model-value="updateSelectedActivity($event as string)"
          />
        </label>
      </div>

      <template #footer>
        <footer class="editor-footer">
          <Button class="editor-validate" label="Valider" icon="pi pi-check" @click="closeActivityEditor" />
        </footer>
      </template>
    </Drawer>
  </section>
</template>
