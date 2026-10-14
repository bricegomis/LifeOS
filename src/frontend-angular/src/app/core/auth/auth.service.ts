import { computed, Injectable, signal } from '@angular/core'
import type { Session, User } from '@supabase/supabase-js'
import { Capacitor } from '@capacitor/core'
import { Browser } from '@capacitor/browser'
import { buildLoginRedirectPath, restoreAuthSession, startGoogleSignIn, validatedReturnPath } from '@/app/core/auth/auth-flow'
import {
  buildSupabaseRedirectUrl,
  initialMagicLinkCallback,
  isSupabaseConfigured,
  supabase,
} from '@/app/core/auth/supabase-client'
import { readMagicLinkCallback } from '@/app/core/auth/magic-link-callback'

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
        this.subscription = client.auth.onAuthStateChange((_event, session) => {
          this.session.set(session)
        }).data.subscription
        this.session.set(await restoreAuthSession(client.auth, initialMagicLinkCallback))
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

  async signInWithGoogle(returnPath = '/'): Promise<void> {
    if (!supabase) throw new Error('Supabase n’est pas configuré.')
    this.error.set(null)
    const redirectTo = buildSupabaseRedirectUrl(
      Capacitor.isNativePlatform() ? validatedReturnPath(returnPath) : buildLoginRedirectPath(returnPath),
    )
    if (!redirectTo) throw new Error('Impossible de déterminer l’URL de retour de connexion.')

    if (Capacitor.isNativePlatform()) {
      await startGoogleSignIn(supabase.auth, redirectTo, async (url: string) => {
        await Browser.open({ url, windowName: '_system' })
      })
    } else {
      await startGoogleSignIn(supabase.auth, redirectTo)
    }
  }

  async handleNativeCallback(callbackUrl: string): Promise<string> {
    if (!supabase) throw new Error('Supabase n’est pas configuré.')

    try {
      await Browser.close()
    } catch (error) {
      console.warn('LifeOS : fermeture du navigateur natif indisponible', error instanceof Error ? error.message : 'erreur native')
    }

    const callback = readMagicLinkCallback(callbackUrl)
    if (callback.kind === 'none') {
      const error = new Error('Paramètres de retour de connexion manquants.')
      this.error.set(error.message)
      throw error
    }
    let redirectPath = '/'
    try {
      const parsedUrl = new URL(callbackUrl)
      const redirectParam = parsedUrl.searchParams.get('redirect')
      if (redirectParam) {
        redirectPath = validatedReturnPath(redirectParam)
      }
    } catch {
      throw new Error('URL de retour de connexion invalide.')
    }

    try {
      const session = await restoreAuthSession(supabase.auth, callback)
      this.session.set(session)
      return redirectPath
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Connexion impossible.')
      throw error
    }
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
