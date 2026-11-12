import { Capacitor, registerPlugin } from '@capacitor/core'
import type { AppGroupWidgetSnapshot } from './native.models'

// Written by app versions that still had the cooking session (removed by ADR 0010).
const LEGACY_COOKING_STORAGE_KEY = 'lifeos.native.cookingSession'

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
  /** Legacy cleanup only: the Swift side still stores sessions started by older versions. */
  clearCookingSession(): Promise<void>
  /** Legacy cleanup only: ends a Live Activity started by an older version. */
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
    async clearCookingSession() {
      if (typeof localStorage !== 'undefined') localStorage.removeItem(LEGACY_COOKING_STORAGE_KEY)
    },
    async endLiveActivity() {
      // no-op on web
    },
    async reloadWidgetTimelines() {
      // no-op on web
    },
  },
})
