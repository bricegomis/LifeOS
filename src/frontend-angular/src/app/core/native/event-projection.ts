import type { ManualDay, ManualWeek } from '../api/manual-planner.models.ts'
import type { AppGroupEventItem } from './native.models.ts'

/**
 * Projects a local civil date and minute of day (0..1439) into an ISO string
 * formatted for that specific date in the given IANA timeZoneId, handling standard vs DST offsets.
 */
export function civilTimeToIso(dateStr: string, minute: number, timeZoneId: string): string {
  const [year, month, day] = dateStr.split('-').map(Number)
  const hour = Math.floor(minute / 60)
  const min = minute % 60

  // Format candidate UTC time and determine actual offset at that local time
  // Using Intl.DateTimeFormat to compute the civil offset for that specific instant
  const approxUtc = new Date(Date.UTC(year!, month! - 1, day!, hour, min))
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone: timeZoneId,
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: 'numeric',
    minute: 'numeric',
    second: 'numeric',
    hour12: false,
  })

  // Format parts to see civil time represented by approxUtc in the target timezone
  const parts = formatter.formatToParts(approxUtc)
  const getPart = (type: string) => Number(parts.find(p => p.type === type)?.value ?? 0)

  const civilYear = getPart('year')
  const civilMonth = getPart('month')
  const civilDay = getPart('day')
  let civilHour = getPart('hour')
  if (civilHour === 24) civilHour = 0
  const civilMinute = getPart('minute')

  // Calculate the difference between target civil time and what approxUtc evaluates to in that timezone
  const targetMinutes = (day! * 1440) + (hour * 60) + min
  const resultMinutes = (civilDay * 1440) + (civilHour * 60) + civilMinute
  const diffMinutes = targetMinutes - resultMinutes

  // Adjust approxUtc by the difference
  const exactInstant = new Date(approxUtc.getTime() + diffMinutes * 60 * 1000)

  return exactInstant.toISOString()
}

/**
 * Extracts and sorts all future/upcoming and current events from manual weeks across a rolling horizon.
 * Events without startMinute are included with startIso = null.
 */
export function projectEventsFromWeeks(
  weeks: ManualWeek[],
  now: Date,
  timeZoneId: string,
): AppGroupEventItem[] {
  const allEvents: AppGroupEventItem[] = []

  for (const week of weeks) {
    const tz = week.timeZoneId || timeZoneId
    for (const day of week.days) {
      for (const meal of day.meals) {
        const startIso = meal.startMinute !== null ? civilTimeToIso(day.date, meal.startMinute, tz) : null
        const endIso = meal.endMinute !== null ? civilTimeToIso(day.date, meal.endMinute, tz) : null
        allEvents.push({
          id: `meal-${meal.id}`,
          kind: 'meal',
          title: meal.name,
          subtitle: meal.recipeLines.length > 0
            ? meal.recipeLines.map(r => r.name || 'Recette').join(', ')
            : 'Repas',
          date: day.date,
          startMinute: meal.startMinute,
          endMinute: meal.endMinute,
          startIso,
          endIso,
          deepLink: `/planning?date=${day.date}`,
        })
      }

      for (const sport of day.sports) {
        const startIso = sport.startMinute !== null ? civilTimeToIso(day.date, sport.startMinute, tz) : null
        const endIso = sport.endMinute !== null ? civilTimeToIso(day.date, sport.endMinute, tz) : null
        allEvents.push({
          id: `sport-${sport.id}`,
          kind: 'sport',
          title: sport.name,
          subtitle: `${sport.sport} · ${sport.durationMinutes} min`,
          date: day.date,
          startMinute: sport.startMinute,
          endMinute: sport.endMinute,
          startIso,
          endIso,
          deepLink: `/planning?date=${day.date}`,
        })
      }
    }
  }

  // Sort events chronologically:
  // 1. By date string
  // 2. Events with startMinute (ascending), followed by events without startMinute (nulls at end)
  allEvents.sort((a, b) => {
    if (a.date !== b.date) return a.date.localeCompare(b.date)
    if (a.startMinute !== null && b.startMinute !== null) return a.startMinute - b.startMinute
    if (a.startMinute !== null) return -1
    if (b.startMinute !== null) return 1
    return a.title.localeCompare(b.title)
  })

  return allEvents
}
