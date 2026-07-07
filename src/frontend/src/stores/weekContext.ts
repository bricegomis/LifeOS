import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import type { DayContext, WeekContext, WeekMode, Weekday, WorkLocation } from '@/types'

interface StoredWeekContext {
  schemaVersion: 1
  data: WeekContext
}

const STORAGE_KEY = 'lifeos.context.v1'
const SCHEMA_VERSION = 1

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

const defaultWeekContext: WeekContext = {
  weekMode: 'kids',
  days: {
    monday: { workLocation: 'home', bikeCommute: false },
    tuesday: { workLocation: 'office', bikeCommute: true },
    wednesday: { workLocation: 'home', bikeCommute: false },
    thursday: { workLocation: 'home', bikeCommute: false },
    friday: { workLocation: 'home', bikeCommute: false },
    saturday: { workLocation: 'off', bikeCommute: false },
    sunday: { workLocation: 'off', bikeCommute: false },
  },
}

function cloneWeekContext(context: WeekContext): WeekContext {
  return {
    weekMode: context.weekMode,
    days: Object.fromEntries(
      weekdays.map((weekday) => [weekday, { ...context.days[weekday] }]),
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

    const parsedState = JSON.parse(rawState) as Partial<StoredWeekContext>

    if (parsedState.schemaVersion !== SCHEMA_VERSION || !parsedState.data) {
      return cloneWeekContext(defaultWeekContext)
    }

    return {
      weekMode: parsedState.data.weekMode ?? defaultWeekContext.weekMode,
      days: {
        ...cloneWeekContext(defaultWeekContext).days,
        ...parsedState.data.days,
      },
    }
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
    }),
  )
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

  function updateWeekMode(weekMode: WeekMode): void {
    weekContext.value.weekMode = weekMode
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
    updateWeekMode,
    updateWorkLocation,
    updateBikeCommute,
  }
})
