import { computed, ref, watch } from 'vue'
import { defineStore } from 'pinia'
import { usePlanningRulesStore } from '@/stores/planningRules'
import { useWeekContextStore } from '@/stores/weekContext'
import { useWeekPlannerStore } from '@/stores/weekPlanner'
import {
  initializeSupabaseAuth,
  isAuthenticated,
  isSupabaseAuthAvailable,
  signInWithMagicLink,
  signOutSupabase,
  supabaseSession,
} from '@/services/supabase/auth'
import {
  loadPlanningRules,
  loadUserSettings,
  loadWeekContextConfig,
  loadWeekModeOverrides,
  loadWeekPlan,
  listWeekPlans,
  replacePlanningRules,
  replaceWeekModeOverrides,
  saveUserSettings,
  saveWeekContextConfig,
  upsertWeekPlan,
} from '@/services/supabase/lifeosRepository'

let bootstrapUserId: string | null = null
let initializePromise: Promise<void> | null = null

async function bootstrapRemoteState(userId: string): Promise<void> {
  const planningRulesStore = usePlanningRulesStore()
  const weekContextStore = useWeekContextStore()
  const weekPlannerStore = useWeekPlannerStore()

  const [remoteUserSettings, remoteWeekContextConfig, remoteWeekModeOverrides, remotePlanningRules, remoteWeekPlan] =
    await Promise.all([
      loadUserSettings(userId),
      loadWeekContextConfig(userId),
      loadWeekModeOverrides(userId),
      loadPlanningRules(userId),
      loadWeekPlan(userId, weekPlannerStore.weekPlan.startDate),
    ])

  if (remoteUserSettings) {
    weekContextStore.weekContext.days = remoteUserSettings.weekContextDays
  } else {
    await saveUserSettings(userId, {
      weekContextDays: weekContextStore.weekContext.days,
    })
  }

  if (remoteWeekContextConfig) {
    weekContextStore.weekContext.alternatingWeekConfig = remoteWeekContextConfig
  } else {
    await saveWeekContextConfig(userId, weekContextStore.weekContext.alternatingWeekConfig)
  }

  if (remoteWeekModeOverrides) {
    weekContextStore.weekContext.weekModeOverrides = remoteWeekModeOverrides
  } else {
    await replaceWeekModeOverrides(userId, weekContextStore.weekContext.weekModeOverrides)
  }

  if (remotePlanningRules) {
    planningRulesStore.planningRules = remotePlanningRules.planningRules
    planningRulesStore.frequencyRules = remotePlanningRules.frequencyRules
  } else {
    await replacePlanningRules(userId, {
      planningRules: planningRulesStore.planningRules,
      frequencyRules: planningRulesStore.frequencyRules,
    })
  }

  if (remoteWeekPlan) {
    weekPlannerStore.weekPlan = remoteWeekPlan
  } else {
    const remoteWeekPlans = await listWeekPlans(userId)
    const latestRemoteWeekPlan = remoteWeekPlans[0]

    if (latestRemoteWeekPlan) {
      weekPlannerStore.weekPlan = latestRemoteWeekPlan
    } else {
      await upsertWeekPlan(userId, weekPlannerStore.weekPlan)
    }
  }
}

export const useAuthStore = defineStore('auth', () => {
  const ready = ref(false)
  const loading = ref(false)
  const errorMessage = ref<string | null>(null)

  const session = computed(() => supabaseSession.value)
  const user = computed(() => session.value?.user ?? null)
  const authenticated = computed(() => isAuthenticated())
  const configured = computed(() => isSupabaseAuthAvailable())

  watch(
    supabaseSession,
    async (nextSession, previousSession) => {
      const nextUserId = nextSession?.user.id ?? null

      if (!nextUserId) {
        bootstrapUserId = null
        return
      }

      if (bootstrapUserId === nextUserId && previousSession?.user.id === nextUserId) {
        return
      }

      bootstrapUserId = nextUserId

      try {
        await bootstrapRemoteState(nextUserId)
      } catch (error) {
        errorMessage.value = error instanceof Error ? error.message : 'Impossible de synchroniser les données distantes.'
      }
    },
  )

  async function initialize(): Promise<void> {
    if (initializePromise) {
      return initializePromise
    }

    loading.value = true

    initializePromise = (async () => {
      try {
        await initializeSupabaseAuth()
        ready.value = true
      } catch (error) {
        errorMessage.value = error instanceof Error ? error.message : 'Impossible d’initialiser Supabase.'
        ready.value = true
      } finally {
        loading.value = false
      }
    })()

    return initializePromise
  }

  async function ensureReady(): Promise<void> {
    if (ready.value) {
      return
    }

    await initialize()
  }

  async function sendMagicLink(email: string, redirectPath = '/login'): Promise<void> {
    errorMessage.value = null
    const result = await signInWithMagicLink(email, redirectPath)

    if (!result.success) {
      throw new Error(result.message)
    }
  }

  async function signOut(): Promise<void> {
    errorMessage.value = null
    const result = await signOutSupabase()

    if (!result.success) {
      throw new Error(result.message)
    }
  }

  return {
    ready,
    loading,
    errorMessage,
    session,
    user,
    authenticated,
    configured,
    initialize,
    ensureReady,
    sendMagicLink,
    signOut,
  }
})
