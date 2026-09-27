import { computed, Injectable, signal } from '@angular/core'
import type { Session, User } from '@supabase/supabase-js'
import { removeMagicLinkParams } from '@/app/core/auth/magic-link-callback'
import {
  buildSupabaseRedirectUrl,
  initialMagicLinkCallback,
  isSupabaseConfigured,
  supabase,
} from '@/app/core/auth/supabase-client'

function describeMagicLinkError(errorCode: string | null, description: string | null): string {
  if (errorCode === 'otp_expired') {
    return 'Ce lien de connexion a expiré ou a déjà été utilisé. Demandez un nouveau lien.'
  }
  return description
    ? `Connexion par lien magique impossible : ${description}`
    : 'Connexion par lien magique impossible. Demandez un nouveau lien.'
}

const PKCE_SESSION_MISSING_MESSAGE =
  'Connexion impossible avec ce lien : ouvrez-le dans le même navigateur que celui utilisé pour le demander, ou demandez un nouveau lien.'

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly session = signal<Session | null>(null)
  readonly ready = signal(false)
  readonly initializing = signal(false)
  readonly error = signal<string | null>(null)
  readonly user = computed<User | null>(() => this.session()?.user ?? null)
  readonly authenticated = computed(() => Boolean(this.session()?.access_token))
  readonly configured = isSupabaseConfigured

  private initializePromise: Promise<void> | null = null
  private subscription: { unsubscribe: () => void } | null = null

  initialize(): Promise<void> {
    if (this.initializePromise) return this.initializePromise

    this.initializing.set(true)
    this.initializePromise = (async () => {
      try {
        if (!supabase) {
          this.session.set(null)
          return
        }
        const client = supabase
        const callback = initialMagicLinkCallback
        let callbackError: string | null = null

        // Waits for supabase-js to finish its own URL detection (PKCE code exchange).
        let { data, error } = await client.auth.getSession()
        if (error) throw error

        if (callback.kind === 'token-hash' && !data.session) {
          const result = await client.auth.verifyOtp({ token_hash: callback.tokenHash, type: callback.otpType })
          if (result.error) callbackError = describeMagicLinkError(result.error.code ?? null, result.error.message)
          ;({ data, error } = await client.auth.getSession())
          if (error) throw error
        }

        this.session.set(data.session)

        if (callback.kind === 'error') {
          callbackError = describeMagicLinkError(callback.errorCode, callback.description)
        } else if (callback.kind === 'pkce-code') {
          // supabase-js only exchanges the code when the code verifier created by
          // signInWithOtp exists in this browser's storage.
          if (!data.session) callbackError = PKCE_SESSION_MISSING_MESSAGE
          window.history.replaceState(window.history.state, '', removeMagicLinkParams(window.location.href, { keepCode: false }))
        }

        if (callbackError) this.error.set(callbackError)
        this.subscription = client.auth.onAuthStateChange((_event, session) => {
          this.session.set(session)
        }).data.subscription
      } catch (error) {
        this.session.set(null)
        this.error.set(error instanceof Error ? error.message : 'Impossible de restaurer la session Supabase.')
      } finally {
        this.ready.set(true)
        this.initializing.set(false)
      }
    })()
    return this.initializePromise
  }

  async ensureReady(): Promise<void> {
    if (!this.ready()) await this.initialize()
  }

  async accessToken(): Promise<string | null> {
    if (!supabase) return null
    const { data, error } = await supabase.auth.getSession()
    if (error) throw error
    this.session.set(data.session)
    return data.session?.access_token ?? null
  }

  async sendMagicLink(email: string, redirectPath = '/login'): Promise<void> {
    if (!supabase) throw new Error('Supabase n’est pas configuré.')
    this.error.set(null)
    const { error } = await supabase.auth.signInWithOtp({
      email,
      options: { emailRedirectTo: buildSupabaseRedirectUrl(redirectPath) ?? undefined },
    })
    if (error) throw error
  }

  async signOut(): Promise<void> {
    if (!supabase) {
      this.session.set(null)
      return
    }
    const { error } = await supabase.auth.signOut()
    this.session.set(null)
    if (error) throw error
  }
}
