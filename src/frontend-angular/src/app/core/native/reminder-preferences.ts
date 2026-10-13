import type { ReminderPreferences } from './native.models.ts'

export const DEFAULT_REMINDER_PREFERENCES: ReminderPreferences = {
  enabled: false,
  leadMinutes: 15,
  meals: true,
  sports: true,
}

export function validateReminderPreferences(raw: unknown): ReminderPreferences {
  if (!raw || typeof raw !== 'object') return DEFAULT_REMINDER_PREFERENCES
  const obj = raw as Partial<ReminderPreferences>

  const validLeads = [0, 5, 15, 30] as const
  const lead = validLeads.includes(obj.leadMinutes as 0 | 5 | 15 | 30)
    ? (obj.leadMinutes as 0 | 5 | 15 | 30)
    : DEFAULT_REMINDER_PREFERENCES.leadMinutes

  return {
    enabled: typeof obj.enabled === 'boolean' ? obj.enabled : DEFAULT_REMINDER_PREFERENCES.enabled,
    leadMinutes: lead,
    meals: typeof obj.meals === 'boolean' ? obj.meals : DEFAULT_REMINDER_PREFERENCES.meals,
    sports: typeof obj.sports === 'boolean' ? obj.sports : DEFAULT_REMINDER_PREFERENCES.sports,
  }
}
