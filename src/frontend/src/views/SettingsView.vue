<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { compositeDishes, mealComponents } from '@/data/localLibrary'
import { usePlanningRulesStore } from '@/stores/planningRules'
import {
  useWeekContextStore,
  weekdayLabels,
  weekdays,
  workLocationLabels,
} from '@/stores/weekContext'
import type {
  ComponentType,
  FrequencyRule,
  FrequencyRuleTarget,
  MealType,
  PlanningRule,
  PlanningRuleTarget,
  WeekMode,
  Weekday,
  WorkLocation,
} from '@/types'

interface SelectOption<T extends string = string> {
  label: string
  value: T
}

interface TargetOption {
  label: string
  value: string
  description: string
  target: PlanningRuleTarget | FrequencyRuleTarget
}

const planningRulesStore = usePlanningRulesStore()
const weekContextStore = useWeekContextStore()

const weekdayOptions: SelectOption<Weekday>[] = [
  { label: 'Lundi', value: 'monday' },
  { label: 'Mardi', value: 'tuesday' },
  { label: 'Mercredi', value: 'wednesday' },
  { label: 'Jeudi', value: 'thursday' },
  { label: 'Vendredi', value: 'friday' },
  { label: 'Samedi', value: 'saturday' },
  { label: 'Dimanche', value: 'sunday' },
]

const mealTypeOptions: SelectOption<MealType>[] = [
  { label: 'Petit-déjeuner', value: 'breakfast' },
  { label: 'Déjeuner', value: 'lunch' },
  { label: 'Dîner', value: 'dinner' },
]

const weekModeOptions: SelectOption<WeekMode>[] = [
  { label: 'Avec enfants', value: 'kids' },
  { label: 'Solo', value: 'solo' },
]

const workLocationOptions: SelectOption<WorkLocation>[] = [
  { label: 'Télétravail', value: 'home' },
  { label: 'Bureau', value: 'office' },
  { label: 'Off', value: 'off' },
]

const bikeCommuteOptions = [
  { label: 'Sans vélo', value: false },
  { label: 'Vélo', value: true },
]

const componentTypeLabels: Record<ComponentType, string> = {
  protein: 'Protéine principale',
  starch: 'Féculent',
  vegetable: 'Légume',
  optional: 'Complément',
}

const categoryTargets: TargetOption[] = [
  {
    label: 'Repas plaisir',
    value: 'category:pleasure-meal',
    description: 'Catégorie simple',
    target: { kind: 'category', categoryId: 'pleasure-meal', label: 'Repas plaisir' },
  },
]

const ruleDialogVisible = ref(false)
const editingRuleId = ref<string | null>(null)
const ruleForm = reactive<{
  weekday: Weekday
  mealType: MealType
  targetKey: string
}>({
  weekday: 'monday',
  mealType: 'lunch',
  targetKey: '',
})

const componentTargetOptions = computed<TargetOption[]>(() =>
  mealComponents
    .filter((component) => component.active)
    .map((component) => ({
      label: component.name,
      value: `component:${component.id}`,
      description: componentTypeLabels[component.componentType],
      target: {
        kind: 'component',
        componentId: component.id,
        componentType: component.componentType,
      },
    })),
)

const dishTargetOptions = computed<TargetOption[]>(() =>
  compositeDishes
    .filter((dish) => dish.active)
    .map((dish) => ({
      label: dish.name,
      value: `dish:${dish.id}`,
      description: 'Repas complet',
      target: {
        kind: 'dish',
        dishId: dish.id,
      },
    })),
)

const planningTargetOptions = computed<TargetOption[]>(() => [
  ...componentTargetOptions.value,
  ...dishTargetOptions.value,
])

const frequencyTargetOptions = computed<TargetOption[]>(() => [
  ...componentTargetOptions.value,
  ...dishTargetOptions.value,
  ...categoryTargets,
])

const ruleDialogTitle = computed(() =>
  editingRuleId.value ? 'Modifier une règle fixe' : 'Ajouter une règle fixe',
)

function weekdayLabel(weekday: Weekday): string {
  return weekdayOptions.find((option) => option.value === weekday)?.label ?? weekday
}

function mealTypeLabel(mealType: MealType): string {
  return mealTypeOptions.find((option) => option.value === mealType)?.label ?? mealType
}

function targetKey(target: PlanningRuleTarget | FrequencyRuleTarget): string {
  if (target.kind === 'component') {
    return `component:${target.componentId}`
  }

  if (target.kind === 'dish') {
    return `dish:${target.dishId}`
  }

  return `category:${target.categoryId}`
}

function targetOption(target: PlanningRuleTarget | FrequencyRuleTarget): TargetOption | null {
  return frequencyTargetOptions.value.find((option) => option.value === targetKey(target)) ?? null
}

function targetLabel(target: PlanningRuleTarget | FrequencyRuleTarget): string {
  return targetOption(target)?.label ?? 'Cible inconnue'
}

function targetDescription(target: PlanningRuleTarget | FrequencyRuleTarget): string {
  return targetOption(target)?.description ?? ''
}

