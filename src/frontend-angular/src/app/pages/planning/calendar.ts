import type { ManualDay, ManualMeal, ManualSport } from '../../core/api/manual-planner.models.ts'

export const DISPLAY_START = 360
export const DISPLAY_END = 1200
export const HOUR_HEIGHT = 96

export function isoDate(date = new Date()): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

export function addDays(date: string, days: number): string {
  const [year, month, day] = date.split('-').map(Number)
  return isoDate(new Date(year!, month! - 1, day! + days))
}

export function monday(date = isoDate()): string {
  const [year, month, day] = date.split('-').map(Number)
  const weekday = new Date(year!, month! - 1, day!).getDay()
  return addDays(date, -((weekday + 6) % 7))
}

export function timeLabel(minute: number | null): string {
  if (minute === null) return 'À positionner'
  return `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`
}

export function parseTime(value: string): number | null {
  if (!/^\d{2}:\d{2}$/.test(value)) return null
  const [hour, minute] = value.split(':').map(Number)
  if (hour! > 23 || minute! > 59) return null
  return hour! * 60 + minute!
}

export interface CalendarEvent {
  key: string
  kind: 'meal' | 'sport'
  value: ManualMeal | ManualSport
  lane: number
  lanes: number
  top: number
  height: number
}

export function dayEvents(day: ManualDay): CalendarEvent[] {
  return [
    ...day.meals.map(value => ({ key: `meal-${value.id}`, kind: 'meal' as const, value })),
    ...day.sports.map(value => ({ key: `sport-${value.id}`, kind: 'sport' as const, value })),
  ].sort((a, b) => (a.value.startMinute ?? 1441) - (b.value.startMinute ?? 1441))
    .map(event => ({ ...event, lane: 0, lanes: 1, top: 0, height: 0 }))
}

export function outsideDisplay(event: CalendarEvent): boolean {
  return event.value.startMinute !== null &&
    (event.value.startMinute < DISPLAY_START || (event.value.endMinute ?? DISPLAY_END) > DISPLAY_END)
}

export function layoutEvents(day: ManualDay): CalendarEvent[] {
  const events = dayEvents(day).filter(e => e.value.startMinute !== null && !outsideDisplay(e))
  const ends: number[] = []
  let group: CalendarEvent[] = []
  const finish = () => { for (const event of group) event.lanes = ends.length; group = []; ends.length = 0 }
  for (const event of events) {
    const start = event.value.startMinute!
    if (ends.length && ends.every(end => end <= start)) finish()
    let lane = ends.findIndex(end => end <= start)
    if (lane < 0) lane = ends.length
    event.lane = lane
    event.top = (start - DISPLAY_START) / 60 * HOUR_HEIGHT
    event.height = Math.max(64, (event.value.endMinute! - start) / 60 * HOUR_HEIGHT)
    event.height = Math.min(event.height, (DISPLAY_END - start) / 60 * HOUR_HEIGHT)
    ends[lane] = start + event.height / HOUR_HEIGHT * 60
    group.push(event)
  }
  finish()
  return events
}
