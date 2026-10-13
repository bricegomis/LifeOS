import { Injectable, effect, inject, signal } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { Capacitor } from '@capacitor/core'
import { App as CapApp } from '@capacitor/app'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { AuthService } from '@/app/core/auth/auth.service'
import type { ManualWeek, ManualWeekSummary } from '@/app/core/api/manual-planner.models'
import { monday, isoDate, addDays } from '@/app/pages/planning/calendar'
import type { AppGroupWidgetSnapshot } from './native.models'
import { projectEventsFromWeeks } from './event-projection'
import { LifeosNativeBridge, requireNativeBridge } from './native-bridge'
import { LocalRemindersService } from './local-reminders.service'
import { CookingSessionService } from './cooking-session.service'
import { nativeStateCoordinator, SupersededNativeOperation } from './native-state-coordinator'

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
  private listening = false
  private observedUser: string | null | undefined
  private authorizedUser: string | null = null

  constructor() {
    nativeStateCoordinator.pause()
    effect(() => {
      if (!this.auth.ready()) return
      const userId = this.auth.user()?.id ?? null
      if (this.observedUser === userId) return
      this.observedUser = userId
      const previous = typeof localStorage === 'undefined' ? null : localStorage.getItem(LAST_IDENTITY_KEY)
      void this.acceptIdentity(userId, previous).catch(error => this.reportError(error))
    })
  }

  private async acceptIdentity(userId: string | null, previous: string | null): Promise<void> {
    if (!userId || !previous?.startsWith(`${userId}:`)) await this.purgeAll()
    if (userId && this.auth.user()?.id === userId && this.observedUser === userId) {
      this.authorizedUser = userId
      nativeStateCoordinator.resume()
      await this.syncNativeState()
    }
  }

  async initListeners(): Promise<void> {
    if (this.listening || !Capacitor.isNativePlatform()) return
    this.listening = true
    try {
      await CapApp.addListener('appStateChange', state => {
        if (state.isActive) void this.syncNativeState()
      })
    } catch (error) {
      this.listening = false
      this.reportError(error)
    }
  }

  async resolveHouseholdId(): Promise<string> {
    const household = await firstValueFrom(this.api.get<{ id: string }>('/households/current'))
    if (!household?.id) throw new Error('Foyer courant non disponible')
    return household.id
  }

  async syncNativeState(): Promise<void> {
    const user = this.auth.user()
    if (!this.auth.ready() || !user || this.authorizedUser !== user.id) return
    if (this.isSyncing) {
      this.needsRerun = true
      return
    }
    this.isSyncing = true
    this.syncStatus.set('syncing')
    let token = nativeStateCoordinator.token()
    const assertCurrent = () => {
      if (!nativeStateCoordinator.isCurrent(token) || this.auth.user()?.id !== user.id || this.authorizedUser !== user.id) {
        throw new SupersededNativeOperation()
      }
    }
    try {
      const householdId = await this.resolveHouseholdId()
      assertCurrent()
      const identityKey = `${user.id}:${householdId}`
      const previous = typeof localStorage === 'undefined' ? null : localStorage.getItem(LAST_IDENTITY_KEY)
      if (previous && previous !== identityKey) {
        await this.purgeAll()
        if (this.auth.user()?.id !== user.id) throw new SupersededNativeOperation()
        this.authorizedUser = user.id
        nativeStateCoordinator.resume()
        token = nativeStateCoordinator.token()
      }
      assertCurrent()
      if (typeof localStorage !== 'undefined') localStorage.setItem(LAST_IDENTITY_KEY, identityKey)
      const summaries = await firstValueFrom(this.api.get<ManualWeekSummary[]>('/manual-planner/weeks'))
      assertCurrent()
      const currentMonday = monday(isoDate())
      const nextMonday = addDays(currentMonday, 7)
      const weeks: ManualWeek[] = []
      for (const summary of summaries.filter(s => s.startsOn === currentMonday || s.startsOn === nextMonday)) {
        weeks.push(await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${summary.id}`)))
        assertCurrent()
      }
      const tz = weeks[0]?.timeZoneId || 'Europe/Paris'
      const events = projectEventsFromWeeks(weeks, new Date(), tz)
      await this.cooking.restoreSession()
      assertCurrent()
      const snapshot: AppGroupWidgetSnapshot = {
        version: 1, householdId, userId: user.id, generatedAt: new Date().toISOString(),
        timeZoneId: tz, events,
        // Cooking has one authoritative file, not a stale duplicate in the planning snapshot.
        cookingSession: null,
      }
      await nativeStateCoordinator.run(token, async () => {
        assertCurrent()
        requireNativeBridge()
        await LifeosNativeBridge.saveWidgetSnapshot({ snapshot })
        await LifeosNativeBridge.reloadWidgetTimelines()
        await this.reminders.rescheduleNotifications(events)
      })
      assertCurrent()
      this.syncStatus.set('synced')
      this.lastSyncError.set(null)
      this.lastSyncedAt.set(snapshot.generatedAt)
    } catch (error) {
      if (!(error instanceof SupersededNativeOperation) && nativeStateCoordinator.isCurrent(token)) this.reportError(error)
    } finally {
      this.isSyncing = false
      if (this.needsRerun) {
        this.needsRerun = false
        void this.syncNativeState()
      }
    }
  }

  async purgeAll(): Promise<void> {
    nativeStateCoordinator.pause()
    this.authorizedUser = null
    this.needsRerun = false
    const errors: unknown[] = []
    for (const clear of [
      () => this.cooking.clearLocalState(),
      () => this.reminders.clearPreferences(),
      () => { if (typeof localStorage !== 'undefined') localStorage.removeItem(LAST_IDENTITY_KEY) },
    ]) {
      try { clear() } catch (error) { errors.push(error) }
    }
    this.syncStatus.set('idle')
    this.lastSyncError.set(null)
    this.lastSyncedAt.set(null)
    await nativeStateCoordinator.cleanup(async () => {
      for (const operation of [
        () => LifeosNativeBridge.clearWidgetSnapshot(),
        () => LifeosNativeBridge.clearCookingSession(),
        () => LifeosNativeBridge.endLiveActivity(),
        () => LifeosNativeBridge.reloadWidgetTimelines(),
        () => this.reminders.cancelAll(),
      ]) {
        try { await operation() } catch (error) { errors.push(error) }
      }
      if (errors.length) {
        this.reportError(new Error(`Purge partielle : ${apiErrorMessage(errors[0])}`))
        throw new Error(this.lastSyncError() ?? 'Purge native impossible')
      }
    })
  }

  private reportError(error: unknown): void {
    this.lastSyncError.set(apiErrorMessage(error))
    this.syncStatus.set(this.lastSyncedAt() ? 'stale' : 'error')
  }
}
