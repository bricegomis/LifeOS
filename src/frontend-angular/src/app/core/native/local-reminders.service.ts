import { Injectable } from '@angular/core'
import { Capacitor } from '@capacitor/core'
import { LocalNotifications } from '@capacitor/local-notifications'
import type { AppGroupEventItem, ReminderPreferences } from './native.models'
import { hashNotificationId } from './notification-id'
import { DEFAULT_REMINDER_PREFERENCES, validateReminderPreferences } from './reminder-preferences'

export { DEFAULT_REMINDER_PREFERENCES, validateReminderPreferences }

const REMINDERS_PREFS_KEY = 'lifeos.native.remindersPreferences'

@Injectable({ providedIn: 'root' })
export class LocalRemindersService {
  getPreferences(): ReminderPreferences {
    if (typeof localStorage === 'undefined') return DEFAULT_REMINDER_PREFERENCES
    const raw = localStorage.getItem(REMINDERS_PREFS_KEY)
    if (!raw) return DEFAULT_REMINDER_PREFERENCES
    try {
      return validateReminderPreferences(JSON.parse(raw))
    } catch {
      return DEFAULT_REMINDER_PREFERENCES
    }
  }

  savePreferences(prefs: ReminderPreferences): void {
    const validated = validateReminderPreferences(prefs)
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(REMINDERS_PREFS_KEY, JSON.stringify(validated))
    }
  }

  async checkPermissions(): Promise<boolean> {
    if (!Capacitor.isNativePlatform()) return false
    try {
      const status = await LocalNotifications.checkPermissions()
      return status.display === 'granted'
    } catch {
      return false
    }
  }

  async requestPermissions(): Promise<boolean> {
    if (!Capacitor.isNativePlatform()) return false
    const res = await LocalNotifications.requestPermissions()
    return res.display === 'granted'
  }

  hashNotificationId(key: string): number {
    return hashNotificationId(key)
  }

  /**
   * Reschedules pending notifications based on current events and preferences.
   * Capped to max 50 pending notifications (iOS limit is 64).
   * Throws on scheduling failure so the caller can reflect the incident.
   */
  async rescheduleNotifications(events: AppGroupEventItem[], prefs = this.getPreferences()): Promise<void> {
    if (!Capacitor.isNativePlatform()) return

    // 1. Cancel all existing pending local notifications
    const pending = await LocalNotifications.getPending()
    if (pending.notifications.length > 0) {
      await LocalNotifications.cancel({
        notifications: pending.notifications.map(n => ({ id: n.id })),
      })
    }

    if (!prefs.enabled) {
      return
    }

    const hasPermission = await this.checkPermissions()
    if (!hasPermission) {
      throw new Error('Permission de notifications non accordée')
    }

    const now = Date.now()
    const notificationsToSchedule = []

    for (const ev of events) {
      if (ev.kind === 'meal' && !prefs.meals) continue
      if (ev.kind === 'sport' && !prefs.sports) continue
      if (!ev.startIso) continue

      const startTimestamp = new Date(ev.startIso).getTime()
      const notifyTimestamp = startTimestamp - prefs.leadMinutes * 60 * 1000

      // Skip if in the past
      if (notifyTimestamp <= now) continue

      const id = this.hashNotificationId(`${ev.id}-${ev.startIso}`)
      const leadText = prefs.leadMinutes === 0
        ? 'maintenant'
        : `dans ${prefs.leadMinutes} min`

      notificationsToSchedule.push({
        id,
        title: ev.kind === 'meal' ? `Repas : ${ev.title}` : `Sport : ${ev.title}`,
        body: `${ev.subtitle} · Prévu ${leadText}`,
        schedule: { at: new Date(notifyTimestamp) },
        extra: {
          eventId: ev.id,
          deepLink: ev.deepLink,
          date: ev.date,
        },
      })

      if (notificationsToSchedule.length >= 50) {
        break
      }
    }

    if (notificationsToSchedule.length > 0) {
      await LocalNotifications.schedule({
        notifications: notificationsToSchedule,
      })
    }
  }

  async cancelAll(): Promise<void> {
    if (!Capacitor.isNativePlatform()) return
    const pending = await LocalNotifications.getPending()
    if (pending.notifications.length > 0) {
      await LocalNotifications.cancel({
        notifications: pending.notifications.map(n => ({ id: n.id })),
      })
    }
  }
}
