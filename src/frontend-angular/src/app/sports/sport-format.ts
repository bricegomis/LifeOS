import type { ChallengeDayStatus, ChallengeReportDto, ChallengeUnit, GoalProgressDto, SportOption, SportSessionDto, SportSessionRequest, SportTemplateDto } from './sports.models.ts'

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

export function sessionTitle(session: SportSessionDto): string { return session.templateName ?? session.sportLabel }
export function sessionDetails(session: SportSessionDto): string {
  const bits = [session.sportLabel, duration(session.durationMinutes)]
  if (session.distanceKm !== null) bits.push(`${decimal(session.distanceKm)} km`)
  if (session.feeling !== null) bits.push(`ressenti ${session.feeling}/10`)
  if (session.averageHeartRate !== null) bits.push(`FC ${session.averageHeartRate}`)
  return bits.join(' · ')
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
  sport: string
  templateId: string | null
  durationMinutes: number | null
  distanceKm: number | null
  calories: number | null
  feeling: number | null
  averageHeartRate: number | null
  maxHeartRate: number | null
  notes: string
}

export function emptySessionDraft(today: string): SessionDraft {
  return { date: today, sport: 'running', templateId: null, durationMinutes: null, distanceKm: null, calories: null, feeling: null, averageHeartRate: null, maxHeartRate: null, notes: '' }
}

export function draftFromSession(session: SportSessionDto): SessionDraft {
  return {
    date: session.date, sport: session.sport, templateId: session.templateId, durationMinutes: session.durationMinutes,
    distanceKm: session.distanceKm, calories: session.calories, feeling: session.feeling,
    averageHeartRate: session.averageHeartRate, maxHeartRate: session.maxHeartRate, notes: session.notes,
  }
}

/** Copies a template's values into the draft; the session keeps them as its own. */
export function applyTemplate(draft: SessionDraft, template: SportTemplateDto): SessionDraft {
  return { ...draft, templateId: template.id, sport: template.sport, durationMinutes: template.durationMinutes, distanceKm: template.distanceKm, calories: template.calories }
}

const numberOrNull = (value: number | string | null | undefined): number | null =>
  value === null || value === undefined || value === '' || Number.isNaN(Number(value)) ? null : Number(value)

/** Builds the request; the distance is dropped for sports without one. Returns an error message when invalid. */
export function sessionRequest(draft: SessionDraft, sports: SportOption[]): SportSessionRequest | string {
  const minutes = numberOrNull(draft.durationMinutes)
  if (!minutes || minutes <= 0) return 'La durée est obligatoire.'
  const hasDistance = sports.find(sport => sport.code === draft.sport)?.hasDistance ?? false
  return {
    date: draft.date, sport: draft.sport, templateId: draft.templateId, durationMinutes: Math.round(minutes),
    distanceKm: hasDistance ? numberOrNull(draft.distanceKm) : null, calories: numberOrNull(draft.calories),
    feeling: numberOrNull(draft.feeling), averageHeartRate: numberOrNull(draft.averageHeartRate),
    maxHeartRate: numberOrNull(draft.maxHeartRate), notes: draft.notes.trim(),
  }
}
