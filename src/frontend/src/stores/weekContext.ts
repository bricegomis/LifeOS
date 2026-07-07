import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import type {
  AlternatingWeekConfig,
  DayContext,
  WeekContext,
  WeekMode,
  WeekModeOverride,
  Weekday,
  WorkLocation,
} from '@/types'

interface StoredWeekContext {
  schemaVersion: 2
  data: WeekContext
}

interface LegacyStoredWeekContext {
  schemaVersion: 1
  data: {
    weekMode?: unknown
    days?: unknown
  }
}

const STORAGE_KEY = 'lifeos.context.v1'
const SCHEMA_VERSION = 2
const DAY_MS = 24 * 60 * 60 * 1000

export const weekdays: Weekday[] = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday',
]

export const weekdayLabels: Record<Weekday, string> = {
  monday: 'Lundi',
  tuesday: 'Mardi',
  wednesday: 'Mercredi',
  thursday: 'Jeudi',
  friday: 'Vendredi',
  saturday: 'Samedi',
  sunday: 'Dimanche',
}

export const weekModeLabels: Record<WeekMode, string> = {
  kids: 'Avec enfants',
  solo: 'Solo',
}

export const workLocationLabels: Record<WorkLocation, string> = {
  home: 'Télétravail',
  office: 'Bureau',
  off: 'Off',
}

export const workLocationShortLabels: Record<WorkLocation, string> = {
  home: 'TT',
  office: 'Bureau',
  off: 'Off',
}

function formatDateString(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')

  return `${year}-${month}-${day}`
}

function parseDateString(value: string): Date | null {
  const date = new Date(`${value}T00:00:00`)

  return Number.isNaN(date.getTime()) ? null : date
}

