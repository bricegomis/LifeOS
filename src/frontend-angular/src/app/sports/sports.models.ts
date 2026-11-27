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

/** The five feelings asked by the watch at the end of a session (ADR 0019). */
export type SessionFeel = 'very_weak' | 'weak' | 'normal' | 'strong' | 'very_strong'

export interface SportSessionRequest {
  memberProfileId?: string | null
  /** Paris day of the start. */
  date: string
  /** Paris times, HH:mm; an end before the start is on the next day. */
  startTime: string
  endTime: string
  sport: string
  templateId: string | null
  /** Active time; end minus start when null. */
  durationMinutes: number | null
  distanceKm: number | null
  calories: number | null
  /** 1 (very easy) to 10 (maximal). */
  effort: number | null
  feel: SessionFeel | null
  averageHeartRate: number | null
  maxHeartRate: number | null
  notes: string
}

export interface SportSessionDto {
  id: string
  memberProfileId: string
  date: string
  startedAt: string
  endedAt: string
  sport: string
  sportLabel: string
  templateId: string | null
  templateName: string | null
  durationMinutes: number
  distanceKm: number | null
  calories: number | null
  effort: number | null
  feel: SessionFeel | null
  feelLabel: string | null
  averageHeartRate: number | null
  maxHeartRate: number | null
  notes: string
  source: 'manual' | 'garmin'
  elevationGainMeters: number | null
  averageSpeedKmh: number | null
  aerobicTrainingEffect: number | null
  anaerobicTrainingEffect: number | null
  /** Seconds in heart rate zones 1 to 5; Garmin only. */
  heartRateZoneSeconds: number[] | null
  garminActivityId: number | null
  garminType: string | null
  garminName: string | null
  sportOverridden: boolean
}

/** VO2max estimated by the watch on one day (ADR 0019). */
export interface Vo2MaxDto { date: string, sport: 'running' | 'cycling', sportLabel: string, value: number }

export type GarminState = 'never' | 'ok' | 'mfa_requested' | 'code_refused' | 'code_expired' | 'credentials_refused' | 'error'

/** The Garmin connection of the account, reported by the garmin-sync service. */
export interface GarminStatusDto {
  state: GarminState
  stateLabel: string
  message: string | null
  waitsForCode: boolean
  lastSeenAt: string | null
  lastSuccessAt: string | null
  lastSuccessSummary: string | null
  lastErrorAt: string | null
  lastError: string | null
  nextSyncAt: string | null
  syncRequested: boolean
  reconnectRequested: boolean
  codePending: boolean
}

export interface ImportTokenDto { id: string, name: string, hint: string, memberProfileId: string, createdAt: string, lastUsedAt: string | null }
/** The secret is shown once. */
export interface CreatedImportTokenDto { info: ImportTokenDto, token: string }

export type GoalKind = 'template' | 'sport' | 'sessions' | 'volume'
export type GoalUnit = 'kcal' | 'km' | 'hours'

export interface WeeklyGoalRequest {
  kind: GoalKind
  templateId: string | null
  sport: string | null
  unit: GoalUnit | null
  target: number
  /** Sports left out of a global goal (sessions or volume). */
  excludedSports?: string[]
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
  excludedSports: string[]
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

/** Routines, their passes and exercises (ADR 0014, ADR 0015). */
export type ExerciseMeasure = 'hold' | 'reps'

export interface ExerciseRequest {
  name: string
  instructions: string | null
  measure: ExerciseMeasure
  /** Usual load in kg, offered as the target in a routine; null means bodyweight. */
  defaultLoadKg: number | null
  /** Done on the left then on the right: one set covers both sides, its target applies to each side. */
  perSide: boolean
}

export interface ExerciseDto extends ExerciseRequest {
  id: string
  hasImage: boolean
  isArchived: boolean
  /** Already done: the measure no longer changes. */
  measureLocked: boolean
}

/** A load of null (or 0) means bodyweight. */
export interface RoutineLineRequest { exerciseId: string, sets: number, target: number, loadKg: number | null }

export interface RoutineRequest {
  name: string
  /** 1 (Monday) to 7 (Sunday). */
  days: number[]
  jokersPerWeek: number
  lines: RoutineLineRequest[]
}

export interface RoutineLineDto extends RoutineLineRequest {
  exerciseName: string
  instructions: string | null
  measure: ExerciseMeasure
  hasImage: boolean
  /** Left then right; the target applies to each side. */
  perSide: boolean
}

export interface RoutineDto {
  id: string
  name: string
  days: number[]
  jokersPerWeek: number
  lines: RoutineLineDto[]
  startedOn: string
}

/** An exercise done alone, outside any routine; the usual load when loadKg is null, 0 = bodyweight. */
export interface ExerciseLogRequest { setsDone: number, value: number, loadKg: number | null, date: string | null }

export interface ExerciseCompletionDto {
  id: string
  /** The pass it was ticked in; null when done alone, or once its routine was deleted. */
  sessionId: string | null
  exerciseId: string
  date: string
  setsDone: number
  value: number
  loadKg: number | null
  loggedAt: string
}

/** One pass of a routine: finished when every exercise has all its sets. */
export interface RoutineSessionDto {
  id: string
  date: string
  startedAt: string
  finishedAt: string | null
  finished: boolean
  exercisesDone: number
  exercisesTotal: number
  /** Free note on the pass; null when none. */
  comment: string | null
  completions: ExerciseCompletionDto[]
}

export type RoutineDayStatus = 'not_counted' | 'not_planned' | 'done' | 'joker' | 'missed' | 'in_progress'
export interface RoutineDayDto { date: string, done: number, planned: number, percent: number, status: RoutineDayStatus, passes: number }

export interface RoutineReportDto {
  routine: RoutineDto
  today: string
  plannedToday: boolean
  /** Exercises done in today's best pass. */
  doneToday: number
  totalToday: number
  completedToday: boolean
  passesToday: number
  nextPlannedDay: string | null
  streak: number
  record: number
  jokersPerWeek: number
  jokersLeftThisWeek: number
  /** Today's passes, oldest first; at most one unfinished. */
  todaySessions: RoutineSessionDto[]
  calendar: RoutineDayDto[]
}

export interface ExerciseWeekDto { monday: string, bestValue: number | null, bestLoadKg: number | null }
export interface ExerciseProgressDto {
  exerciseId: string
  bestValue: number | null
  bestLoadKg: number | null
  daysDone: number
  weeks: ExerciseWeekDto[]
  /** Latest ticks first. */
  recent: ExerciseCompletionDto[]
}

