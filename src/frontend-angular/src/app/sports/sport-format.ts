import type { ChallengeDayStatus, ChallengeReportDto, ChallengeUnit, GoalProgressDto, SessionFeel, SportOption, SportSessionDto, SportSessionRequest, SportTemplateDto } from './sports.models.ts'

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']
const DAYS = ['dim.', 'lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.']
const LONG_DAYS = ['Dimanche', 'Lundi', 'Mardi', 'Mercredi', 'Jeudi', 'Vendredi', 'Samedi']
const LONG_MONTHS = ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre']

/** ISO dates (yyyy-MM-dd) are read as calendar days, never shifted by a time zone. */
function parts(iso: string): Date { return new Date(`${iso}T12:00:00Z`) }

export function integer(value: number): string {
  return String(Math.round(value)).replace(/\B(?=(\d{3})+(?!\d))/g, ' ')
}
export function decimal(value: number, digits = 1): string {
  return (Math.round(value * 10 ** digits) / 10 ** digits).toFixed(digits).replace('.', ',')
}
export function duration(minutes: number): string {
  if (minutes < 60) return `${minutes} min`
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return rest ? `${hours} h ${String(rest).padStart(2, '0')}` : `${hours} h`
}
export function shortDay(iso: string): string { const d = parts(iso); return `${DAYS[d.getUTCDay()]} ${d.getUTCDate()}` }
export function dayMonth(iso: string): string { const d = parts(iso); return `${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}` }
export function longDay(iso: string): string {
  const d = parts(iso)
  return `${LONG_DAYS[d.getUTCDay()]} ${d.getUTCDate()} ${LONG_MONTHS[d.getUTCMonth()]}`
}
export function weekRange(monday: string, sunday: string): string { return `du ${dayMonth(monday)} au ${dayMonth(sunday)}` }

/** "5 min 34 /km" for walking sports, "km/h" otherwise; empty when it cannot be computed. */
export function pace(sport: string, minutes: number | null, km: number | null): string {
  if (!minutes || !km || km <= 0) return ''
  if (['running', 'walking', 'hiking'].includes(sport)) {
    const perKm = minutes / km
    let whole = Math.floor(perKm)
    let seconds = Math.round((perKm - whole) * 60)
    if (seconds === 60) { whole += 1; seconds = 0 }
    return `Allure : ${whole} min ${String(seconds).padStart(2, '0')} /km`
  }
  return `Vitesse : ${decimal(km / minutes * 60)} km/h`
}

/** The watch's five feelings, weakest first (ADR 0019). */
export const FEELS: { code: SessionFeel, label: string }[] = [
  { code: 'very_weak', label: 'Très faible' },
  { code: 'weak', label: 'Faible' },
  { code: 'normal', label: 'Normale' },
  { code: 'strong', label: 'Forte' },
  { code: 'very_strong', label: 'Très forte' },
]

