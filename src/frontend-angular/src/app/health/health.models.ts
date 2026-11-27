/** API models of the Health module (ADR 0018). Dates are ISO days (yyyy-MM-dd). */
export type SupplementUnit = 'gelule' | 'comprime' | 'g' | 'ml' | 'goutte' | 'dose'
export type SupplementMoment = 'matin' | 'midi' | 'soir' | 'choix'
export type PeriodUnit = 'week' | 'month'

export interface SupplementLinkDto { label: string, url: string }
export interface SupplementPeriodDto { kind: 'year' | 'cure', startOn: string, cureLength: number, cureUnit: PeriodUnit, pauseLength: number, pauseUnit: PeriodUnit }
export interface SupplementStatusDto { active: boolean, dayInCure: number | null, cureDays: number | null, daysLeft: number | null, nextCureOn: string | null, ended: boolean }
export interface SupplementDayDto { day: string, taken: boolean, active: boolean }

export interface SupplementDto {
  id: string
  name: string
  notes: string | null
  dose: number
  unit: SupplementUnit
  moment: SupplementMoment
  packageContent: number
  packagePrice: number | null
  shop: string | null
  links: SupplementLinkDto[]
  period: SupplementPeriodDto
  isStopped: boolean
  hasImage: boolean
  updatedAt: string
  /** price × dose × 30 / contents, for the share of days in cure; null without a price. */
  monthlyCost: number | null
  status: SupplementStatusDto
  takenToday: boolean
  streak: number
  /** The last 14 days, oldest first. */
  recentDays: SupplementDayDto[]
}

export interface SupplementListDto { today: string, monthlyTotal: number, supplements: SupplementDto[] }

export interface SupplementRequest {
  name: string
  notes: string | null
  dose: number
  unit: SupplementUnit
  moment: SupplementMoment
  packageContent: number
  packagePrice: number | null
  shop: string | null
  links: { label: string | null, url: string }[]
  period: { kind: 'year' | 'cure', startOn: string | null, cureLength: number | null, cureUnit: PeriodUnit | null, pauseLength: number | null, pauseUnit: PeriodUnit | null }
}

export type SymptomKind = 'physique' | 'mental'
export interface SymptomTypeDto { id: string, name: string, kind: SymptomKind, isArchived: boolean }
/** `time` is "HH:mm:ss" when given. */
export interface SymptomEntryDto { id: string, symptomTypeId: string, symptomName: string, kind: SymptomKind, day: string, time: string | null, intensity: number, note: string | null }
export interface SymptomDayDto { day: string, calm: boolean, maxIntensity: number | null, entries: SymptomEntryDto[] }
/** Days from `from` to `today`, newest first, only those with something noted. */
export interface SymptomJournalDto { today: string, from: string, days: SymptomDayDto[], types: SymptomTypeDto[] }
export interface SymptomEntryRequest { symptomTypeId: string, day: string | null, time: string | null, intensity: number, note: string | null }
export interface CheckinRequest { day: string | null, calm: boolean, symptoms: { symptomTypeId: string, intensity: number }[] }

/** in_lab: in the lab range while no personal target is set. */
export type MarkerStatus = 'below_lab' | 'above_lab' | 'in_target' | 'normal' | 'in_lab'
export type MarkerTrend = 'better' | 'worse' | 'stable' | 'in_target' | 'in_lab'
export interface MarkerValueDto { analysisId: string, day: string, value: number, status: MarkerStatus }
export interface MarkerDto {
  id: string
  name: string
  unit: string
  labMin: number | null
  labMax: number | null
  targetMin: number | null
  targetMax: number | null
  /** 0: once. */
  frequencyMonths: number
  panels: string[]
  isArchived: boolean
  /** Oldest first. */
  values: MarkerValueDto[]
  status: MarkerStatus | null
  trend: MarkerTrend | null
  nextDueOn: string | null
  due: boolean
  late: boolean
  /** What it measures and what it is used for. */
  description: string | null
  /** What a value below the range can mean. */
  whenLow: string | null
  /** What a value above the range can mean. */
  whenHigh: string | null
}
export interface MarkerListDto { today: string, markers: MarkerDto[], panels: string[] }
export interface MarkerRequest {
  name: string, unit: string, labMin: number | null, labMax: number | null, targetMin: number | null, targetMax: number | null, frequencyMonths: number, panels: string[],
  description: string | null, whenLow: string | null, whenHigh: string | null
}
export interface AnalysisValueDto { markerId: string, markerName: string, unit: string, value: number, status: MarkerStatus }
export interface AnalysisDto { id: string, day: string, laboratory: string | null, notes: string | null, documentId: string | null, values: AnalysisValueDto[] }
export interface AnalysisRequest { day: string | null, laboratory: string | null, notes: string | null, documentId: string | null, values: { markerId: string, value: number }[] }
export type MeasureStatus = 'below_target' | 'in_target' | 'above_target'
export interface MeasurementDto { id: string, day: string, value: number, note: string | null, status: MeasureStatus | null }
/** A kind of body measure with an optional personal target; status is the last value's, null without a target. */
export interface MeasureTypeDto {
  id: string, name: string, unit: string, targetMin: number | null, targetMax: number | null, isArchived: boolean, values: MeasurementDto[], status: MeasureStatus | null
}
export interface MeasureTypeRequest { name: string, unit: string, targetMin: number | null, targetMax: number | null }

export type HealthDocumentType = 'ordonnance' | 'compte_rendu' | 'resultat' | 'bilan' | 'autre'
export interface HealthDocumentFileDto { id: string, contentType: string, size: number, originalName: string | null }
export interface HealthDocumentDto {
  id: string, type: HealthDocumentType, title: string, day: string, who: string | null, notes: string | null, files: HealthDocumentFileDto[], analysisIds: string[]
}
export interface HealthDocumentRequest { type: HealthDocumentType, title: string, day: string | null, who: string | null, notes: string | null }
/** `time` is "HH:mm:ss" when given. */
export interface AppointmentDto {
  id: string, day: string, time: string | null, who: string, reason: string | null, place: string | null, notes: string | null, bookingUrl: string | null,
  reportDocumentId: string | null, reportTitle: string | null
}
export interface AppointmentListDto { today: string, upcoming: AppointmentDto[], past: AppointmentDto[] }
export interface AppointmentRequest {
  day: string, time: string | null, who: string, reason: string | null, place: string | null, notes: string | null, bookingUrl: string | null, reportDocumentId: string | null
}

/** One day of sleep and recovery imported from Garmin (ADR 0019); private like all health data. */
export interface WellnessDto {
  date: string
  sleepStart: string | null
  sleepEnd: string | null
  sleepSeconds: number | null
  deepSleepSeconds: number | null
  lightSleepSeconds: number | null
  remSleepSeconds: number | null
  awakeSeconds: number | null
  sleepScore: number | null
  sleepQuality: string | null
  restingHeartRate: number | null
  hrvNightAverage: number | null
  hrvWeeklyAverage: number | null
  hrvBaselineLow: number | null
  hrvBaselineHigh: number | null
  hrvStatus: string | null
  hrvStatusLabel: string | null
  respiration: number | null
  spO2: number | null
  bodyBatteryWake: number | null
  bodyBatteryHigh: number | null
  bodyBatteryLow: number | null
  stressAverage: number | null
  steps: number | null
  updatedAt: string
}
