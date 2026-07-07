<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { compositeDishes, mealComponents } from '@/data/localLibrary'
import { usePlanningRulesStore } from '@/stores/planningRules'
import {
  addWeeksToDateString,
  getWeekMode,
  getWeekModeOverride,
  getWeekRangeLabel,
  useWeekContextStore,
  weekdayLabels,
  weekdays,
  weekModeLabels,
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
  WeekModeOverride,
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

const overrideDialogVisible = ref(false)
const editingOverrideWeekStartDate = ref<string | null>(null)
const overrideForm = reactive<{
  weekStartDate: string
  mode: WeekMode
}>({
  weekStartDate: addWeeksToDateString(
    weekContextStore.weekContext.alternatingWeekConfig.referenceWeekStartDate,
    1,
  ),
  mode: 'kids',
})

const alternatingWeekConfig = computed(() => weekContextStore.weekContext.alternatingWeekConfig)

const sortedWeekModeOverrides = computed(() =>
  [...weekContextStore.weekContext.weekModeOverrides].sort((left, right) =>
    left.weekStartDate.localeCompare(right.weekStartDate),
  ),
)

const alternatingWeekPreview = computed(() => {
  const config = alternatingWeekConfig.value

  return Array.from({ length: 4 }, (_, index) => {
    const weekStartDate = addWeeksToDateString(config.referenceWeekStartDate, index)
    const mode = getWeekMode(weekStartDate, config, weekContextStore.weekContext.weekModeOverrides)
    const override = getWeekModeOverride(weekStartDate, weekContextStore.weekContext.weekModeOverrides)

    return {
      weekStartDate,
      label: getWeekRangeLabel(weekStartDate),
      modeLabel: weekModeLabels[mode],
      overrideLabel: override ? `Exception : ${weekModeLabels[override.mode]}` : '',
    }
  })
})

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

function updateReferenceWeekStartDate(value: string): void {
  weekContextStore.setReferenceWeekStartDate(value)
}

function updateReferenceWeekStartDateFromEvent(event: Event): void {
  const target = event.target as HTMLInputElement | null

  if (target) {
    updateReferenceWeekStartDate(target.value)
  }
}

function updateReferenceWeekMode(value: WeekMode): void {
  weekContextStore.setReferenceWeekMode(value)
}

function openNewOverrideDialog(): void {
  editingOverrideWeekStartDate.value = null
  overrideForm.weekStartDate = addWeeksToDateString(
    weekContextStore.weekContext.alternatingWeekConfig.referenceWeekStartDate,
    1,
  )
  overrideForm.mode = 'kids'
  overrideDialogVisible.value = true
}

function openEditOverrideDialog(override: WeekModeOverride): void {
  editingOverrideWeekStartDate.value = override.weekStartDate
  overrideForm.weekStartDate = override.weekStartDate
  overrideForm.mode = override.mode
  overrideDialogVisible.value = true
}

function saveWeekModeOverride(): void {
  weekContextStore.upsertWeekModeOverride(
    {
      weekStartDate: overrideForm.weekStartDate,
      mode: overrideForm.mode,
    },
    editingOverrideWeekStartDate.value,
  )
  overrideDialogVisible.value = false
}

function deleteWeekModeOverride(weekStartDate: string): void {
  weekContextStore.deleteWeekModeOverride(weekStartDate)
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

    <section class="settings-section" aria-labelledby="alternating-week-title">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Alternance par défaut</p>
          <h2 id="alternating-week-title">Semaine de référence</h2>
        </div>

        <Button label="Ajouter une exception" icon="pi pi-plus" size="small" @click="openNewOverrideDialog" />
      </div>

      <div class="alternating-grid">
        <label class="form-field">
          <span>Semaine de référence</span>
          <input
            class="text-input"
            type="date"
            :value="alternatingWeekConfig.referenceWeekStartDate"
            @change="updateReferenceWeekStartDateFromEvent"
          />
        </label>

        <label class="form-field">
          <span>Mode de référence</span>
          <Select
            :model-value="alternatingWeekConfig.referenceWeekMode"
            :options="weekModeOptions"
            option-label="label"
            option-value="value"
            @update:model-value="updateReferenceWeekMode($event)"
          />
        </label>
      </div>

      <div class="alternating-preview" aria-label="Prévisualisation de l'alternance">
        <article v-for="week in alternatingWeekPreview" :key="week.weekStartDate" class="alternating-preview-row">
          <div>
            <strong>{{ week.label }}</strong>
            <span>{{ week.modeLabel }}</span>
          </div>
          <small v-if="week.overrideLabel">{{ week.overrideLabel }}</small>
        </article>
      </div>
    </section>

    <section class="settings-section" aria-labelledby="exceptions-title">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Exceptions</p>
          <h2 id="exceptions-title">Surcharges ponctuelles</h2>
        </div>

        <Button label="Ajouter" icon="pi pi-plus" size="small" @click="openNewOverrideDialog" />
      </div>

      <div v-if="sortedWeekModeOverrides.length" class="override-list">
        <article v-for="override in sortedWeekModeOverrides" :key="override.weekStartDate" class="override-row">
          <div class="rule-main">
            <strong>Semaine du {{ getWeekRangeLabel(override.weekStartDate) }}</strong>
            <span>{{ weekModeLabels[override.mode] }}</span>
          </div>

          <div class="rule-actions">
            <Button
              icon="pi pi-pencil"
              aria-label="Modifier l'exception"
              text
              rounded
              @click="openEditOverrideDialog(override)"
            />
            <Button
              icon="pi pi-trash"
              aria-label="Supprimer l'exception"
              severity="danger"
              text
              rounded
              @click="deleteWeekModeOverride(override.weekStartDate)"
            />
          </div>
        </article>
      </div>

      <p v-else class="empty-state">Aucune exception configurée.</p>
    </section>

    <section class="settings-section" aria-labelledby="weekly-context-title">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Contexte semaine</p>
          <h2 id="weekly-context-title">Organisation réelle</h2>
        </div>
      </div>

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
      v-model:visible="overrideDialogVisible"
      :header="editingOverrideWeekStartDate ? 'Modifier une exception' : 'Ajouter une exception'"
      modal
      class="rule-dialog"
      :style="{ width: 'min(92vw, 32rem)' }"
    >
      <form class="rule-form" @submit.prevent="saveWeekModeOverride">
        <label class="form-field">
          <span>Semaine</span>
          <input class="text-input" type="date" v-model="overrideForm.weekStartDate" />
        </label>

        <label class="form-field">
          <span>Mode</span>
          <Select
            v-model="overrideForm.mode"
            :options="weekModeOptions"
            option-label="label"
            option-value="value"
          />
        </label>

        <div class="dialog-actions">
          <Button label="Annuler" severity="secondary" text @click="overrideDialogVisible = false" />
          <Button label="Enregistrer" type="submit" />
        </div>
      </form>
    </Dialog>

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

.alternating-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

.alternating-preview,
.override-list {
  display: grid;
  gap: 0.5rem;
}

.alternating-preview-row,
.override-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.85rem 0;
  border-top: 1px solid var(--surface-border);
}

.alternating-preview-row strong,
.override-row strong {
  display: block;
}

.alternating-preview-row span,
.override-row span {
  color: var(--text-color-secondary);
}

.alternating-preview-row small {
  color: var(--text-color-secondary);
  font-size: 0.82rem;
  white-space: nowrap;
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

.text-input {
  width: 100%;
  min-height: 2.6rem;
  padding: 0.65rem 0.8rem;
  border: 1px solid var(--surface-border);
  border-radius: 6px;
  background: var(--surface-ground);
  color: var(--text-color);
  font: inherit;
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

  .alternating-grid {
    grid-template-columns: 1fr;
  }

  .rule-actions,
  .frequency-control {
    justify-content: flex-start;
  }

  .alternating-preview-row,
  .override-row {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
