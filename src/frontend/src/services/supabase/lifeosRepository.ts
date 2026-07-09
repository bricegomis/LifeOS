import {
  type AlternatingWeekConfig,
  type DayContext,
  type FrequencyRule,
  type PlanningRule,
  type WeekContext,
  type WeekModeOverride,
  type WeekPlan,
  type Weekday,
} from '@/types'
import { isSupabaseConfigured, supabase } from './client'

const weekdays: Weekday[] = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday',
]

interface UserSettingsRow {
  user_id: string
  data: {
    weekContextDays?: Record<Weekday, DayContext>
  } | null
}

interface PlanningRuleRow {
  id: string
  user_id: string
  rule_kind: 'planning' | 'frequency'
  data: unknown
}

interface WeekContextConfigRow {
  user_id: string
  reference_week_start_date: string
  reference_week_mode: WeekContext['alternatingWeekConfig']['referenceWeekMode']
}

interface WeekModeOverrideRow {
  user_id: string
  week_start_date: string
  mode: WeekModeOverride['mode']
}

interface WeekPlanRow {
  user_id: string
  week_start_date: string
  data: WeekPlan
}

export interface RemotePlanningRulesState {
  planningRules: PlanningRule[]
  frequencyRules: FrequencyRule[]
}

export interface RemoteUserSettingsState {
  weekContextDays: Record<Weekday, DayContext>
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isWeekday(value: unknown): value is Weekday {
  return typeof value === 'string' && (weekdays as readonly string[]).includes(value)
}

function isDayContext(value: unknown): value is DayContext {
  return (
    isRecord(value) &&
    (value.workLocation === 'home' || value.workLocation === 'office' || value.workLocation === 'off') &&
    typeof value.bikeCommute === 'boolean'
  )
}

function isWeekContextDays(value: unknown): value is Record<Weekday, DayContext> {
  return (
    isRecord(value) &&
    weekdays.every((weekday) => isDayContext(value[weekday]))
  )
}

function isWeekMode(value: unknown): value is WeekContext['alternatingWeekConfig']['referenceWeekMode'] {
  return value === 'kids' || value === 'solo'
}

function isPlanningRule(value: unknown): value is PlanningRule {
  return (
    isRecord(value) &&
    typeof value.id === 'string' &&
    isWeekday(value.weekday) &&
    (value.mealType === 'breakfast' || value.mealType === 'lunch' || value.mealType === 'dinner') &&
    isRecord(value.target)
  )
}

function isFrequencyRule(value: unknown): value is FrequencyRule {
  return (
    isRecord(value) &&
    typeof value.id === 'string' &&
    typeof value.targetCountPerWeek === 'number' &&
    isRecord(value.target)
  )
}

function isWeekPlan(value: unknown): value is WeekPlan {
  return isRecord(value) && typeof value.id === 'string' && typeof value.startDate === 'string'
}

function clientAvailable() {
  return isSupabaseConfigured && supabase
}

export async function loadUserSettings(userId: string): Promise<RemoteUserSettingsState | null> {
  const client = clientAvailable()

  if (!client) {
    return null
  }

  const { data, error } = await client
    .from('user_settings')
    .select('data')
    .eq('user_id', userId)
    .maybeSingle()

  if (error) {
    throw error
  }

  const row = data as UserSettingsRow | null

  if (!row?.data || !isRecord(row.data) || !isWeekContextDays(row.data.weekContextDays)) {
    return null
  }

  return {
    weekContextDays: row.data.weekContextDays,
  }
}

export async function saveUserSettings(userId: string, state: RemoteUserSettingsState): Promise<void> {
  const client = clientAvailable()

  if (!client) {
    return
  }

  const { error } = await client.from('user_settings').upsert({
    user_id: userId,
    data: {
      weekContextDays: state.weekContextDays,
    },
    updated_at: new Date().toISOString(),
  })

  if (error) {
    throw error
  }
}

export async function loadPlanningRules(userId: string): Promise<RemotePlanningRulesState | null> {
  const client = clientAvailable()

  if (!client) {
    return null
  }

  const { data, error } = await client
    .from('planning_rules')
    .select('id, rule_kind, data')
    .eq('user_id', userId)
    .order('updated_at', { ascending: true })

  if (error) {
    throw error
  }

  const rows = (data ?? []) as PlanningRuleRow[]

  if (!rows.length) {
    return null
  }

  const planningRules = rows
    .filter((row) => row.rule_kind === 'planning' && isPlanningRule(row.data))
    .map((row) => row.data as PlanningRule)

  const frequencyRules = rows
    .filter((row) => row.rule_kind === 'frequency' && isFrequencyRule(row.data))
    .map((row) => row.data as FrequencyRule)

  if (!planningRules.length && !frequencyRules.length) {
    return null
  }

  return {
    planningRules,
    frequencyRules,
  }
}

export async function replacePlanningRules(
  userId: string,
  state: RemotePlanningRulesState,
): Promise<void> {
  const client = clientAvailable()

  if (!client) {
    return
  }

  const { error: deleteError } = await client.from('planning_rules').delete().eq('user_id', userId)

  if (deleteError) {
    throw deleteError
  }

  const rows: PlanningRuleRow[] = [
    ...state.planningRules.map((rule) => ({
      id: rule.id,
      user_id: userId,
      rule_kind: 'planning' as const,
      data: rule,
    })),
    ...state.frequencyRules.map((rule) => ({
      id: rule.id,
      user_id: userId,
      rule_kind: 'frequency' as const,
      data: rule,
    })),
  ]

  if (!rows.length) {
    return
  }

  const { error: insertError } = await client.from('planning_rules').insert(rows)

  if (insertError) {
    throw insertError
  }
}

export async function loadWeekContextConfig(userId: string): Promise<AlternatingWeekConfig | null> {
  const client = clientAvailable()

  if (!client) {
    return null
  }

  const { data, error } = await client
    .from('week_context_config')
    .select('reference_week_start_date, reference_week_mode')
    .eq('user_id', userId)
    .maybeSingle<WeekContextConfigRow>()

  if (error) {
    throw error
  }

  if (!data || !isWeekMode(data.reference_week_mode)) {
    return null
  }

  return {
    referenceWeekStartDate: data.reference_week_start_date,
    referenceWeekMode: data.reference_week_mode,
  }
}

export async function saveWeekContextConfig(
  userId: string,
  alternatingWeekConfig: AlternatingWeekConfig,
): Promise<void> {
  const client = clientAvailable()

  if (!client) {
    return
  }

  const { error } = await client.from('week_context_config').upsert({
    user_id: userId,
    reference_week_start_date: alternatingWeekConfig.referenceWeekStartDate,
    reference_week_mode: alternatingWeekConfig.referenceWeekMode,
    updated_at: new Date().toISOString(),
  })

  if (error) {
    throw error
  }
}

export async function loadWeekModeOverrides(userId: string): Promise<WeekModeOverride[] | null> {
  const client = clientAvailable()

  if (!client) {
    return null
  }

  const { data, error } = await client
    .from('week_mode_overrides')
    .select('week_start_date, mode')
    .eq('user_id', userId)
    .order('week_start_date', { ascending: true })

  if (error) {
    throw error
  }

  const rows = (data ?? []) as WeekModeOverrideRow[]

  if (!rows.length) {
    return null
  }

  return rows
    .filter((row) => isWeekMode(row.mode))
    .map((row) => ({
      weekStartDate: row.week_start_date,
      mode: row.mode,
    }))
}

export async function replaceWeekModeOverrides(
  userId: string,
  overrides: WeekModeOverride[],
): Promise<void> {
  const client = clientAvailable()

  if (!client) {
    return
  }

  const { error: deleteError } = await client.from('week_mode_overrides').delete().eq('user_id', userId)

  if (deleteError) {
    throw deleteError
  }

  if (!overrides.length) {
    return
  }

  const { error: insertError } = await client.from('week_mode_overrides').insert(
    overrides.map((override) => ({
      user_id: userId,
      week_start_date: override.weekStartDate,
      mode: override.mode,
    })),
  )

  if (insertError) {
    throw insertError
  }
}

export async function loadWeekPlan(
  userId: string,
  weekStartDate: string,
): Promise<WeekPlan | null> {
  const client = clientAvailable()

  if (!client) {
    return null
  }

  const { data, error } = await client
    .from('week_plans')
    .select('data')
    .eq('user_id', userId)
    .eq('week_start_date', weekStartDate)
    .maybeSingle()

  if (error) {
    throw error
  }

  const row = data as WeekPlanRow | null

  if (!row?.data || !isWeekPlan(row.data)) {
    return null
  }

  return row.data
}

export async function listWeekPlans(userId: string): Promise<WeekPlan[]> {
  const client = clientAvailable()

  if (!client) {
    return []
  }

  const { data, error } = await client
    .from('week_plans')
    .select('data')
    .eq('user_id', userId)
    .order('week_start_date', { ascending: false })

  if (error) {
    throw error
  }

  return ((data ?? []) as WeekPlanRow[])
    .map((row) => (isWeekPlan(row.data) ? row.data : null))
    .filter((weekPlan): weekPlan is WeekPlan => Boolean(weekPlan))
}

export async function upsertWeekPlan(userId: string, weekPlan: WeekPlan): Promise<void> {
  const client = clientAvailable()

  if (!client) {
    return
  }

  const { error } = await client.from('week_plans').upsert(
    {
      user_id: userId,
      week_start_date: weekPlan.startDate,
      data: weekPlan,
      updated_at: new Date().toISOString(),
    },
    {
      onConflict: 'user_id,week_start_date',
    },
  )

  if (error) {
    throw error
  }
}
