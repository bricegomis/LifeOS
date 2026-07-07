import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import type { FrequencyRule, PlanningRule } from '@/types'

interface PlanningRulesState {
  planningRules: PlanningRule[]
  frequencyRules: FrequencyRule[]
}

const STORAGE_KEY = 'lifeos.planningRules.v1'

const defaultPlanningRules: PlanningRule[] = [
  {
    id: 'fixed-tuesday-lunch-sardines',
    weekday: 'tuesday',
    mealType: 'lunch',
    target: { kind: 'component', componentId: 'sardines', componentType: 'protein' },
  },
  {
    id: 'fixed-wednesday-lunch-liver',
    weekday: 'wednesday',
    mealType: 'lunch',
    target: { kind: 'component', componentId: 'liver', componentType: 'protein' },
  },
  {
    id: 'fixed-thursday-lunch-sardines',
    weekday: 'thursday',
    mealType: 'lunch',
    target: { kind: 'component', componentId: 'sardines', componentType: 'protein' },
  },
  {
    id: 'fixed-saturday-dinner-chicken',
    weekday: 'saturday',
    mealType: 'dinner',
    target: { kind: 'component', componentId: 'chicken', componentType: 'protein' },
  },
  {
    id: 'fixed-sunday-dinner-pizza',
    weekday: 'sunday',
    mealType: 'dinner',
    target: { kind: 'dish', dishId: 'homemade-pizza' },
  },
]

const defaultFrequencyRules: FrequencyRule[] = [
  {
    id: 'frequency-sardines',
    target: { kind: 'component', componentId: 'sardines' },
    targetCountPerWeek: 3,
  },
  {
    id: 'frequency-tempeh',
    target: { kind: 'component', componentId: 'tempeh' },
    targetCountPerWeek: 1,
  },
  {
    id: 'frequency-pleasure-meal',
    target: { kind: 'category', categoryId: 'pleasure-meal', label: 'Repas plaisir' },
    targetCountPerWeek: 2,
  },
]

function cloneDefaultState(): PlanningRulesState {
  return {
    planningRules: defaultPlanningRules.map((rule) => ({ ...rule, target: { ...rule.target } })),
    frequencyRules: defaultFrequencyRules.map((rule) => ({ ...rule, target: { ...rule.target } })),
  }
}

function loadState(): PlanningRulesState {
  if (typeof window === 'undefined') {
    return cloneDefaultState()
  }

  try {
    const rawState = window.localStorage.getItem(STORAGE_KEY)

    if (!rawState) {
      return cloneDefaultState()
    }

    const parsedState = JSON.parse(rawState) as Partial<PlanningRulesState>

    return {
      planningRules: parsedState.planningRules ?? cloneDefaultState().planningRules,
      frequencyRules: parsedState.frequencyRules ?? cloneDefaultState().frequencyRules,
    }
  } catch {
    return cloneDefaultState()
  }
}

function persistState(state: PlanningRulesState): void {
  if (typeof window === 'undefined') {
    return
  }

  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

function createRuleId(prefix: string): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return `${prefix}-${crypto.randomUUID()}`
  }

  return `${prefix}-${Date.now()}`
}

function normalizedFrequency(value: number): number {
  return Math.max(0, Math.round(value))
}

export const usePlanningRulesStore = defineStore('planningRules', () => {
  const initialState = loadState()
  const planningRules = ref<PlanningRule[]>(initialState.planningRules)
  const frequencyRules = ref<FrequencyRule[]>(initialState.frequencyRules)

  watch(
    [planningRules, frequencyRules],
    () => {
      persistState({
        planningRules: planningRules.value,
        frequencyRules: frequencyRules.value,
      })
    },
    { deep: true },
  )

  function addPlanningRule(rule: Omit<PlanningRule, 'id'>): void {
    planningRules.value = [
      ...planningRules.value,
      {
        ...rule,
        id: createRuleId('fixed-rule'),
        target: { ...rule.target },
      },
    ]
  }

  function updatePlanningRule(id: string, rule: Omit<PlanningRule, 'id'>): void {
    planningRules.value = planningRules.value.map((item) =>
      item.id === id
        ? {
            ...rule,
            id,
            target: { ...rule.target },
          }
        : item,
    )
  }

  function deletePlanningRule(id: string): void {
    planningRules.value = planningRules.value.filter((rule) => rule.id !== id)
  }

  function updateFrequencyRule(id: string, targetCountPerWeek: number): void {
    frequencyRules.value = frequencyRules.value.map((rule) =>
      rule.id === id
        ? {
            ...rule,
            targetCountPerWeek: normalizedFrequency(targetCountPerWeek),
          }
        : rule,
    )
  }

  return {
    planningRules,
    frequencyRules,
    addPlanningRule,
    updatePlanningRule,
    deletePlanningRule,
    updateFrequencyRule,
  }
})