function clonePlanningTarget(target: PlanningRuleTarget | FrequencyRuleTarget): PlanningRuleTarget {
  if (target.kind === 'component') {
    return {
      kind: 'component',
      componentId: target.componentId,
      componentType: 'componentType' in target ? target.componentType : 'protein',
    }
  }

  if (target.kind === 'dish') {
    return { kind: 'dish', dishId: target.dishId }
  }

  throw new Error('Une règle fixe doit cibler un composant ou un plat.')
}

function openNewRuleDialog(): void {
  editingRuleId.value = null
  ruleForm.weekday = 'monday'
  ruleForm.mealType = 'lunch'
  ruleForm.targetKey = planningTargetOptions.value[0]?.value ?? ''
  ruleDialogVisible.value = true
}

function openEditRuleDialog(rule: PlanningRule): void {
  editingRuleId.value = rule.id
  ruleForm.weekday = rule.weekday
  ruleForm.mealType = rule.mealType
  ruleForm.targetKey = targetKey(rule.target)
  ruleDialogVisible.value = true
}

function savePlanningRule(): void {
  const selectedTarget = planningTargetOptions.value.find(
    (option) => option.value === ruleForm.targetKey,
  )

  if (!selectedTarget) {
    return
  }

  const rule = {
    weekday: ruleForm.weekday,
    mealType: ruleForm.mealType,
    target: clonePlanningTarget(selectedTarget.target),
  }

  if (editingRuleId.value) {
    planningRulesStore.updatePlanningRule(editingRuleId.value, rule)
  } else {
    planningRulesStore.addPlanningRule(rule)
  }

  ruleDialogVisible.value = false
}

function updateFrequency(rule: FrequencyRule, value: number | null): void {
  planningRulesStore.updateFrequencyRule(rule.id, value ?? 0)
}

function updateWeekMode(value: WeekMode): void {
  weekContextStore.updateWeekMode(value)
}

function updateWorkLocation(weekday: Weekday, value: WorkLocation): void {
  weekContextStore.updateWorkLocation(weekday, value)
}

function updateBikeCommute(weekday: Weekday, value: boolean): void {
  weekContextStore.updateBikeCommute(weekday, value)
}
</script>

<template>
  <section class="page-stack settings-page">
    <header class="page-hero settings-hero">
      <p class="eyebrow">Réglages</p>
      <h1>Règles de planification alimentaire</h1>
      <p>Configuration locale simple pour préparer le futur générateur de semaine.</p>
    </header>

    <section class="settings-section" aria-labelledby="weekly-context-title">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Contexte semaine</p>
          <h2 id="weekly-context-title">Organisation réelle</h2>
        </div>
      </div>

      <label class="form-field week-mode-field">
        <span>Mode semaine</span>
        <Select
          :model-value="weekContextStore.weekContext.weekMode"
          :options="weekModeOptions"
          option-label="label"
          option-value="value"
          @update:model-value="updateWeekMode($event)"
        />
      </label>

      <div class="context-grid" aria-label="Organisation des journées">
        <article v-for="weekday in weekdays" :key="weekday" class="context-row">
          <strong>{{ weekdayLabels[weekday] }}</strong>

          <div class="context-controls">
            <Select
              :model-value="weekContextStore.weekContext.days[weekday].workLocation"
              :options="workLocationOptions"
              option-label="label"
              option-value="value"
              :aria-label="`Lieu de travail ${weekdayLabels[weekday]}`"
              @update:model-value="updateWorkLocation(weekday, $event)"
            />

            <Select
              :model-value="weekContextStore.weekContext.days[weekday].bikeCommute"
              :options="bikeCommuteOptions"
              option-label="label"
              option-value="value"
              :disabled="weekContextStore.weekContext.days[weekday].workLocation !== 'office'"
              :aria-label="`Trajet vélo ${weekdayLabels[weekday]}`"
              @update:model-value="updateBikeCommute(weekday, $event)"
            />
          </div>

          <small>
            {{ workLocationLabels[weekContextStore.weekContext.days[weekday].workLocation] }}
            <template v-if="weekContextStore.weekContext.days[weekday].bikeCommute">
              · Vélo
            </template>
          </small>
        </article>
      </div>
    </section>

    <section class="settings-section" aria-labelledby="fixed-planning-title">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Planning fixe</p>
          <h2 id="fixed-planning-title">Repas imposés</h2>
        </div>

        <Button label="Ajouter" icon="pi pi-plus" size="small" @click="openNewRuleDialog" />
      </div>

      <div v-if="planningRulesStore.planningRules.length" class="rule-list">
        <article v-for="rule in planningRulesStore.planningRules" :key="rule.id" class="rule-row">
          <div class="rule-main">
            <strong>
              {{ weekdayLabel(rule.weekday) }} · {{ mealTypeLabel(rule.mealType) }} ·
              {{ targetLabel(rule.target) }}
            </strong>
            <span>{{ targetDescription(rule.target) }}</span>
          </div>

          <div class="rule-actions">
            <Button
              icon="pi pi-pencil"
              aria-label="Modifier la règle"
              text
              rounded
              @click="openEditRuleDialog(rule)"
            />
            <Button
              icon="pi pi-trash"
              aria-label="Supprimer la règle"
              severity="danger"
              text
              rounded
              @click="planningRulesStore.deletePlanningRule(rule.id)"
            />
          </div>
        </article>
      </div>

      <p v-else class="empty-state">Aucune règle fixe configurée.</p>
    </section>

    <section class="settings-section" aria-labelledby="frequency-title">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Fréquences hebdomadaires</p>
          <h2 id="frequency-title">Objectifs simples</h2>
        </div>
      </div>

      <div class="frequency-list">
        <article
          v-for="rule in planningRulesStore.frequencyRules"
          :key="rule.id"
          class="frequency-row"
        >
          <div class="frequency-main">
            <strong>{{ targetLabel(rule.target) }}</strong>
            <span>{{ targetDescription(rule.target) }}</span>
          </div>

          <div class="frequency-control">
            <InputNumber
              :model-value="rule.targetCountPerWeek"
              input-id="frequency-count"
              :min="0"
              :max="14"
              show-buttons
              button-layout="horizontal"
              decrement-button-class="frequency-button"
              increment-button-class="frequency-button"
              increment-button-icon="pi pi-plus"
              decrement-button-icon="pi pi-minus"
              @update:model-value="updateFrequency(rule, $event)"
            />
            <span>/ semaine</span>
          </div>
        </article>
      </div>
    </section>

    <Dialog
      v-model:visible="ruleDialogVisible"
      :header="ruleDialogTitle"
      modal
      class="rule-dialog"
      :style="{ width: 'min(92vw, 34rem)' }"
    >
      <form class="rule-form" @submit.prevent="savePlanningRule">
        <label class="form-field">
          <span>Jour</span>
          <Select
            v-model="ruleForm.weekday"
            :options="weekdayOptions"
            option-label="label"
            option-value="value"
          />
        </label>

        <label class="form-field">
          <span>Créneau</span>
          <Select
            v-model="ruleForm.mealType"
            :options="mealTypeOptions"
            option-label="label"
            option-value="value"
          />
        </label>

        <label class="form-field">
          <span>Repas ou composant imposé</span>
          <Select
            v-model="ruleForm.targetKey"
            :options="planningTargetOptions"
            option-label="label"
            option-value="value"
          >
            <template #option="{ option }">
              <div class="select-option">
                <strong>{{ option.label }}</strong>
                <span>{{ option.description }}</span>
              </div>
            </template>
          </Select>
        </label>

        <div class="dialog-actions">
          <Button label="Annuler" severity="secondary" text @click="ruleDialogVisible = false" />
          <Button label="Enregistrer" type="submit" />
        </div>
      </form>
    </Dialog>
  </section>
