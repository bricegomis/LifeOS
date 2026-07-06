<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'
import Tag from 'primevue/tag'
import { componentsByType, dishesForMealType } from '@/data/localLibrary'
import { useWeekPlannerStore } from '@/stores/weekPlanner'
import type { ComponentType, MealComponent, MealSlot } from '@/types'

const props = defineProps<{
  visible: boolean
  dayId: string | null
  mealSlot: MealSlot | null
  title: string
  position: 'left' | 'right' | 'top' | 'bottom'
  drawerStyle: Record<string, string | undefined>
}>()

const emit = defineEmits<{
  'update:visible': [visible: boolean]
}>()

const plannerStore = useWeekPlannerStore()

const selectedComponent = ref<{ componentIndex: number; componentType: ComponentType } | null>(null)
const showDishChoices = ref(false)

const drawerVisible = computed({
  get: () => props.visible,
  set: (visible: boolean) => {
    emit('update:visible', visible)

    if (!visible) {
      resetChoices()
    }
  },
})

const compatibleComponents = computed<MealComponent[]>(() => {
  if (!selectedComponent.value) {
    return []
  }

  return componentsByType(selectedComponent.value.componentType)
})

const availableDishes = computed(() => {
  if (!props.mealSlot) {
    return []
  }

  return dishesForMealType(props.mealSlot.mealType)
})

const mealSummary = computed(() => {
  if (!props.mealSlot) {
    return ''
  }

  if (props.mealSlot.mealDefinition.kind === 'composite') {
    return props.mealSlot.mealDefinition.name
  }

  return props.mealSlot.mealDefinition.components.map((component) => component.name).join(' + ')
})

function resetChoices(): void {
  selectedComponent.value = null
  showDishChoices.value = false
}

function openComponentChoices(componentIndex: number, componentType: ComponentType): void {
  selectedComponent.value = { componentIndex, componentType }
  showDishChoices.value = false
}

function replaceComponent(componentId: string): void {
  if (!props.dayId || !props.mealSlot || !selectedComponent.value) {
    return
  }

  plannerStore.replaceComponent(
    props.dayId,
    props.mealSlot.mealType,
    selectedComponent.value.componentIndex,
    selectedComponent.value.componentType,
    componentId,
  )
  selectedComponent.value = null
}

function replaceDish(dishId: string): void {
  if (!props.dayId || !props.mealSlot) {
    return
  }

  plannerStore.replaceDish(props.dayId, props.mealSlot.mealType, dishId)
  showDishChoices.value = false
}

watch(
  () => [props.dayId, props.mealSlot?.id],
  () => {
    resetChoices()
  },
)
</script>

<template>
  <Drawer
    v-model:visible="drawerVisible"
    class="planner-editor-drawer"
    :position="position"
    :style="drawerStyle"
    modal
    block-scroll
    :dismissable="true"
    :header="title"
  >
    <template #header>
      <div class="editor-heading">
        <p class="eyebrow">Édition repas</p>
        <h2>{{ title }}</h2>
      </div>
    </template>

    <template v-if="mealSlot">
      <div class="editor-content">
        <p class="editor-summary">{{ mealSummary }}</p>

        <template v-if="mealSlot.mealDefinition.kind === 'assembled'">
          <button
            v-for="(component, componentIndex) in mealSlot.mealDefinition.components"
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
              <span aria-hidden="true">{{ mealSlot.mealDefinition.icon }}</span>
              {{ mealSlot.mealDefinition.name }}
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
      <footer v-if="mealSlot" class="editor-footer">
        <div class="editor-tags">
          <Tag
            :value="`${mealSlot.estimatedCalories} kcal · ${mealSlot.estimatedProteinGrams} g prot.`"
            severity="secondary"
            rounded
          />
          <Tag
            :value="`${mealSlot.preparationTimeMinutes} min`"
            icon="pi pi-clock"
            severity="info"
            rounded
          />
        </div>
        <Button class="editor-validate" label="Valider" icon="pi pi-check" @click="drawerVisible = false" />
      </footer>
    </template>
  </Drawer>
</template>
