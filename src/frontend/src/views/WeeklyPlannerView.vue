<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import MealEditorDrawer from '@/components/MealEditorDrawer.vue'
import { activities } from '@/data/localLibrary'
import { usePlanningRulesStore } from '@/stores/planningRules'
import { useWeekPlannerStore } from '@/stores/weekPlanner'
import type { MealType, WeekPlan } from '@/types'

const plannerStore = useWeekPlannerStore()
const planningRulesStore = usePlanningRulesStore()
const { weekPlan } = storeToRefs(plannerStore)
const { planningRules, frequencyRules } = storeToRefs(planningRulesStore)

const selectedMeal = ref<{ dayId: string; mealType: MealType } | null>(null)
const activeActivityDayId = ref<string | null>(null)
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

const mealEditorVisible = computed({
  get: () => Boolean(displayedSelectedMealSlot.value && displayedSelectedDay.value),
  set: (visible: boolean) => {
    if (!visible) {
      closeMealEditor()
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

const weekLabel = computed(() => formatWeekLabel(weekPlan.value.startDate, weekOffset.value))

const weekSummary = computed(() => {
  const dayCount = displayedWeekPlan.value.days.length || 1
  const totals = displayedWeekPlan.value.days.reduce(
    (summary, day) => {
      const meals = [day.breakfast, day.lunch, day.dinner]

      return {
        calories: summary.calories + meals.reduce((total, meal) => total + meal.estimatedCalories, 0),
        protein: summary.protein + meals.reduce((total, meal) => total + meal.estimatedProteinGrams, 0),
        activities: summary.activities + (day.activity.id === 'rest' ? 0 : 1),
      }
    },
    { calories: 0, protein: 0, activities: 0 },
  )

  return {
    averageCalories: Math.round(totals.calories / dayCount),
    averageProtein: Math.round(totals.protein / dayCount),
    plannedActivities: totals.activities,
  }
})

function openMeal(dayId: string, mealType: MealType): void {
  activeActivityDayId.value = null
  selectedMeal.value = { dayId, mealType }
}

function closeMealEditor(): void {
  selectedMeal.value = null
}

function openActivity(dayId: string): void {
  closeMealEditor()
  activeActivityDayId.value = activeActivityDayId.value === dayId ? null : dayId
}

function closeEditors(): void {
  closeMealEditor()
  activeActivityDayId.value = null
}

function updateActivity(dayId: string, activityId: string): void {
  plannerStore.updateActivity(dayId, activityId)
  activeActivityDayId.value = null
}

function activityDuration(durationMinutes: number | undefined): string | undefined {
  return durationMinutes ? `${durationMinutes} min` : undefined
}

function resetDemoWeek(): void {
  plannerStore.generateWeek(planningRules.value, frequencyRules.value)
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
      <div class="week-title-row">
        <Button
          icon="pi pi-chevron-left"
          text
          rounded
          severity="secondary"
          aria-label="Semaine précédente"
          @click="previousWeek"
        />
        <div>
          <p class="eyebrow">Planning semaine</p>
          <h1>{{ weekLabel }}</h1>
          <p>Une vue claire pour préparer la semaine sans la transformer en tableur.</p>
        </div>
        <Button
          icon="pi pi-chevron-right"
          text
          rounded
          severity="secondary"
          aria-label="Semaine suivante"
          @click="nextWeek"
        />
      </div>

      <div class="week-controls" aria-label="Navigation semaine">
        <Button label="Générer la semaine" icon="pi pi-refresh" @click="resetDemoWeek" />
      </div>
    </header>

    <section class="week-summary-strip" aria-label="Résumé de semaine">
      <div>
        <span>Protéines / jour</span>
        <strong>{{ weekSummary.averageProtein }} g</strong>
      </div>
      <div>
        <span>Kcal / jour</span>
        <strong>{{ weekSummary.averageCalories }}</strong>
      </div>
      <div>
        <span>Activités prévues</span>
        <strong>{{ weekSummary.plannedActivities }}</strong>
      </div>
    </section>

    <section class="week-planner-surface" aria-label="Planning hebdomadaire">
      <div class="planner-scroll">
        <div class="week-grid" role="table" aria-label="Repas et activités de la semaine">
          <div class="week-grid-header" role="row">
            <span role="columnheader">Jour</span>
            <span role="columnheader">Petit-déjeuner</span>
            <span role="columnheader">Déjeuner</span>
            <span role="columnheader">Dîner</span>
            <span role="columnheader">Activité</span>
          </div>

          <div v-for="day in displayedWeekPlan.days" :key="day.id" class="week-grid-row" role="row">
            <div class="week-day-cell" role="rowheader">
              <span class="day-name">{{ day.dateLabel }}</span>
              <span class="day-date">{{ day.shortDateLabel }}</span>
            </div>

            <div v-for="mealType in mealTypes" :key="mealType" class="week-meal-cell" role="cell">
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
            </div>

            <div class="activity-cell" role="cell">
              <button
                class="activity-button"
                type="button"
                :aria-expanded="activeActivityDayId === day.id"
                @click="openActivity(day.id)"
              >
                <span class="activity-symbol" aria-hidden="true">{{ day.activity.icon }}</span>
                <span>
                  <strong>{{ day.activity.name }}</strong>
                  <small v-if="activityDuration(day.activity.defaultDurationMinutes)">
                    {{ activityDuration(day.activity.defaultDurationMinutes) }}
                  </small>
                </span>
              </button>

              <div v-if="activeActivityDayId === day.id" class="activity-choice-panel">
                <button
                  v-for="activity in activities"
                  :key="activity.id"
                  class="activity-choice"
                  type="button"
                  :class="{ 'is-selected': activity.id === day.activity.id }"
                  @click="updateActivity(day.id, activity.id)"
                >
                  <span aria-hidden="true">{{ activity.icon }}</span>
                  <span>{{ activity.name }}</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <MealEditorDrawer
      v-model:visible="mealEditorVisible"
      :day-id="selectedMeal?.dayId ?? null"
      :meal-slot="displayedSelectedMealSlot"
      :title="mealEditorSubtitle"
      :position="drawerPosition"
      :drawer-style="drawerStyle"
    />
  </section>
</template>
