// Pure OAuth 2.1 helpers for the LifeOS API authorization server (ADR 0011): authorization
// code with PKCE S256, rotating refresh tokens. No framework imports, so `node --test` runs them.

export const CLIENT_ID = 'lifeos-web'
export const NATIVE_REDIRECT_URI = 'lifeos://auth/callback'
export const SCOPES = 'openid email profile offline_access lifeos'
const REFRESH_SKEW_MS = 60_000

export interface AuthUser {
  id: string
  email: string
  name: string
}

export interface StoredSession {
  accessToken: string
  refreshToken: string | null
  expiresAt: number
  user: AuthUser
}

export interface PendingSignIn {
  state: string
  verifier: string
  redirectUri: string
  returnPath: string
}

export type OAuthCallback =
  | { kind: 'none' }
  | { kind: 'code'; code: string; state: string }
  | { kind: 'error'; error: string; description: string | null; state: string | null }

export class TokenError extends Error {
  readonly code: string

  constructor(code: string, message: string) {
    super(message)
    this.code = code
  }
}

export function base64Url(bytes: Uint8Array): string {
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

export function randomToken(byteLength = 32, cryptoImpl: Crypto = globalThis.crypto): string {
  return base64Url(cryptoImpl.getRandomValues(new Uint8Array(byteLength)))
}

export async function codeChallenge(verifier: string, cryptoImpl: Crypto = globalThis.crypto): Promise<string> {
  const digest = await cryptoImpl.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
  return base64Url(new Uint8Array(digest))
}

/** Web redirect URI: the app's base URL, without hash (the router uses hash locations). */
export function webRedirectUri(origin: string, baseHref: string): string {
  return new URL(baseHref || '/', origin).toString()
}

export function buildAuthorizeUrl(
  apiBaseUrl: string,
  options: { redirectUri: string; state: string; challenge: string; forceLogin: boolean },
): string {
  const url = new URL('/connect/authorize', apiBaseUrl)
  url.search = new URLSearchParams({
    client_id: CLIENT_ID,
    response_type: 'code',
    redirect_uri: options.redirectUri,
    scope: SCOPES,
    state: options.state,
    code_challenge: options.challenge,
    code_challenge_method: 'S256',
    ...(options.forceLogin ? { prompt: 'login' } : {}),
  }).toString()
  return url.toString()
}

/** The authorization response arrives in the query string, before any hash route. */
export function readOAuthCallback(href: string): OAuthCallback {
  const params = new URL(href).searchParams
  const state = params.get('state')
  const error = params.get('error')
  if (error) return { kind: 'error', error, description: params.get('error_description'), state }
  const code = params.get('code')
  if (code && state) return { kind: 'code', code, state }
  return { kind: 'none' }
}

const CALLBACK_PARAMS = ['code', 'state', 'error', 'error_description', 'error_uri', 'iss']

export function removeOAuthParams(href: string): string {
  const url = new URL(href)
  CALLBACK_PARAMS.forEach((key) => url.searchParams.delete(key))
  return url.toString()
}

export function callbackErrorMessage(error: string, description: string | null): string {
  if (error === 'access_denied') {
    return description || 'La connexion a été annulée ou refusée.'
  }
  return description ? `Connexion impossible : ${description}` : 'Connexion impossible. Réessayez.'
}

export function decodeJwtPayload(token: string): Record<string, unknown> {
  const part = token.split('.')[1]
  if (!part) throw new Error('Jeton d’identité illisible.')
  const base64 = part.replace(/-/g, '+').replace(/_/g, '/')
  const json = new TextDecoder().decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)))
  return JSON.parse(json) as Record<string, unknown>
}

interface TokenResponse {
  access_token?: unknown
  refresh_token?: unknown
  expires_in?: unknown
  id_token?: unknown
}

export function sessionFromTokenResponse(body: TokenResponse, now: number, previous: StoredSession | null = null): StoredSession {
  if (typeof body.access_token !== 'string' || !body.access_token) throw new TokenError('invalid_response', 'Réponse de connexion invalide.')
  let user = previous?.user ?? null
  if (typeof body.id_token === 'string') {
    const claims = decodeJwtPayload(body.id_token)
    if (typeof claims['sub'] === 'string') {
      const email = typeof claims['email'] === 'string' ? claims['email'] : user?.email ?? ''
      const name = typeof claims['name'] === 'string' ? claims['name'] : email
      user = { id: claims['sub'], email, name }
    }
  }
  if (!user) throw new TokenError('invalid_response', 'Identité absente de la réponse de connexion.')
  const expiresIn = typeof body.expires_in === 'number' ? body.expires_in : 3600
  return {
    accessToken: body.access_token,
    // Refresh tokens rotate: always keep the newest one.
    refreshToken: typeof body.refresh_token === 'string' ? body.refresh_token : previous?.refreshToken ?? null,
    expiresAt: now + expiresIn * 1000,
    user,
  }
}

export function needsRefresh(session: StoredSession, now: number): boolean {
  return session.expiresAt - REFRESH_SKEW_MS <= now
}

export function parseStoredSession(raw: string | null): StoredSession | null {
  if (!raw) return null
  try {
    const value = JSON.parse(raw) as Partial<StoredSession>
    const user = value.user as Partial<AuthUser> | undefined
    if (typeof value.accessToken !== 'string' || typeof value.expiresAt !== 'number'
      || (value.refreshToken !== null && typeof value.refreshToken !== 'string')
      || typeof user?.id !== 'string' || typeof user.email !== 'string' || typeof user.name !== 'string') return null
    return value as StoredSession
  } catch {
    return null
  }
}

export function parsePendingSignIn(raw: string | null): PendingSignIn | null {
  if (!raw) return null
  try {
    const value = JSON.parse(raw) as Partial<PendingSignIn>
    if (typeof value.state !== 'string' || typeof value.verifier !== 'string'
      || typeof value.redirectUri !== 'string' || typeof value.returnPath !== 'string') return null
    return value as PendingSignIn
  } catch {
    return null
  }
}

/** POSTs a form to a token-style endpoint; OAuth errors become {@link TokenError}. */
export async function postForm(
  fetchImpl: typeof fetch,
  url: string,
  params: Record<string, string>,
): Promise<Record<string, unknown>> {
  let response: Response
  try {
    response = await fetchImpl(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(params).toString(),
    })
  } catch {
    throw new TokenError('network', 'LifeOS API injoignable. Vérifiez la connexion.')
  }
  const text = await response.text()
  let body: Record<string, unknown> = {}
  try { body = text ? JSON.parse(text) as Record<string, unknown> : {} } catch { /* non-JSON error page */ }
  if (!response.ok) {
    const code = typeof body['error'] === 'string' ? body['error'] : `http_${response.status}`
    const description = typeof body['error_description'] === 'string' ? body['error_description'] : 'Connexion impossible.'
    throw new TokenError(code, description)
  }
  return body
}
