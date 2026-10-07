import type { SupabaseClient } from '@supabase/supabase-js'
import type { MagicLinkCallback } from './magic-link-callback'

type AuthClient = Pick<SupabaseClient['auth'], 'getSession' | 'exchangeCodeForSession' | 'verifyOtp' | 'signInWithOAuth'>

const INTERNAL_ROUTE = /^\/(?:planning|recipes|meals|foods|sports|stores|articles|stock|library|settings|today|planner)?(?:\?[^#\\\s]*)?$/

export function validatedReturnPath(value: string | null): string {
  return value && INTERNAL_ROUTE.test(value) ? value : '/'
}

export function buildLoginRedirectPath(returnPath: string): string {
  return `/login?redirect=${encodeURIComponent(validatedReturnPath(returnPath))}`
}

export async function startGoogleSignIn(auth: AuthClient, redirectTo: string): Promise<void> {
  const { data, error } = await auth.signInWithOAuth({
    provider: 'google',
    options: { redirectTo, skipBrowserRedirect: false },
  })
  if (error) throw error
  if (!data.url) throw new Error('Google n’a pas fourni de page de connexion. Réessayez ou utilisez le lien par e-mail.')
}

function callbackErrorMessage(code: string | null, description: string | null): string {
  if (code === 'otp_expired') {
    return 'Ce lien de connexion a expiré ou a déjà été utilisé. Demandez un nouveau lien.'
  }
  if (code === 'access_denied') {
    return 'La connexion a été annulée ou refusée. Réessayez avec Google ou utilisez le lien par e-mail.'
  }
  return description
    ? `Connexion impossible : ${description}`
    : 'Connexion impossible. Réessayez avec Google ou demandez un nouveau lien par e-mail.'
}

export async function restoreAuthSession(auth: AuthClient, callback: MagicLinkCallback) {
  if (callback.kind === 'error') {
    throw new Error(callbackErrorMessage(callback.errorCode, callback.description))
  }

  if (callback.kind === 'pkce-code') {
    const { data, error } = await auth.exchangeCodeForSession(callback.code)
    if (error) {
      throw new Error(`Impossible de terminer la connexion : ${error.message}. Réessayez depuis ce navigateur, sans fermer la fenêtre privée.`)
    }
    if (!data.session) {
      throw new Error('Aucune session reçue. Relancez Google dans cette fenêtre ou ouvrez le lien e-mail dans le navigateur utilisé pour le demander.')
    }
    return data.session
  }

  const { data, error } = await auth.getSession()
  if (error) throw error
  if (callback.kind === 'token-hash' && !data.session) {
    const result = await auth.verifyOtp({ token_hash: callback.tokenHash, type: callback.otpType })
    if (result.error) throw new Error(callbackErrorMessage(result.error.code ?? null, result.error.message))
    if (!result.data.session) throw new Error('Le lien n’a pas créé de session. Demandez un nouveau lien.')
    return result.data.session
  }
  return data.session
}
