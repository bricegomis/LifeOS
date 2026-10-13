import { Injectable, inject, signal } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { Capacitor } from '@capacitor/core'
import { App as CapApp } from '@capacitor/app'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { AuthService } from '@/app/core/auth/auth.service'
import type { ManualWeek, ManualWeekSummary } from '@/app/core/api/manual-planner.models'
import { monday, isoDate, addDays } from '@/app/pages/planning/calendar'
import type { AppGroupWidgetSnapshot } from './native.models'
import { projectEventsFromWeeks } from './event-projection'
import { LifeosNativeBridge } from './native-bridge'
import { LocalRemindersService } from './local-reminders.service'
import { CookingSessionService } from './cooking-session.service'

const LAST_IDENTITY_KEY = 'lifeos.native.activeIdentity'

@Injectable({ providedIn: 'root' })
export class NativeSyncService {
  private readonly api = inject(LifeosApiService)
  private readonly auth = inject(AuthService)
  private readonly reminders = inject(LocalRemindersService)
  private readonly cooking = inject(CookingSessionService)

  readonly syncStatus = signal<'idle' | 'syncing' | 'synced' | 'stale' | 'error'>('idle')
  readonly lastSyncError = signal<string | null>(null)
  readonly lastSyncedAt = signal<string | null>(null)

  private isSyncing = false
  private needsRerun = false

  initListeners(): void {
    if (!Capacitor.isNativePlatform()) return
    try {
      void CapApp.addListener('appStateChange', (state) => {
        if (state.isActive) {
          void this.cooking.restoreSession()
          void this.syncNativeState()
        }
      })
    } catch {
      // not running in native app
    }
  }

  async resolveHouseholdId(): Promise<string> {
    const household = await firstValueFrom(this.api.get<{ id: string }>('/households/current'))
    if (!household?.id) {
      throw new Error('Foyer courant non disponible')
    }
    return household.id
  }

  /**
   * Refreshes the widget snapshot and updates local reminders for the rolling 2-3 weeks horizon.
   * If a sync is already running, coalesces incoming requests into a subsequent rerun.
   */
  async syncNativeState(): Promise<void> {
    if (this.isSyncing) {
      this.needsRerun = true
      return
    }

    const user = this.auth.user()
    if (!user) {
      await this.purgeAll()
      return
    }

    this.isSyncing = true
    this.syncStatus.set('syncing')

    try {
      // 1. Identity isolation: purge prior state immediately if user changed
      if (typeof localStorage !== 'undefined') {
        const lastIdentity = localStorage.getItem(LAST_IDENTITY_KEY)
        if (lastIdentity) {
          const [lastUserId] = lastIdentity.split(':')
          if (lastUserId && lastUserId !== user.id) {
            await this.purgeAll()
          }
        }
      }

      // 2. Resolve verified household ID; if API fails, throw to keep previous snapshot intact
      const householdId = await this.resolveHouseholdId()
      const identityKey = `${user.id}:${householdId}`

      if (typeof localStorage !== 'undefined') {
        const lastIdentity = localStorage.getItem(LAST_IDENTITY_KEY)
        if (lastIdentity && lastIdentity !== identityKey) {
          await this.purgeAll()
        }
        localStorage.setItem(LAST_IDENTITY_KEY, identityKey)
      }

      // 3. Fetch week summaries
      const summaries = await firstValueFrom(this.api.get<ManualWeekSummary[]>('/manual-planner/weeks'))
      if (!summaries || summaries.length === 0) {
        await this.writeEmptySnapshot(user.id, householdId)
        this.syncStatus.set('synced')
        this.lastSyncError.set(null)
        this.lastSyncedAt.set(new Date().toISOString())
        return
      }

      // Determine rolling horizon: current week and next week
      const currentMonday = monday(isoDate())
      const nextMonday = addDays(currentMonday, 7)

      const targetSummaries = summaries.filter(s => s.startsOn === currentMonday || s.startsOn === nextMonday)
      const weeks: ManualWeek[] = []

      for (const summary of targetSummaries) {
        const detail = await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${summary.id}`))
        weeks.push(detail)
      }

      const tz = weeks[0]?.timeZoneId || 'Europe/Paris'
      const events = projectEventsFromWeeks(weeks, new Date(), tz)

      // Sync cooking session
      const cooking = await this.cooking.restoreSession()

      const snapshot: AppGroupWidgetSnapshot = {
        version: 1,
        householdId,
        userId: user.id,
        generatedAt: new Date().toISOString(),
        timeZoneId: tz,
        events,
        cookingSession: cooking ? {
          sessionId: cooking.sessionId,
          recipeId: cooking.recipeId,
          recipeTitle: cooking.recipeTitle,
          startedAt: cooking.startedAt,
          steps: cooking.steps,
          completedStepIndices: cooking.completedStepIndices,
          isCompleted: cooking.isCompleted,
        } : null,
      }

      await LifeosNativeBridge.saveWidgetSnapshot({ snapshot })
      await LifeosNativeBridge.reloadWidgetTimelines()

      // Reschedule reminders
      await this.reminders.rescheduleNotifications(events)

      this.syncStatus.set('synced')
      this.lastSyncError.set(null)
      this.lastSyncedAt.set(new Date().toISOString())
    } catch (error) {
      const msg = apiErrorMessage(error)
      this.lastSyncError.set(msg)
      // Never wipe valid local snapshot on network failure: mark stale or error
      this.syncStatus.set(this.lastSyncedAt() ? 'stale' : 'error')
    } finally {
      this.isSyncing = false
      if (this.needsRerun) {
        this.needsRerun = false
        void this.syncNativeState()
      }
    }
  }

  async purgeAll(): Promise<void> {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(LAST_IDENTITY_KEY)
    }
    this.syncStatus.set('idle')
    this.lastSyncError.set(null)
    this.lastSyncedAt.set(null)

    const errors: unknown[] = []
    try {
      await LifeosNativeBridge.clearWidgetSnapshot()
    } catch (e) {
      errors.push(e)
    }
    try {
      await LifeosNativeBridge.clearCookingSession()
    } catch (e) {
      errors.push(e)
    }
    try {
      await LifeosNativeBridge.endLiveActivity()
    } catch (e) {
      errors.push(e)
    }
    try {
      await LifeosNativeBridge.reloadWidgetTimelines()
    } catch (e) {
      errors.push(e)
    }
    try {
      await this.reminders.cancelAll()
    } catch (e) {
      errors.push(e)
    }

    if (errors.length > 0) {
      this.lastSyncError.set(`Purge partielle: ${(errors[0] as Error)?.message ?? 'erreur pont natif'}`)
    }
  }

  private async writeEmptySnapshot(userId: string, householdId: string): Promise<void> {
    const snapshot: AppGroupWidgetSnapshot = {
      version: 1,
      householdId,
      userId,
      generatedAt: new Date().toISOString(),
      timeZoneId: 'Europe/Paris',
      events: [],
      cookingSession: null,
    }
    await LifeosNativeBridge.saveWidgetSnapshot({ snapshot })
    await LifeosNativeBridge.reloadWidgetTimelines()
    await this.reminders.cancelAll()
  }
}
