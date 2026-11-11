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
  // The cooking session was removed (ADR 0010); the native snapshot schema still carries the key.
  cookingSession: null
}
