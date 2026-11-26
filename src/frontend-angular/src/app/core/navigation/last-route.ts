import { validatedReturnPath } from '../auth/auth-flow.ts'

export const LAST_ROUTE_KEY = 'lifeos.navigation.last-route'
/** Past this delay, reopening the app starts again from the home page. */
export const LAST_ROUTE_MAX_AGE_MS = 12 * 60 * 60 * 1000

interface StoredRoute {
  url: string
  at: number
}

/** The app was opened on its start address (home screen icon, iPhone app), not on a given page. */
export function isLaunchHash(hash: string): boolean {
  return hash === '' || hash === '#' || hash === '#/'
}

/** What to keep for a visited page, or null when it is not worth coming back to. */
export function serializeLastRoute(url: string, now: number): string | null {
  const safe = validatedReturnPath(url)
  if (safe === '/' || safe !== url) return null
  return JSON.stringify({ url: safe, at: now } satisfies StoredRoute)
}

/** The page to reopen, if it was visited recently enough and is still a known internal route. */
export function restorableRoute(raw: string | null, now: number, maxAgeMs = LAST_ROUTE_MAX_AGE_MS): string | null {
  if (!raw) return null
  let stored: unknown
  try { stored = JSON.parse(raw) } catch { return null }
  if (!stored || typeof stored !== 'object') return null
  const { url, at } = stored as Partial<StoredRoute>
  if (typeof url !== 'string' || typeof at !== 'number' || !Number.isFinite(at)) return null
  if (at > now || now - at > maxAgeMs) return null
  const safe = validatedReturnPath(url)
  return safe === url && safe !== '/' ? safe : null
}
