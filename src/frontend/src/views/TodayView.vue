<script setup lang="ts">
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import { useWeekPlannerStore } from '@/stores/weekPlanner'
import type { MealSlot, MealType } from '@/types'

const plannerStore = useWeekPlannerStore()
const { weekPlan } = storeToRefs(plannerStore)

const today = computed(() => weekPlan.value.days[0] ?? null)

const mealTypeLabels: Record<MealType, string> = {
  breakfast: 'Petit-déjeuner',
  lunch: 'Déjeuner',
  dinner: 'Dîner',
}

const todayMeals = computed(() => {
  if (!today.value) {
    return []
  }

  return [
    { mealType: 'breakfast' as const, slot: today.value.breakfast },
    { mealType: 'lunch' as const, slot: today.value.lunch },
    { mealType: 'dinner' as const, slot: today.value.dinner },
  ]
})

function mealName(slot: MealSlot): string {
  if (slot.mealDefinition.kind === 'composite') {
    return slot.mealDefinition.name
  }

  return slot.mealDefinition.components.map((component) => component.name).join(' + ')
}
</script>

<template>
  <section class="page-stack today-page">
    <header v-if="today" class="page-hero today-hero">
      <p class="eyebrow">Aujourd'hui</p>
      <h1>{{ today.dateLabel }} {{ today.shortDateLabel }}</h1>
      <p>Le plan du jour, juste ce qu'il faut pour ne pas y repenser.</p>
    </header>

    <div v-if="today" class="today-content">
      <section class="today-meals" aria-label="Repas du jour">
        <article v-for="meal in todayMeals" :key="meal.mealType" class="today-card">
          <div class="today-card-heading">
            <span>{{ mealTypeLabels[meal.mealType] }}</span>
            <strong>{{ mealName(meal.slot) }}</strong>
          </div>

          <div class="today-meal-body">
            <template v-if="meal.slot.mealDefinition.kind === 'assembled'">
              <p
                v-for="component in meal.slot.mealDefinition.components"
                :key="component.id"
                class="meal-component"
              >
                <span aria-hidden="true">{{ component.icon }}</span>
                <span>{{ component.name }}</span>
              </p>
            </template>

            <p v-else class="meal-dish">
              <span aria-hidden="true">{{ meal.slot.mealDefinition.icon }}</span>
              <span>{{ meal.slot.mealDefinition.name }}</span>
            </p>
          </div>

          <p class="meal-metadata">
            {{ meal.slot.estimatedCalories }} kcal ·
            {{ meal.slot.estimatedProteinGrams }} g prot. ·
            {{ meal.slot.preparationTimeMinutes }} min
          </p>
        </article>
      </section>

      <aside class="today-activity" aria-label="Activité du jour">
        <span class="activity-icon" aria-hidden="true">
          <i class="pi pi-heart"></i>
        </span>
        <div>
          <p class="eyebrow">Activité physique</p>
          <h2>{{ today.activity.name }}</h2>
        </div>
      </aside>

      <RouterLink class="today-planner-link" to="/planner">
        <span>Voir toute la semaine</span>
        <i class="pi pi-arrow-right" aria-hidden="true"></i>
      </RouterLink>
    </div>
  </section>
</template>
