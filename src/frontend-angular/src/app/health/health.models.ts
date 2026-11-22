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
