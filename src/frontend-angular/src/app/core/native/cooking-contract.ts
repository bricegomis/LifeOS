import type { CookingSessionState } from './native.models'

export const COOKING_MAX_AGE_MS = 8 * 60 * 60 * 1000
export const COOKING_STORAGE_KEY = 'lifeos.native.cookingSession'

export function readCookingSession(value: unknown, now = Date.now()): CookingSessionState | null {
  if (value === null) return null
  if (typeof value !== 'object' || !value) throw new Error('Session cuisine invalide')
  const s = value as Record<string, unknown>
  if (s['version'] !== undefined && s['version'] !== 2) throw new Error('Version cuisine non prise en charge')
  const startedAt = s['startedAt'] ?? s['startedAtIso']
  const updatedAt = s['updatedAt'] ?? startedAt
  const revision = s['revision'] ?? 0
  if (typeof s['sessionId'] !== 'string' || !s['sessionId']
    || typeof s['recipeTitle'] !== 'string'
    || (s['recipeId'] != null && typeof s['recipeId'] !== 'string')
    || !Array.isArray(s['steps']) || !s['steps'].every(x => typeof x === 'string')
    || !Array.isArray(s['completedStepIndices'])
    || !s['completedStepIndices'].every(x => Number.isInteger(x))
    || typeof startedAt !== 'string' || !Number.isFinite(Date.parse(startedAt))
    || typeof updatedAt !== 'string' || !Number.isFinite(Date.parse(updatedAt))
    || typeof revision !== 'number' || !Number.isSafeInteger(revision) || revision < 0 || revision >= Number.MAX_SAFE_INTEGER) {
    throw new Error('Session cuisine invalide')
  }
  const age = now - Date.parse(startedAt)
  if (age < 0 || age >= COOKING_MAX_AGE_MS) return null
  const steps = s['steps'] as string[]
  const completedStepIndices = [...new Set(s['completedStepIndices'] as number[])]
    .filter(i => i >= 0 && i < steps.length).sort((a, b) => a - b)
  return {
    version: 2, revision, sessionId: s['sessionId'], recipeId: s['recipeId'] as string | null ?? null,
    recipeTitle: s['recipeTitle'], startedAt, updatedAt, steps, completedStepIndices,
    isCompleted: steps.length > 0 && completedStepIndices.length === steps.length,
  }
}

export function mutateCookingSession(
  session: CookingSessionState | null,
  sessionId: string,
  operation: 'toggle' | 'complete',
  stepIndex?: number,
  now = Date.now(),
): CookingSessionState {
  const current = readCookingSession(session, now)
  if (!current || current.sessionId !== sessionId) throw new Error('Session cuisine expirée ou remplacée')
  if (current.revision >= Number.MAX_SAFE_INTEGER - 1) throw new Error('Révision cuisine maximale atteinte')
  const completed = new Set(current.completedStepIndices)
  if (operation === 'toggle') {
    if (stepIndex === undefined || !Number.isInteger(stepIndex) || stepIndex < 0 || stepIndex >= current.steps.length) {
      throw new Error('Index cuisine invalide')
    }
    if (completed.has(stepIndex)) completed.delete(stepIndex)
    else completed.add(stepIndex)
  } else {
    current.steps.forEach((_, i) => completed.add(i))
  }
  return {
    ...current, revision: current.revision + 1, updatedAt: new Date(now).toISOString(),
    completedStepIndices: [...completed].sort((a, b) => a - b),
    isCompleted: current.steps.length > 0 && completed.size === current.steps.length,
  }
}
