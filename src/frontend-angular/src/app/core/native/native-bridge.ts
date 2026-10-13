import { registerPlugin } from '@capacitor/core'
import type { AppGroupWidgetSnapshot, CookingSessionState } from './native.models'

export interface LifeosNativeBridgePlugin {
  saveWidgetSnapshot(options: { snapshot: AppGroupWidgetSnapshot }): Promise<void>
  clearWidgetSnapshot(): Promise<void>
  getCookingSession(): Promise<{ session: CookingSessionState | null }>
  saveCookingSession(options: { session: CookingSessionState }): Promise<void>
  clearCookingSession(): Promise<void>
  startLiveActivity(options: {
    sessionId: string
    recipeTitle: string
    steps: string[]
    completedStepIndices: number[]
  }): Promise<{ activityId?: string }>
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
        const raw = localStorage.getItem('lifeos.native.cookingSession')
        if (raw) {
          try {
            return { session: JSON.parse(raw) as CookingSessionState }
          } catch {
            return { session: null }
          }
        }
      }
      return { session: null }
    },
    async saveCookingSession(options: { session: CookingSessionState }) {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem('lifeos.native.cookingSession', JSON.stringify(options.session))
      }
    },
    async clearCookingSession() {
      if (typeof localStorage !== 'undefined') {
        localStorage.removeItem('lifeos.native.cookingSession')
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
