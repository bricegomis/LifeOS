import { createClient } from '@supabase/supabase-js'
import { Capacitor } from '@capacitor/core'
import { baseUrl, supabaseAnonKey, supabaseUrl } from '@/environments/environment'
import {
  readMagicLinkCallback,
  removeMagicLinkParams,
  type MagicLinkCallback,
} from '@/app/core/auth/magic-link-callback'
import { validatedReturnPath } from '@/app/core/auth/auth-flow'

export const isSupabaseConfigured = Boolean(supabaseUrl && supabaseAnonKey)
export const NATIVE_AUTH_CALLBACK_URL = 'lifeos://auth/callback'

// Captured before supabase-js and the router read the URL. Error / token_hash params are
// removed right away so the hash router lands on a valid route. AuthService explicitly
// exchanges PKCE codes so SDK initialization cannot hide callback failures.
export const initialMagicLinkCallback: MagicLinkCallback =
  typeof window === 'undefined' ? { kind: 'none' } : readMagicLinkCallback(window.location.href)

if (typeof window !== 'undefined' && initialMagicLinkCallback.kind !== 'none') {
  window.history.replaceState(window.history.state, '', removeMagicLinkParams(window.location.href, { keepCode: false }))
}

export const supabase = isSupabaseConfigured
  ? createClient(supabaseUrl, supabaseAnonKey, {
      auth: {
        autoRefreshToken: true,
        detectSessionInUrl: initialMagicLinkCallback.kind !== 'pkce-code',
        flowType: 'pkce',
        persistSession: true,
        storageKey: 'lifeos.supabase.auth',
      },
    })
  : null

export function buildNativeSupabaseRedirectUrl(path = '/login'): string {
  const safePath = validatedReturnPath(path)
  return `${NATIVE_AUTH_CALLBACK_URL}?redirect=${encodeURIComponent(safePath)}`
}

export function buildSupabaseRedirectUrl(path = '/login'): string | null {
  if (typeof window === 'undefined') return null

  if (Capacitor.isNativePlatform()) {
    return buildNativeSupabaseRedirectUrl(path)
  }

  const redirectUrl = new URL(baseUrl, window.location.origin)
  redirectUrl.hash = path.startsWith('/') ? path : `/${path}`
  return redirectUrl.toString()
}
