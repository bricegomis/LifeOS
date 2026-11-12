import { computed, Injectable, signal } from '@angular/core'
import { Capacitor } from '@capacitor/core'
import { Browser } from '@capacitor/browser'
import { apiBaseUrl, baseUrl } from '@/environments/environment'
import { validatedReturnPath } from '@/app/core/auth/auth-flow'
import {
  buildAuthorizeUrl,
  callbackErrorMessage,
  CLIENT_ID,
  codeChallenge,
  NATIVE_REDIRECT_URI,
  needsRefresh,
  parsePendingSignIn,
  parseStoredSession,
  postForm,
  randomToken,
  readOAuthCallback,
  removeOAuthParams,
  sessionFromTokenResponse,
  TokenError,
  webRedirectUri,
  type AuthUser,
  type OAuthCallback,
  type StoredSession,
} from '@/app/core/auth/oauth-client'

const SESSION_KEY = 'lifeos.auth.session'
const PENDING_KEY = 'lifeos.auth.pending'
const FORCE_LOGIN_KEY = 'lifeos.auth.force-login'
const LEGACY_KEYS = ['lifeos.supabase.auth']

// Captured before the router reads the URL; the parameters are removed right away so the hash
// router lands on a valid route. AuthService.initialize() finishes the sign-in.
const initialCallback: OAuthCallback = typeof window === 'undefined' ? { kind: 'none' } : readOAuthCallback(window.location.href)
if (typeof window !== 'undefined' && initialCallback.kind !== 'none') {
  window.history.replaceState(window.history.state, '', removeOAuthParams(window.location.href))
}

function readStorage(key: string): string | null {
  try { return localStorage.getItem(key) } catch { return null }
}

function writeStorage(key: string, value: string | null): void {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, value)
  } catch { /* private mode: the session lasts as long as the page */ }
}

/**
 * Signs in through the LifeOS API OAuth server (ADR 0011): Google is handled by the API, the
 * app receives an access token and a rotating refresh token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly session = signal<StoredSession | null>(null)
  readonly ready = signal(false)
  readonly initializing = signal(false)
  readonly error = signal<string | null>(null)
  readonly user = computed<AuthUser | null>(() => this.session()?.user ?? null)
  readonly authenticated = computed(() => Boolean(this.session()?.accessToken))
  readonly configured = Boolean(apiBaseUrl)

  private initializePromise: Promise<void> | null = null
  private refreshPromise: Promise<StoredSession | null> | null = null
  private returnPath: string | null = null

  initialize(): Promise<void> {
    if (this.initializePromise) return this.initializePromise
    this.initializing.set(true)
    this.initializePromise = (async () => {
      try {
        LEGACY_KEYS.forEach((key) => writeStorage(key, null))
        this.session.set(parseStoredSession(readStorage(SESSION_KEY)))
        if (initialCallback.kind !== 'none') {
          this.returnPath = await this.completeSignIn(initialCallback)
        }
      } catch (error) {
        this.error.set(error instanceof Error ? error.message : 'Connexion impossible.')
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

  /** The internal route requested before signing in, once. */
  takeReturnPath(): string | null {
    const path = this.returnPath
    this.returnPath = null
    return path
  }

  async accessToken(): Promise<string | null> {
    await this.ensureReady()
    const current = this.session()
    if (!current) return null
    if (!needsRefresh(current, Date.now())) return current.accessToken
    return (await this.refresh())?.accessToken ?? null
  }

  async signInWithGoogle(returnPath = '/'): Promise<void> {
    if (!this.configured) throw new Error('L’adresse de LifeOS API n’est pas configurée.')
    this.error.set(null)
    const native = Capacitor.isNativePlatform()
    const verifier = randomToken(48)
    const state = randomToken(24)
    const redirectUri = native ? NATIVE_REDIRECT_URI : webRedirectUri(window.location.origin, baseUrl)
    writeStorage(PENDING_KEY, JSON.stringify({ state, verifier, redirectUri, returnPath: validatedReturnPath(returnPath) }))
    const url = buildAuthorizeUrl(apiBaseUrl, {
      redirectUri,
      state,
      challenge: await codeChallenge(verifier),
      forceLogin: readStorage(FORCE_LOGIN_KEY) === '1',
    })
    if (native) await Browser.open({ url, windowName: '_system' })
    else window.location.assign(url)
  }

  async handleNativeCallback(callbackUrl: string): Promise<string> {
    try {
      await Browser.close()
    } catch (error) {
      console.warn('LifeOS : fermeture du navigateur natif indisponible', error instanceof Error ? error.message : 'erreur native')
    }
    try {
      return await this.completeSignIn(readOAuthCallback(callbackUrl))
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Connexion impossible.')
      throw error
    }
  }

  async signOut(): Promise<void> {
    const refreshToken = this.session()?.refreshToken
    this.store(null)
    // Next sign-in asks Google again instead of reusing the API's short sign-in cookie.
    writeStorage(FORCE_LOGIN_KEY, '1')
    if (refreshToken) {
      try {
        await postForm(fetch, `${apiBaseUrl}/connect/revoke`, { client_id: CLIENT_ID, token: refreshToken, token_type_hint: 'refresh_token' })
      } catch { /* the token expires anyway; signing out locally is what matters */ }
    }
  }

  private async completeSignIn(callback: OAuthCallback): Promise<string> {
    const pending = parsePendingSignIn(readStorage(PENDING_KEY))
    writeStorage(PENDING_KEY, null)
    if (callback.kind === 'none') throw new Error('Paramètres de retour de connexion manquants.')
    if (!pending || callback.state !== pending.state) {
      throw new Error('Retour de connexion inattendu. Relancez la connexion depuis cet appareil.')
    }
    if (callback.kind === 'error') throw new Error(callbackErrorMessage(callback.error, callback.description))

    const body = await postForm(fetch, `${apiBaseUrl}/connect/token`, {
      grant_type: 'authorization_code',
      client_id: CLIENT_ID,
      code: callback.code,
      redirect_uri: pending.redirectUri,
      code_verifier: pending.verifier,
    })
    this.store(sessionFromTokenResponse(body, Date.now()))
    writeStorage(FORCE_LOGIN_KEY, null)
    this.error.set(null)
    return validatedReturnPath(pending.returnPath)
  }

  private refresh(): Promise<StoredSession | null> {
    this.refreshPromise ??= (async () => {
      const current = this.session()
      if (!current?.refreshToken) {
        this.store(null)
        return null
      }
      try {
        const body = await postForm(fetch, `${apiBaseUrl}/connect/token`, {
          grant_type: 'refresh_token',
          client_id: CLIENT_ID,
          refresh_token: current.refreshToken,
        })
        const next = sessionFromTokenResponse(body, Date.now(), current)
        this.store(next)
        return next
      } catch (error) {
        // A network failure keeps the session for a later retry; a refused token ends it.
        if (error instanceof TokenError && error.code === 'network') return null
        this.store(null)
        this.error.set('Session expirée. Reconnectez-vous.')
        return null
      }
    })().finally(() => { this.refreshPromise = null })
    return this.refreshPromise
  }

  private store(session: StoredSession | null): void {
    this.session.set(session)
    writeStorage(SESSION_KEY, session ? JSON.stringify(session) : null)
  }
}