/** Paris wall clock "HH:mm" of an instant, whatever the device's time zone. */
export function parisTime(instant: string): string {
  return new Intl.DateTimeFormat('fr-FR', { timeZone: 'Europe/Paris', hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(instant))
}
/** "16 h 24 – 17 h 07". */
export function timeRange(session: Pick<SportSessionDto, 'startedAt' | 'endedAt'>): string {
  const clock = (instant: string) => parisTime(instant).replace(':', ' h ')
  return `${clock(session.startedAt)} – ${clock(session.endedAt)}`
}

export function sessionTitle(session: SportSessionDto): string {
  if (session.templateName) return session.templateName
  return session.garminName && session.garminName !== session.sportLabel ? `${session.sportLabel} · ${session.garminName}` : session.sportLabel
}
export function sessionDetails(session: SportSessionDto): string {
  const bits = [timeRange(session), duration(session.durationMinutes)]
  if (session.distanceKm !== null) bits.push(`${decimal(session.distanceKm, 2)} km`)
  if (session.averageHeartRate !== null) bits.push(`FC ${session.averageHeartRate}`)
  if (session.effort !== null) bits.push(`effort ${session.effort}/10`)
  return bits.join(' · ')
}
/** Pace or speed from the watch's average speed, or from duration and distance. */
export function sessionPace(session: SportSessionDto): string {
  if (session.averageSpeedKmh && session.averageSpeedKmh > 0) {
    const minutes = session.distanceKm ? session.distanceKm / session.averageSpeedKmh * 60 : 0
    if (['running', 'walking', 'hiking'].includes(session.sport) && minutes) return pace(session.sport, minutes, session.distanceKm)
    if (session.sport === 'swimming' && session.distanceKm) {
      const per100 = 6 / session.averageSpeedKmh
      return `Allure : ${Math.floor(per100)} min ${String(Math.round((per100 % 1) * 60)).padStart(2, '0')} /100 m`
    }
    return `Vitesse : ${decimal(session.averageSpeedKmh)} km/h`
  }
  return pace(session.sport, session.durationMinutes, session.distanceKm)
}
/** Minutes per heart rate zone with their share of the total, for bars. */
export function zoneRows(seconds: number[] | null): { label: string, minutes: number, percent: number }[] {
  if (!seconds || seconds.length !== 5) return []
  const total = seconds.reduce((sum, value) => sum + value, 0) || 1
  return seconds.map((value, index) => ({ label: `Zone ${index + 1}`, minutes: Math.round(value / 60), percent: Math.round(value * 100 / total) }))
}
export function sessionCalories(session: SportSessionDto): string {
  return session.calories === null ? 'kcal ?' : `${integer(session.calories)} kcal`
}

export function goalProgressText(progress: GoalProgressDto): string {
  const { goal, achieved } = progress
  if (goal.kind !== 'volume') return `${achieved} / ${goal.target}`
  if (goal.unit === 'kcal') return `${integer(achieved)} / ${integer(goal.target)}${progress.partial ? ' (partiel)' : ''}`
  if (goal.unit === 'km') return `${decimal(achieved)} / ${decimal(goal.target)} km`
  return `${decimal(achieved)} / ${decimal(goal.target)} h`
}
export function goalPercent(progress: GoalProgressDto): number {
  return Math.max(0, Math.min(100, Math.round(progress.achieved * 100 / progress.goal.target)))
}

export function challengeAmount(unit: ChallengeUnit, value: number): string {
  if (unit === 'reps') return integer(value)
  return value >= 60 ? `${integer(Math.floor(value / 60))} h${value % 60 ? ` ${String(value % 60).padStart(2, '0')}` : ''}` : `${value} min`
}
export function challengeUnitLabel(unit: ChallengeUnit): string { return unit === 'reps' ? 'répétitions' : 'minutes' }

/** Quick "+n" buttons: three steps for a large target, two for a small one. */
export function quickSteps(unit: ChallengeUnit, target: number): number[] {
  if (unit === 'minutes') return [5, 10, 20]
  if (target >= 50) return [10, 20, 25]
  if (target >= 20) return [5, 10]
  return [1, 5]
}
export function challengePercent(report: ChallengeReportDto): number {
  return Math.min(100, Math.round(report.todayTotal * 100 / Math.max(1, report.todayTarget)))
}
export function streakLine(report: ChallengeReportDto): string {
  const days = report.streak > 1 ? 'jours' : 'jour'
  return `Série : ${report.streak} ${days}${report.reachedToday ? '' : ' (aujourd’hui en cours)'} · record ${report.record}`
}
export function jokerLine(report: ChallengeReportDto): string {
  if (!report.jokersPerWeek) return 'Sans joker'
  const left = report.jokersLeftThisWeek
  return `Jokers cette semaine : ${left} sur ${report.jokersPerWeek} restant${left > 1 ? 's' : ''}`
}
export function entryTime(loggedAt: string): string {
  const date = new Date(loggedAt)
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}

/** Message shown when an added set reaches the day's target or crosses a milestone; empty otherwise. */
export function celebration(before: ChallengeReportDto, after: ChallengeReportDto): string {
  const unit = after.challenge.unit
  const crossed = after.milestones.filter(milestone => before.total < milestone && after.total >= milestone).pop()
  if (crossed) {
    const noun = unit === 'reps' ? ` ${after.challenge.name.toLocaleLowerCase('fr')}` : ''
    return `Palier atteint : ${challengeAmount(unit, crossed)}${noun} au total !`
  }
  if (!before.reachedToday && after.reachedToday) return `${after.challenge.name} : défi du jour atteint. Série de ${after.streak} jour${after.streak > 1 ? 's' : ''}.`
  return ''
}

/** Calendar cell level, from the day's percentage of its target. */
export function dayLevel(status: ChallengeDayStatus, percent: number): string {
  if (status === 'not_counted') return 'none'
  if (status === 'joker') return 'joker'
  if (percent >= 100) return 'full'
  if (percent >= 50) return 'high'
  if (percent > 0) return 'low'
  return 'zero'
}

/** Pads the calendar so that columns are weeks starting on Monday and the current week ends on Sunday. */
export function calendarWeeks<T extends { date: string }>(days: T[]): (T | null)[][] {
  if (!days.length) return []
  const first = parts(days[0]!.date)
  const lead = (first.getUTCDay() + 6) % 7
  const cells: (T | null)[] = [...Array<null>(lead).fill(null), ...days]
  while (cells.length % 7) cells.push(null)
  const weeks: (T | null)[][] = []
  for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7))
  return weeks
}