function isValidDateString(value: unknown): value is string {
  return typeof value === 'string' && parseDateString(value) !== null
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isWeekMode(value: unknown): value is WeekMode {
  return value === 'kids' || value === 'solo'
}

function isWorkLocation(value: unknown): value is WorkLocation {
  return value === 'home' || value === 'office' || value === 'off'
}

export function getCurrentWeekStartDate(reference = new Date()): string {
  const weekStart = new Date(reference.getFullYear(), reference.getMonth(), reference.getDate())
  const dayOfWeek = weekStart.getDay()
  const offset = dayOfWeek === 0 ? -6 : 1 - dayOfWeek

  weekStart.setDate(weekStart.getDate() + offset)

  return formatDateString(weekStart)
}

export function addWeeksToDateString(dateString: string, offsetWeeks: number): string {
  const date = parseDateString(dateString)

  if (!date) {
    return dateString
  }

  date.setDate(date.getDate() + offsetWeeks * 7)

  return formatDateString(date)
}

function differenceInWeeks(leftDateString: string, rightDateString: string): number {
  const leftDate = parseDateString(leftDateString)
  const rightDate = parseDateString(rightDateString)

  if (!leftDate || !rightDate) {
    return 0
  }

  return Math.round((leftDate.getTime() - rightDate.getTime()) / (7 * DAY_MS))
}

export function getWeekRangeLabel(weekStartDate: string): string {
  const weekStart = parseDateString(weekStartDate)

  if (!weekStart) {
    return weekStartDate
  }

  const weekEnd = new Date(weekStart)
  weekEnd.setDate(weekEnd.getDate() + 6)

  const sameYear = weekStart.getFullYear() === weekEnd.getFullYear()
  const sameMonth = sameYear && weekStart.getMonth() === weekEnd.getMonth()
  const monthFormatter = new Intl.DateTimeFormat('fr-FR', { month: 'long' })
  const dayFormatter = new Intl.DateTimeFormat('fr-FR', { day: 'numeric' })
  const startMonth = monthFormatter.format(weekStart)
  const endMonth = monthFormatter.format(weekEnd)
  const startDay = dayFormatter.format(weekStart)
  const endDay = dayFormatter.format(weekEnd)

  if (sameMonth) {
    return `${startDay}–${endDay} ${startMonth}`
  }

  if (sameYear) {
    return `${startDay} ${startMonth}–${endDay} ${endMonth}`
  }

  return `${startDay} ${startMonth} ${weekStart.getFullYear()}–${endDay} ${endMonth} ${weekEnd.getFullYear()}`
}

function createDefaultAlternatingWeekConfig(): AlternatingWeekConfig {
  return {
    referenceWeekStartDate: getCurrentWeekStartDate(),
    referenceWeekMode: 'kids',
  }
}

function createDefaultDayContexts(): Record<Weekday, DayContext> {
  return {
    monday: { workLocation: 'home', bikeCommute: false },
    tuesday: { workLocation: 'office', bikeCommute: true },
    wednesday: { workLocation: 'home', bikeCommute: false },
    thursday: { workLocation: 'home', bikeCommute: false },
    friday: { workLocation: 'home', bikeCommute: false },
    saturday: { workLocation: 'off', bikeCommute: false },
    sunday: { workLocation: 'off', bikeCommute: false },
  }
}

function createDefaultWeekContext(): WeekContext {
  return {
    alternatingWeekConfig: createDefaultAlternatingWeekConfig(),
    weekModeOverrides: [],
    days: createDefaultDayContexts(),
  }
}

const defaultWeekContext = createDefaultWeekContext()

function cloneDayContext(context: DayContext): DayContext {
  return {
    workLocation: context.workLocation,
    bikeCommute: context.bikeCommute,
  }
}

function cloneWeekContext(context: WeekContext): WeekContext {
  return {
    alternatingWeekConfig: {
      referenceWeekStartDate: context.alternatingWeekConfig.referenceWeekStartDate,
      referenceWeekMode: context.alternatingWeekConfig.referenceWeekMode,
    },
    weekModeOverrides: context.weekModeOverrides.map((override) => ({ ...override })),
    days: Object.fromEntries(
      weekdays.map((weekday) => [weekday, cloneDayContext(context.days[weekday])]),
    ) as Record<Weekday, DayContext>,
  }
}

function normalizeDayContext(value: unknown, fallback: DayContext): DayContext {
  if (!isRecord(value)) {
    return cloneDayContext(fallback)
  }

  return {
    workLocation: isWorkLocation(value.workLocation) ? value.workLocation : fallback.workLocation,
    bikeCommute: typeof value.bikeCommute === 'boolean' ? value.bikeCommute : fallback.bikeCommute,
  }
}

function normalizeAlternatingWeekConfig(
  value: unknown,
  fallback: AlternatingWeekConfig,
): AlternatingWeekConfig {
  if (!isRecord(value)) {
    return { ...fallback }
  }

  return {
    referenceWeekStartDate: isValidDateString(value.referenceWeekStartDate)
      ? value.referenceWeekStartDate
      : fallback.referenceWeekStartDate,
    referenceWeekMode: isWeekMode(value.referenceWeekMode)
      ? value.referenceWeekMode
      : fallback.referenceWeekMode,
  }
}

function normalizeWeekModeOverride(value: unknown): WeekModeOverride | null {
  if (!isRecord(value) || !isValidDateString(value.weekStartDate) || !isWeekMode(value.mode)) {
    return null
  }

  return {
    weekStartDate: value.weekStartDate,
    mode: value.mode,
  }
}

function normalizeWeekModeOverrides(value: unknown): WeekModeOverride[] {
  if (!Array.isArray(value)) {
    return []
  }

  const overrides = new Map<string, WeekModeOverride>()

  for (const item of value) {
    const override = normalizeWeekModeOverride(item)

    if (override) {
      overrides.set(override.weekStartDate, override)
    }
  }

  return [...overrides.values()].sort((left, right) => left.weekStartDate.localeCompare(right.weekStartDate))
}

function normalizeWeekContext(value: unknown): WeekContext | null {
  if (!isRecord(value)) {
    return null
  }

  const days = isRecord(value.days) ? value.days : {}
  const fallback = createDefaultWeekContext()

  return {
    alternatingWeekConfig: normalizeAlternatingWeekConfig(
      value.alternatingWeekConfig,
      fallback.alternatingWeekConfig,
    ),
    weekModeOverrides: normalizeWeekModeOverrides(value.weekModeOverrides),
    days: Object.fromEntries(
      weekdays.map((weekday) => [
        weekday,
        normalizeDayContext(days[weekday], fallback.days[weekday]),
      ]),
    ) as Record<Weekday, DayContext>,
  }
}

function normalizeLegacyWeekContext(value: unknown): WeekContext | null {
  if (!isRecord(value) || !isWeekMode(value.weekMode)) {
    return null
  }

  const fallback = createDefaultWeekContext()
  const days = isRecord(value.days) ? value.days : {}

  return {
    alternatingWeekConfig: {
      referenceWeekStartDate: fallback.alternatingWeekConfig.referenceWeekStartDate,
      referenceWeekMode: value.weekMode,
    },
    weekModeOverrides: [],
    days: Object.fromEntries(
      weekdays.map((weekday) => [
        weekday,
        normalizeDayContext(days[weekday], fallback.days[weekday]),
      ]),
    ) as Record<Weekday, DayContext>,
  }
}

function loadWeekContext(): WeekContext {
  if (typeof window === 'undefined') {
    return cloneWeekContext(defaultWeekContext)
  }

  try {
    const rawState = window.localStorage.getItem(STORAGE_KEY)

    if (!rawState) {
      return cloneWeekContext(defaultWeekContext)
    }

    const parsedState = JSON.parse(rawState) as Partial<StoredWeekContext | LegacyStoredWeekContext>

    if (parsedState.schemaVersion === SCHEMA_VERSION && parsedState.data) {
      return cloneWeekContext(normalizeWeekContext(parsedState.data) ?? defaultWeekContext)
    }

    if (parsedState.schemaVersion === 1 && parsedState.data) {
      return cloneWeekContext(normalizeLegacyWeekContext(parsedState.data) ?? defaultWeekContext)
    }

    return cloneWeekContext(defaultWeekContext)
  } catch {
    return cloneWeekContext(defaultWeekContext)
  }
}

function persistWeekContext(context: WeekContext): void {
  if (typeof window === 'undefined') {
    return
  }

  window.localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({
      schemaVersion: SCHEMA_VERSION,
      data: context,
    } satisfies StoredWeekContext),
  )
}

