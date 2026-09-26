import { createClient } from '@supabase/supabase-js'
import { baseUrl, supabaseAnonKey, supabaseUrl } from '@/environments/environment'

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
  if (typeof window === 'undefined') return null

  const redirectUrl = new URL(baseUrl, window.location.origin)
  redirectUrl.hash = path.startsWith('/') ? path : `/${path}`
  return redirectUrl.toString()
}