export interface SessionDraft {
  date: string
  startTime: string
  endTime: string
  sport: string
  templateId: string | null
  /** Active time; end minus start when left empty. */
  durationMinutes: number | null
  distanceKm: number | null
  calories: number | null
  effort: number | null
  feel: SessionFeel | null
  averageHeartRate: number | null
  maxHeartRate: number | null
  notes: string
}

export function emptySessionDraft(today: string, now = new Date()): SessionDraft {
  const end = parisTime(now.toISOString())
  const start = addMinutes(end, -60)
  return { date: today, startTime: start, endTime: end, sport: 'running', templateId: null, durationMinutes: null, distanceKm: null, calories: null, effort: null, feel: null, averageHeartRate: null, maxHeartRate: null, notes: '' }
}

export function draftFromSession(session: SportSessionDto): SessionDraft {
  return {
    date: session.date, startTime: parisTime(session.startedAt), endTime: parisTime(session.endedAt), sport: session.sport,
    templateId: session.templateId, durationMinutes: session.durationMinutes,
    distanceKm: session.distanceKm, calories: session.calories, effort: session.effort, feel: session.feel,
    averageHeartRate: session.averageHeartRate, maxHeartRate: session.maxHeartRate, notes: session.notes,
  }
}

/** "HH:mm" shifted by minutes, on a 24 h clock. */
export function addMinutes(time: string, minutes: number): string {
  const [h, m] = time.split(':').map(Number)
  const total = (((h! * 60 + m! + minutes) % 1440) + 1440) % 1440
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}

/** Minutes from start to end; an end before the start is on the next day. Null when a time is missing. */
export function spanMinutes(start: string, end: string): number | null {
  if (!/^\d{2}:\d{2}$/.test(start) || !/^\d{2}:\d{2}$/.test(end)) return null
  const minutes = (value: string) => Number(value.slice(0, 2)) * 60 + Number(value.slice(3))
  const span = minutes(end) - minutes(start)
  return span > 0 ? span : span + 1440
}

/** Copies a template's values into the draft; the end follows the template's duration. */
export function applyTemplate(draft: SessionDraft, template: SportTemplateDto): SessionDraft {
  return { ...draft, templateId: template.id, sport: template.sport, endTime: addMinutes(draft.startTime, template.durationMinutes),
    durationMinutes: null, distanceKm: template.distanceKm, calories: template.calories }
}

const numberOrNull = (value: number | string | null | undefined): number | null =>
  value === null || value === undefined || value === '' || Number.isNaN(Number(value)) ? null : Number(value)

/** Builds the request; the distance is dropped for sports without one. Returns an error message when invalid. */
export function sessionRequest(draft: SessionDraft, sports: SportOption[]): SportSessionRequest | string {
  const span = spanMinutes(draft.startTime, draft.endTime)
  if (span === null) return 'Les heures de début et de fin sont obligatoires.'
  if (draft.startTime === draft.endTime) return 'La fin de la séance doit suivre son début.'
  const minutes = numberOrNull(draft.durationMinutes)
  if (minutes !== null && (minutes <= 0 || minutes > span)) return 'La durée active doit tenir entre le début et la fin.'
  const hasDistance = sports.find(sport => sport.code === draft.sport)?.hasDistance ?? false
  return {
    date: draft.date, startTime: draft.startTime, endTime: draft.endTime, sport: draft.sport, templateId: draft.templateId,
    durationMinutes: minutes === null ? null : Math.round(minutes),
    distanceKm: hasDistance ? numberOrNull(draft.distanceKm) : null, calories: numberOrNull(draft.calories),
    effort: numberOrNull(draft.effort), feel: draft.feel, averageHeartRate: numberOrNull(draft.averageHeartRate),
    maxHeartRate: numberOrNull(draft.maxHeartRate), notes: draft.notes.trim(),
  }
}