function weekModeOverrideIndex(
  overrides: WeekModeOverride[],
  weekStartDate: string,
): number {
  return overrides.findIndex((override) => override.weekStartDate === weekStartDate)
}

export function getWeekMode(
  weekStartDate: string,
  alternatingWeekConfig: AlternatingWeekConfig,
  overrides: WeekModeOverride[],
): WeekMode {
  const override = overrides.find((item) => item.weekStartDate === weekStartDate)

  if (override) {
    return override.mode
  }

  if (
    !isValidDateString(weekStartDate) ||
    !isValidDateString(alternatingWeekConfig.referenceWeekStartDate)
  ) {
    return alternatingWeekConfig.referenceWeekMode
  }

  const offsetWeeks = differenceInWeeks(weekStartDate, alternatingWeekConfig.referenceWeekStartDate)

  return offsetWeeks % 2 === 0
    ? alternatingWeekConfig.referenceWeekMode
    : alternatingWeekConfig.referenceWeekMode === 'kids'
      ? 'solo'
      : 'kids'
}

export function getWeekModeOverride(
  weekStartDate: string,
  overrides: WeekModeOverride[],
): WeekModeOverride | null {
  return overrides.find((override) => override.weekStartDate === weekStartDate) ?? null
}

export const useWeekContextStore = defineStore('weekContext', () => {
  const weekContext = ref<WeekContext>(loadWeekContext())

  watch(
    weekContext,
    () => {
      persistWeekContext(weekContext.value)
    },
    { deep: true },
  )

  function setReferenceWeekStartDate(referenceWeekStartDate: string): void {
    if (!isValidDateString(referenceWeekStartDate)) {
      return
    }

    weekContext.value.alternatingWeekConfig.referenceWeekStartDate = referenceWeekStartDate
  }

  function setReferenceWeekMode(referenceWeekMode: WeekMode): void {
    weekContext.value.alternatingWeekConfig.referenceWeekMode = referenceWeekMode
  }

  function updateAlternatingWeekConfig(
    alternatingWeekConfig: Partial<AlternatingWeekConfig>,
  ): void {
    if (alternatingWeekConfig.referenceWeekStartDate) {
      setReferenceWeekStartDate(alternatingWeekConfig.referenceWeekStartDate)
    }

    if (alternatingWeekConfig.referenceWeekMode) {
      setReferenceWeekMode(alternatingWeekConfig.referenceWeekMode)
    }
  }

  function upsertWeekModeOverride(
    override: WeekModeOverride,
    originalWeekStartDate: string | null = null,
  ): void {
    if (!isValidDateString(override.weekStartDate) || !isWeekMode(override.mode)) {
      return
    }

    const nextOverrides = [...weekContext.value.weekModeOverrides]

    if (originalWeekStartDate && originalWeekStartDate !== override.weekStartDate) {
      const originalIndex = weekModeOverrideIndex(nextOverrides, originalWeekStartDate)

      if (originalIndex >= 0) {
        nextOverrides.splice(originalIndex, 1)
      }
    }

    const overrideIndex = weekModeOverrideIndex(nextOverrides, override.weekStartDate)

    if (overrideIndex >= 0) {
      nextOverrides.splice(overrideIndex, 1, { ...override })
    } else {
      nextOverrides.push({ ...override })
    }

    weekContext.value.weekModeOverrides = nextOverrides.sort((left, right) =>
      left.weekStartDate.localeCompare(right.weekStartDate),
    )
  }

  function deleteWeekModeOverride(weekStartDate: string): void {
    weekContext.value.weekModeOverrides = weekContext.value.weekModeOverrides.filter(
      (override) => override.weekStartDate !== weekStartDate,
    )
  }

  function updateWorkLocation(weekday: Weekday, workLocation: WorkLocation): void {
    weekContext.value.days[weekday].workLocation = workLocation

    if (workLocation !== 'office') {
      weekContext.value.days[weekday].bikeCommute = false
    }
  }

  function updateBikeCommute(weekday: Weekday, bikeCommute: boolean): void {
    weekContext.value.days[weekday].bikeCommute = bikeCommute
  }

  return {
    weekContext,
    setReferenceWeekStartDate,
    setReferenceWeekMode,
    updateAlternatingWeekConfig,
    upsertWeekModeOverride,
    deleteWeekModeOverride,
    updateWorkLocation,
    updateBikeCommute,
  }
})

export function weekdayForIndex(index: number): Weekday {
  return weekdays[((index % weekdays.length) + weekdays.length) % weekdays.length] ?? 'monday'
}

export function contextForDayIndex(
  context: WeekContext,
  dayIndex: number,
): DayContext {
  return context.days[weekdayForIndex(dayIndex)]
}
