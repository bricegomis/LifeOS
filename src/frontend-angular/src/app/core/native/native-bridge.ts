import { Capacitor, registerPlugin } from '@capacitor/core'
import type { AppGroupWidgetSnapshot, CookingSessionState } from './native.models'
import { COOKING_STORAGE_KEY, mutateCookingSession, readCookingSession } from './cooking-contract'

export function nativeBridgeStatus(): 'web' | 'available' | 'unavailable' {
  if (!Capacitor.isNativePlatform()) return 'web'
  return Capacitor.isPluginAvailable('LifeosNativeBridge') ? 'available' : 'unavailable'
}

export function requireNativeBridge(): void {
  if (nativeBridgeStatus() === 'unavailable') {
    throw new Error('Pont iOS LifeosNativeBridge non enregistré : synchronisez puis reconstruisez dans Xcode')
  }
}

export interface LifeosNativeBridgePlugin {
  saveWidgetSnapshot(options: { snapshot: AppGroupWidgetSnapshot }): Promise<void>
  clearWidgetSnapshot(): Promise<void>
  getCookingSession(): Promise<{ session: CookingSessionState | null }>
  saveCookingSession(options: { session: CookingSessionState }): Promise<void>
  mutateCookingSession(options: { sessionId: string; operation: 'toggle' | 'complete'; stepIndex?: number }): Promise<{ session: CookingSessionState }>
  clearCookingSession(options?: { sessionId?: string }): Promise<void>
  startLiveActivity(options: {
    sessionId: string
    recipeTitle: string
    steps: string[]
    completedStepIndices: number[]
  }): Promise<{ activityId?: string; disabled?: boolean }>
  updateLiveActivity(options: {
    completedStepIndices: number[]
    isCompleted: boolean
  }): Promise<void>
  endLiveActivity(): Promise<void>
  reloadWidgetTimelines(): Promise<void>
}

export const LifeosNativeBridge = registerPlugin<LifeosNativeBridgePlugin>('LifeosNativeBridge', {
  web: {
    async saveWidgetSnapshot(options: { snapshot: AppGroupWidgetSnapshot }) {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem('lifeos.native.widgetSnapshot', JSON.stringify(options.snapshot))
      }
    },
    async clearWidgetSnapshot() {
      if (typeof localStorage !== 'undefined') {
        localStorage.removeItem('lifeos.native.widgetSnapshot')
      }
    },
    async getCookingSession() {
      if (typeof localStorage !== 'undefined') {
        const raw = localStorage.getItem(COOKING_STORAGE_KEY)
        if (raw) {
          const session = readCookingSession(JSON.parse(raw))
          if (!session) localStorage.removeItem(COOKING_STORAGE_KEY)
          return { session }
        }
      }
      return { session: null }
    },
    async saveCookingSession(options: { session: CookingSessionState }) {
      const session = readCookingSession(options.session)
      if (!session) throw new Error('Session cuisine expirée')
      if (typeof localStorage !== 'undefined') {
        const raw = localStorage.getItem(COOKING_STORAGE_KEY)
        const current = raw ? readCookingSession(JSON.parse(raw)) : null
        if (current && !current.isCompleted) throw new Error('Une session cuisine est déjà active')
        localStorage.setItem(COOKING_STORAGE_KEY, JSON.stringify(session))
      }
    },
    async mutateCookingSession(options: { sessionId: string; operation: 'toggle' | 'complete'; stepIndex?: number }) {
      const raw = typeof localStorage === 'undefined' ? null : localStorage.getItem(COOKING_STORAGE_KEY)
      const session = mutateCookingSession(raw ? readCookingSession(JSON.parse(raw)) : null,
        options.sessionId, options.operation, options.stepIndex)
      localStorage.setItem(COOKING_STORAGE_KEY, JSON.stringify(session))
      return { session }
    },
    async clearCookingSession(options?: { sessionId?: string }) {
      if (typeof localStorage !== 'undefined') {
        const raw = localStorage.getItem(COOKING_STORAGE_KEY)
        if (options?.sessionId && raw && readCookingSession(JSON.parse(raw))?.sessionId !== options.sessionId) {
          throw new Error('Session cuisine remplacée')
        }
        localStorage.removeItem(COOKING_STORAGE_KEY)
      }
    },
    async startLiveActivity() {
      return {}
    },
    async updateLiveActivity() {
      // no-op on web
    },
    async endLiveActivity() {
      // no-op on web
    },
    async reloadWidgetTimelines() {
      // no-op on web
    },
  },
})
