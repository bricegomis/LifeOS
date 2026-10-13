import { Injectable, inject } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { App as CapApp } from '@capacitor/app'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { AuthService } from '@/app/core/auth/auth.service'
import type { ManualWeek, ManualWeekSummary } from '@/app/core/api/manual-planner.models'
import { monday, isoDate, addDays } from '@/app/pages/planning/calendar'
import type { AppGroupWidgetSnapshot } from './native.models'
import { projectEventsFromWeeks } from './event-projection'
import { LifeosNativeBridge } from './native-bridge'
import { LocalRemindersService } from './local-reminders.service'
import { CookingSessionService } from './cooking-session.service'

@Injectable({ providedIn: 'root' })
export class NativeSyncService {
  private readonly api = inject(LifeosApiService)
  private readonly auth = inject(AuthService)
  private readonly reminders = inject(LocalRemindersService)
  private readonly cooking = inject(CookingSessionService)

  private isSyncing = false

  initListeners(): void {
    try {
      CapApp.addListener('appStateChange', (state) => {
        if (state.isActive) {
          void this.syncNativeState()
        }
      })
    } catch {
      // not running in native app
    }
  }

  /**
   * Refreshes the widget snapshot and updates local reminders for the rolling 2-3 weeks horizon.
   */
  async syncNativeState(): Promise<void> {
    if (this.isSyncing) return
    const user = this.auth.user()
    if (!user) {
      await this.purgeAll()
      return
    }

    this.isSyncing = true
    try {
      // Fetch week summaries
      const summaries = await firstValueFrom(this.api.get<ManualWeekSummary[]>('/manual-planner/weeks'))
      if (!summaries || summaries.length === 0) {
        await this.writeEmptySnapshot(user.id)
        return
      }

      // Determine rolling horizon: current week and next week
      const currentMonday = monday(isoDate())
      const nextMonday = addDays(currentMonday, 7)

      const targetSummaries = summaries.filter(s => s.startsOn === currentMonday || s.startsOn === nextMonday)
      const weeks: ManualWeek[] = []

      for (const summary of targetSummaries) {
        try {
          const detail = await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${summary.id}`))
          weeks.push(detail)
        } catch {
          // ignore single week load failure
        }
      }

      const tz = weeks[0]?.timeZoneId || 'Europe/Paris'
      const events = projectEventsFromWeeks(weeks, new Date(), tz)

      // Sync cooking session
      const cooking = await this.cooking.restoreSession()

      const snapshot: AppGroupWidgetSnapshot = {
        version: 1,
        householdId: 'current',
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
    } catch {
      // sync error
    } finally {
      this.isSyncing = false
    }
  }

  async purgeAll(): Promise<void> {
    try {
      await LifeosNativeBridge.clearWidgetSnapshot()
      await LifeosNativeBridge.clearCookingSession()
      await LifeosNativeBridge.endLiveActivity()
      await LifeosNativeBridge.reloadWidgetTimelines()
      await this.reminders.cancelAll()
    } catch {
      // ignore
    }
  }

  private async writeEmptySnapshot(userId: string): Promise<void> {
    const snapshot: AppGroupWidgetSnapshot = {
      version: 1,
      householdId: 'current',
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
