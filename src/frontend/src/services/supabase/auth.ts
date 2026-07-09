import { shallowRef } from 'vue'
import type { Session, User } from '@supabase/supabase-js'
import { buildSupabaseRedirectUrl, isSupabaseConfigured, supabase } from './client'

export const supabaseSession = shallowRef<Session | null>(null)

let initializePromise: Promise<void> | null = null
let authSubscription: { unsubscribe: () => void } | null = null

export function getCurrentUser(): User | null {
  return supabaseSession.value?.user ?? null
}

export function getCurrentUserId(): string | null {
  return getCurrentUser()?.id ?? null
}

export function isAuthenticated(): boolean {
  return Boolean(getCurrentUserId())
}

export function isSupabaseAuthAvailable(): boolean {
  return Boolean(isSupabaseConfigured && supabase)
}

export async function initializeSupabaseAuth(): Promise<void> {
  if (!supabase) {
    supabaseSession.value = null
    return
  }

  if (initializePromise) {
    return initializePromise
  }

  initializePromise = (async () => {
    const { data, error } = await supabase.auth.getSession()

    if (error) {
      supabaseSession.value = null
    } else {
      supabaseSession.value = data.session ?? null
    }

    if (!authSubscription) {
      authSubscription = supabase.auth.onAuthStateChange((_event, session) => {
        supabaseSession.value = session
      }).data.subscription
    }
  })()

  return initializePromise
}

export async function signInWithMagicLink(
  email: string,
  redirectPath = '/login',
): Promise<{ success: boolean; message: string }> {
  if (!supabase) {
    return {
      success: false,
      message: 'Supabase n’est pas configuré.',
    }
  }

  const { error } = await supabase.auth.signInWithOtp({
    email,
    options: {
      emailRedirectTo: buildSupabaseRedirectUrl(redirectPath) ?? undefined,
    },
  })

  if (error) {
    return {
      success: false,
      message: error.message,
    }
  }

  return {
    success: true,
    message: 'Lien de connexion envoyé.',
  }
}

export async function signOutSupabase(): Promise<{ success: boolean; message: string }> {
  if (!supabase) {
    supabaseSession.value = null

    return {
      success: true,
      message: 'Déconnecté.',
    }
  }

  const { error } = await supabase.auth.signOut()
  supabaseSession.value = null

  if (error) {
    return {
      success: false,
      message: error.message,
    }
  }

  return {
    success: true,
    message: 'Déconnecté.',
  }
}
