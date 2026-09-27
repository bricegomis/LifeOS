import { createClient } from '@supabase/supabase-js'
import { baseUrl, supabaseAnonKey, supabaseUrl } from '@/environments/environment'
import {
  readMagicLinkCallback,
  removeMagicLinkParams,
  type MagicLinkCallback,
} from '@/app/core/auth/magic-link-callback'

export const isSupabaseConfigured = Boolean(supabaseUrl && supabaseAnonKey)

// Captured before supabase-js and the router read the URL. Error / token_hash params are
// removed right away so the hash router lands on a valid route; the PKCE `code` is left for
// supabase-js, which exchanges it (and removes it) during its own initialization.
export const initialMagicLinkCallback: MagicLinkCallback =
  typeof window === 'undefined' ? { kind: 'none' } : readMagicLinkCallback(window.location.href)

if (typeof window !== 'undefined' && initialMagicLinkCallback.kind !== 'none' && initialMagicLinkCallback.kind !== 'pkce-code') {
  window.history.replaceState(window.history.state, '', removeMagicLinkParams(window.location.href, { keepCode: true }))
}

export const supabase = isSupabaseConfigured
  ? createClient(supabaseUrl, supabaseAnonKey, {
      auth: {
        autoRefreshToken: true,
        detectSessionInUrl: true,
        flowType: 'pkce',
        persistSession: true,
        storageKey: 'lifeos.supabase.auth',
      },
    })
  : null

export function buildSupabaseRedirectUrl(path = '/login'): string | null {
  if (typeof window === 'undefined') return null

  const redirectUrl = new URL(baseUrl, window.location.origin)
  redirectUrl.hash = path.startsWith('/') ? path : `/${path}`
  return redirectUrl.toString()
}