</template>

<style scoped>
.settings-page {
  gap: 1.25rem;
}

.settings-hero {
  max-width: 46rem;
}

.settings-section {
  display: grid;
  gap: 1rem;
  padding: 1.1rem;
  border: 1px solid var(--surface-border);
  border-radius: 8px;
  background: var(--surface-card);
}

.section-heading,
.rule-row,
.frequency-row,
.frequency-control,
.rule-actions {
  display: flex;
  align-items: center;
}

.section-heading,
.rule-row,
.frequency-row {
  justify-content: space-between;
  gap: 1rem;
}

.section-heading h2 {
  margin: 0.1rem 0 0;
  font-size: 1.2rem;
}

.rule-list,
.frequency-list,
.context-grid,
.rule-form {
  display: grid;
  gap: 0.75rem;
}

.rule-row,
.frequency-row,
.context-row {
  min-height: 4.25rem;
  padding: 0.85rem 0;
  border-top: 1px solid var(--surface-border);
}

.rule-main,
.frequency-main,
.form-field,
.select-option {
  display: grid;
  gap: 0.25rem;
}

.rule-main span,
.frequency-main span,
.form-field span,
.select-option span,
.empty-state,
.context-row small {
  color: var(--text-color-secondary);
}

.week-mode-field {
  max-width: 18rem;
}

.context-row {
  display: grid;
  grid-template-columns: minmax(7rem, 0.7fr) minmax(18rem, 1.5fr) minmax(8rem, 0.8fr);
  align-items: center;
  gap: 0.75rem;
}

.context-controls {
  display: grid;
  grid-template-columns: minmax(9rem, 1fr) minmax(8rem, 0.8fr);
  gap: 0.5rem;
}

.rule-actions {
  gap: 0.25rem;
  flex-shrink: 0;
}

.frequency-control {
  gap: 0.55rem;
  white-space: nowrap;
}

.form-field {
  font-weight: 600;
}

.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding-top: 0.5rem;
}

@media (max-width: 640px) {
  .section-heading,
  .rule-row,
  .frequency-row,
  .context-row {
    align-items: stretch;
    flex-direction: column;
  }

  .context-row,
  .context-controls {
    grid-template-columns: 1fr;
  }

  .rule-actions,
  .frequency-control {
    justify-content: flex-start;
  }
}
</style>
