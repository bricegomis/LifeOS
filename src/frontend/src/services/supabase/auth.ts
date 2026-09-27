import { shallowRef } from 'vue'
import type { Session, User } from '@supabase/supabase-js'
import { buildSupabaseRedirectUrl, initialMagicLinkCallback, isSupabaseConfigured, supabase } from './client'
import { removeMagicLinkParams } from './magicLinkCallback'

export const supabaseSession = shallowRef<Session | null>(null)

let initializePromise: Promise<string | null> | null = null
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

function describeMagicLinkError(errorCode: string | null, description: string | null): string {
  if (errorCode === 'otp_expired') {
    return 'Ce lien de connexion a expiré ou a déjà été utilisé. Demande un nouveau lien.'
  }

  return description
    ? `Connexion par lien magique impossible : ${description}`
    : 'Connexion par lien magique impossible. Demande un nouveau lien.'
}

const PKCE_SESSION_MISSING_MESSAGE =
  'Connexion impossible avec ce lien : ouvre-le dans le même navigateur que celui utilisé pour le demander, ou demande un nouveau lien.'

/**
 * Restores the Supabase session and consumes the magic-link callback present in the URL.
 * Returns a user-facing message when the callback could not be turned into a session.
 */
export async function initializeSupabaseAuth(): Promise<string | null> {
  if (!supabase) {
    supabaseSession.value = null
    return null
  }

  if (initializePromise) {
    return initializePromise
  }

  initializePromise = (async () => {
    const client = supabase
    const callback = initialMagicLinkCallback
    let callbackError: string | null = null

    // Waits for supabase-js to finish its own URL detection (PKCE code exchange).
    let { data, error } = await client.auth.getSession()

    if (callback.kind === 'token-hash' && !data.session) {
      const result = await client.auth.verifyOtp({ token_hash: callback.tokenHash, type: callback.otpType })

      if (result.error) {
        callbackError = describeMagicLinkError(result.error.code ?? null, result.error.message)
      }

      ;({ data, error } = await client.auth.getSession())
    }

    const session = error ? null : (data.session ?? null)
    supabaseSession.value = session

    if (callback.kind === 'error') {
      callbackError = describeMagicLinkError(callback.errorCode, callback.description)
    } else if (callback.kind === 'pkce-code') {
      if (!session) {
        // supabase-js only exchanges the code when the code verifier created by
        // signInWithOtp exists in this browser's storage.
        callbackError = PKCE_SESSION_MISSING_MESSAGE
      }

      window.history.replaceState(window.history.state, '', removeMagicLinkParams(window.location.href, { keepCode: false }))
    }

    if (!authSubscription) {
      authSubscription = client.auth.onAuthStateChange((_event, nextSession) => {
        supabaseSession.value = nextSession
      }).data.subscription
    }

    return callbackError
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
