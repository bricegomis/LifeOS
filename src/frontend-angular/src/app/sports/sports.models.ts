/** Sport module models (ADR 0013), mirroring the API DTOs. */

export interface SportOption { code: string, label: string, hasDistance: boolean }
export interface SportCatalog { sports: SportOption[], today: string }

export interface SportTemplateRequest {
  name: string
  sport: string
  durationMinutes: number
  distanceKm: number | null
  calories: number
}

export interface SportTemplateDto extends SportTemplateRequest {
  id: string
  sportLabel: string
  isArchived: boolean
}

export interface SportSessionRequest {
  memberProfileId?: string | null
  date: string
  sport: string
  templateId: string | null
  durationMinutes: number
  distanceKm: number | null
  calories: number | null
  feeling: number | null
  averageHeartRate: number | null
  maxHeartRate: number | null
  notes: string
}

export interface SportSessionDto {
  id: string
  memberProfileId: string
  date: string
  sport: string
  sportLabel: string
  templateId: string | null
  templateName: string | null
  durationMinutes: number
  distanceKm: number | null
  calories: number | null
  feeling: number | null
  averageHeartRate: number | null
  maxHeartRate: number | null
  notes: string
  source: string
}

export type GoalKind = 'template' | 'sport' | 'sessions' | 'volume'
export type GoalUnit = 'kcal' | 'km' | 'hours'

export interface WeeklyGoalRequest {
  kind: GoalKind
  templateId: string | null
  sport: string | null
  unit: GoalUnit | null
  target: number
}

export interface WeeklyGoalDto {
  id: string
  kind: GoalKind
  kindLabel: string
  templateId: string | null
  templateName: string | null
  sport: string | null
  unit: GoalUnit | null
  target: number
  label: string
  validFrom: string
  validUntil: string | null
}

export interface GoalProgressDto { goal: WeeklyGoalDto, achieved: number, reached: boolean, partial: boolean }

export interface WeekReportDto {
  monday: string
  sunday: string
  sessionCount: number
  durationMinutes: number
  distanceKm: number
  calories: number
  sessionsWithoutCalories: number
  caloriesComplete: boolean
  goalsReached: number
  goals: GoalProgressDto[]
  sessions: SportSessionDto[]
}

export type ChallengeUnit = 'reps' | 'minutes'

export interface DailyChallengeRequest {
  name: string
  unit: ChallengeUnit
  dailyTarget: number
  jokersPerWeek: number
}

export interface DailyChallengeDto extends DailyChallengeRequest {
  id: string
  startedOn: string
  stoppedOn: string | null
  isActive: boolean
}

export type ChallengeDayStatus = 'not_counted' | 'reached' | 'joker' | 'missed' | 'in_progress'

export interface ChallengeDayDto { date: string, total: number, target: number, percent: number, status: ChallengeDayStatus }
export interface ChallengeEntryDto { id: string, date: string, quantity: number, loggedAt: string }

export interface ChallengeReportDto {
  challenge: DailyChallengeDto
  today: string
  todayTotal: number
  todayTarget: number
  reachedToday: boolean
  streak: number
  record: number
  jokersPerWeek: number
  jokersLeftThisWeek: number
  total: number
  totalThisYear: number
  nextMilestone: number | null
  milestones: number[]
  todayEntries: ChallengeEntryDto[]
  calendar: ChallengeDayDto[]
}
