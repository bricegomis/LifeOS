export interface AppGroupEventItem {
  id: string
  kind: 'meal' | 'sport'
  title: string
  subtitle: string
  date: string // YYYY-MM-DD
  startMinute: number | null
  endMinute: number | null
  startIso: string | null // ISO timestamp with timezone offset
  endIso: string | null
  deepLink: string
}

export interface AppGroupWidgetSnapshot {
  version: 1
  householdId: string
  userId: string
  generatedAt: string
  timeZoneId: string
  events: AppGroupEventItem[]
  cookingSession: AppGroupCookingSession | null
}

export interface AppGroupCookingSession {
  version: 2
  revision: number
  sessionId: string
  recipeId: string | null
  recipeTitle: string
  startedAt: string
  steps: string[]
  completedStepIndices: number[]
  isCompleted: boolean
  updatedAt: string
}

export type CookingSessionState = AppGroupCookingSession

export interface ReminderPreferences {
  enabled: boolean
  leadMinutes: 0 | 5 | 15 | 30
  meals: boolean
  sports: boolean
}
