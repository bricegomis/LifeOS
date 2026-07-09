import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import { compositeDishes, mealComponents } from '@/data/localLibrary'
import { weekdays } from '@/stores/weekContext'
import { getCurrentUserId } from '@/services/supabase/auth'
import { replacePlanningRules } from '@/services/supabase/lifeosRepository'
import type {
  ComponentType,
  FrequencyRule,
  FrequencyRuleTarget,
  MealType,
  PlanningRule,
  PlanningRuleTarget,
  Weekday,
} from '@/types'

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

const activeComponentIds = new Set(mealComponents.filter((component) => component.active).map((component) => component.id))
const activeDishIds = new Set(compositeDishes.filter((dish) => dish.active).map((dish) => dish.id))

function cloneDefaultState(): PlanningRulesState {
  return {
    planningRules: defaultPlanningRules.map((rule) => ({ ...rule, target: { ...rule.target } })),
    frequencyRules: defaultFrequencyRules.map((rule) => ({ ...rule, target: { ...rule.target } })),
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isWeekday(value: unknown): value is Weekday {
  return typeof value === 'string' && (weekdays as readonly string[]).includes(value)
}

function isMealType(value: unknown): value is MealType {
  return value === 'breakfast' || value === 'lunch' || value === 'dinner'
}

function isPlanningRuleTarget(value: unknown): value is PlanningRuleTarget {
  if (!isRecord(value) || typeof value.kind !== 'string') {
    return false
  }

  if (value.kind === 'component') {
    return (
      typeof value.componentId === 'string' &&
      typeof value.componentType === 'string' &&
      ['protein', 'starch', 'vegetable', 'optional'].includes(value.componentType)
    )
  }

  return value.kind === 'dish' && typeof value.dishId === 'string'
}

function isFrequencyRuleTarget(value: unknown): value is FrequencyRuleTarget {
  if (!isRecord(value) || typeof value.kind !== 'string') {
    return false
  }

  if (value.kind === 'component') {
    return typeof value.componentId === 'string'
  }

  if (value.kind === 'dish') {
    return typeof value.dishId === 'string'
  }

  return (
    value.kind === 'category' &&
    typeof value.categoryId === 'string' &&
    typeof value.label === 'string'
  )
}

function normalizePlanningRule(value: unknown): PlanningRule | null {
  if (!isRecord(value) || typeof value.id !== 'string') {
    return null
  }

  if (!isWeekday(value.weekday) || !isMealType(value.mealType) || !isPlanningRuleTarget(value.target)) {
    return null
  }

  if (value.target.kind === 'component') {
    return activeComponentIds.has(value.target.componentId)
      ? {
          id: value.id,
          weekday: value.weekday,
          mealType: value.mealType,
          target: {
            kind: 'component',
            componentId: value.target.componentId,
            componentType: value.target.componentType as ComponentType,
          },
        }
      : null
  }

  return activeDishIds.has(value.target.dishId)
    ? {
        id: value.id,
        weekday: value.weekday,
        mealType: value.mealType,
        target: {
          kind: 'dish',
          dishId: value.target.dishId,
        },
      }
    : null
}

function normalizeFrequencyRule(value: unknown): FrequencyRule | null {
  if (!isRecord(value) || typeof value.id !== 'string' || !isFrequencyRuleTarget(value.target)) {
    return null
  }

  if (value.target.kind === 'component' && !activeComponentIds.has(value.target.componentId)) {
    return null
  }

  if (value.target.kind === 'dish' && !activeDishIds.has(value.target.dishId)) {
    return null
  }

  if (value.target.kind === 'category' && value.target.categoryId !== 'pleasure-meal') {
    return null
  }

  return {
    id: value.id,
    target: {
      ...value.target,
    },
    targetCountPerWeek:
      typeof value.targetCountPerWeek === 'number'
        ? normalizedFrequency(value.targetCountPerWeek)
        : 0,
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
    const defaultState = cloneDefaultState()
    const planningRules = Array.isArray(parsedState.planningRules)
      ? parsedState.planningRules.map(normalizePlanningRule).filter((rule): rule is PlanningRule => Boolean(rule))
      : []
    const frequencyRules = Array.isArray(parsedState.frequencyRules)
      ? parsedState.frequencyRules.map(normalizeFrequencyRule).filter((rule): rule is FrequencyRule => Boolean(rule))
      : []

    return {
      planningRules: planningRules.length ? planningRules : defaultState.planningRules,
      frequencyRules: frequencyRules.length ? frequencyRules : defaultState.frequencyRules,
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

      const userId = getCurrentUserId()

      if (userId) {
        void replacePlanningRules(userId, {
          planningRules: planningRules.value,
          frequencyRules: frequencyRules.value,
        })
      }
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
