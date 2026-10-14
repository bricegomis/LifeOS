import type { ManualDay, ManualWeek } from '../api/manual-planner.models.ts'
import type { AppGroupEventItem } from './native.models.ts'

/**
 * Projects a local civil date (YYYY-MM-DD) and minute of day (0..1439) into an ISO-8601 UTC string.
 * Disambiguates DST transitions (skips and overlaps) and handles month/year civil boundaries accurately
 * without day-number modulo or single-iteration offset approximations.
 */
export function civilTimeToIso(dateStr: string, minute: number, timeZoneId: string): string {
  const [year, month, day] = dateStr.split('-').map(Number)
  const hour = Math.floor(minute / 60)
  const min = minute % 60

  const targetUtcMs = Date.UTC(year!, month! - 1, day!, hour, min, 0)
  let candidateMs = targetUtcMs

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

  const getCivilTime = (ms: number) => {
    const parts = formatter.formatToParts(new Date(ms))
    const p: Record<string, number> = {}
    for (const part of parts) {
      if (part.type !== 'literal') p[part.type] = Number(part.value)
    }
    if (p['hour'] === 24) p['hour'] = 0
    return {
      year: p['year'] ?? year!,
      month: p['month'] ?? month!,
      day: p['day'] ?? day!,
      hour: p['hour'] ?? 0,
      minute: p['minute'] ?? 0,
      second: p['second'] ?? 0,
    }
  }

  let lastDiffMs = 0
  for (let iter = 0; iter < 5; iter++) {
    const civil = getCivilTime(candidateMs)
    const civilUtcCandidate = Date.UTC(civil.year, civil.month - 1, civil.day, civil.hour, civil.minute, civil.second)
    const diffMs = targetUtcMs - civilUtcCandidate
    if (diffMs === 0) break
    // In DST gap (missing hour during spring forward), alternating oscillations occur.
    // Detect oscillation and stop at the valid converted instant.
    if (iter > 0 && Math.abs(diffMs) === Math.abs(lastDiffMs) && diffMs !== lastDiffMs) {
      break
    }
    lastDiffMs = diffMs
    candidateMs += diffMs
  }

  return new Date(candidateMs).toISOString()
}

/**
 * Extracts and sorts all future/upcoming and current events from manual weeks across a rolling horizon.
 * Filters out events whose end time (or start time) is in the past relative to `now`.
 * Events without startMinute are retained for today and future days with startIso = null.
 */
export function projectEventsFromWeeks(
  weeks: ManualWeek[],
  now: Date,
  timeZoneId: string,
): AppGroupEventItem[] {
  const allEvents: AppGroupEventItem[] = []
  const nowMs = now.getTime()

  // Format today's civil date in the given timezone (YYYY-MM-DD)
  const dtf = new Intl.DateTimeFormat('en-CA', {
    timeZone: timeZoneId,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  })
  const todayStr = dtf.format(now) // en-CA emits YYYY-MM-DD

  for (const week of weeks) {
    const tz = week.timeZoneId || timeZoneId
    for (const day of week.days) {
      for (const meal of day.meals) {
        const startIso = meal.startMinute !== null ? civilTimeToIso(day.date, meal.startMinute, tz) : null
        const endIso = meal.endMinute !== null ? civilTimeToIso(day.date, meal.endMinute, tz) : null

        // If timed, drop events that ended in the past (using endIso or startIso + 45min default)
        if (startIso !== null) {
          const effectiveEndMs = endIso !== null ? new Date(endIso).getTime() : new Date(startIso).getTime() + 45 * 60 * 1000
          if (effectiveEndMs < nowMs) continue
        } else {
          // If untimed, drop events from days strictly before today
          if (day.date < todayStr) continue
        }

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

        if (startIso !== null) {
          const effectiveEndMs = endIso !== null
            ? new Date(endIso).getTime()
            : new Date(startIso).getTime() + (sport.durationMinutes || 45) * 60 * 1000
          if (effectiveEndMs < nowMs) continue
        } else {
          if (day.date < todayStr) continue
        }

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
