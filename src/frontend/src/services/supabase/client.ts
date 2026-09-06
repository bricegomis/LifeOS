import { createClient } from '@supabase/supabase-js'

const supabaseUrl = import.meta.env.VITE_SUPABASE_URL?.trim() ?? ''
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY?.trim() ?? ''

export const isSupabaseConfigured = Boolean(supabaseUrl && supabaseAnonKey)

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
  if (typeof window === 'undefined') {
    return null
  }

  const redirectUrl = new URL(import.meta.env.BASE_URL, window.location.origin)
  redirectUrl.hash = path.startsWith('/') ? path : `/${path}`

  return redirectUrl.toString()
}
