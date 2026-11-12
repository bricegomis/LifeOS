import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { createHash } from 'node:crypto'
import {
  buildAuthorizeUrl,
  codeChallenge,
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
  type StoredSession,
} from './oauth-client.ts'

function jwt(payload: object): string {
  return `header.${Buffer.from(JSON.stringify(payload)).toString('base64url')}.signature`
}

const session: StoredSession = {
  accessToken: 'access-1',
  refreshToken: 'refresh-1',
  expiresAt: 10_000_000,
  user: { id: 'user-1', email: 'brice@example.com', name: 'Brice' },
}

describe('PKCE', () => {
  it('derives the S256 challenge of RFC 7636', async () => {
    // RFC 7636, appendix B.
    assert.equal(await codeChallenge('dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk'), 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM')
    const verifier = randomToken(48)
    assert.match(verifier, /^[A-Za-z0-9_-]{64}$/)
    assert.equal(await codeChallenge(verifier), createHash('sha256').update(verifier).digest('base64url'))
    assert.notEqual(randomToken(), randomToken())
  })
})

describe('authorization request', () => {
  it('targets the API authorize endpoint with code, PKCE S256 and the LifeOS scopes', () => {
    const url = new URL(buildAuthorizeUrl('https://api.lifeos.example', {
      redirectUri: 'https://lifeos.example/', state: 'st', challenge: 'ch', forceLogin: false,
    }))
    assert.equal(url.origin + url.pathname, 'https://api.lifeos.example/connect/authorize')
    assert.equal(url.searchParams.get('client_id'), 'lifeos-web')
    assert.equal(url.searchParams.get('response_type'), 'code')
    assert.equal(url.searchParams.get('code_challenge_method'), 'S256')
    assert.equal(url.searchParams.get('scope'), 'openid email profile offline_access lifeos')
    assert.equal(url.searchParams.has('prompt'), false)
    const forced = new URL(buildAuthorizeUrl('https://api.lifeos.example', {
      redirectUri: 'lifeos://auth/callback', state: 'st', challenge: 'ch', forceLogin: true,
    }))
    assert.equal(forced.searchParams.get('prompt'), 'login')
  })

  it('uses the app base URL without hash as web redirect URI', () => {
    assert.equal(webRedirectUri('https://lifeos.example', '/'), 'https://lifeos.example/')
    assert.equal(webRedirectUri('https://user.github.io', '/LifeOS/'), 'https://user.github.io/LifeOS/')
  })
})

describe('authorization response', () => {
  it('reads code and state, or the error, from the query string only', () => {
    assert.deepEqual(readOAuthCallback('https://lifeos.example/?code=c1&state=s1#/recipes'), { kind: 'code', code: 'c1', state: 's1' })
    assert.deepEqual(readOAuthCallback('lifeos://auth/callback?code=c1&state=s1'), { kind: 'code', code: 'c1', state: 's1' })
    assert.deepEqual(
      readOAuthCallback('https://lifeos.example/?error=access_denied&error_description=Refus%C3%A9&state=s1'),
      { kind: 'error', error: 'access_denied', description: 'Refusé', state: 's1' },
    )
    assert.deepEqual(readOAuthCallback('https://lifeos.example/?code=c1'), { kind: 'none' })
    assert.deepEqual(readOAuthCallback('https://lifeos.example/#/recipes?code=c1&state=s1'), { kind: 'none' })
  })

  it('removes the callback parameters and keeps the rest', () => {
    assert.equal(
      removeOAuthParams('https://lifeos.example/?code=c&state=s&iss=x&keep=1#/recipes'),
      'https://lifeos.example/?keep=1#/recipes',
    )
  })
})

describe('tokens', () => {
  it('builds the session from the token response and its identity token', () => {
    const next = sessionFromTokenResponse(
      { access_token: 'a', refresh_token: 'r', expires_in: 3600, id_token: jwt({ sub: 'u', email: 'e@x.fr', name: 'Élise' }) },
      1_000,
    )
    assert.deepEqual(next, { accessToken: 'a', refreshToken: 'r', expiresAt: 3_601_000, user: { id: 'u', email: 'e@x.fr', name: 'Élise' } })
  })

  it('keeps the previous refresh token and user when a refresh omits them', () => {
    const next = sessionFromTokenResponse({ access_token: 'a2', expires_in: 60 }, 0, session)
    assert.equal(next.refreshToken, 'refresh-1')
    assert.deepEqual(next.user, session.user)
    assert.throws(() => sessionFromTokenResponse({ access_token: 'a2' }, 0, null), TokenError)
    assert.throws(() => sessionFromTokenResponse({}, 0, session), TokenError)
  })

  it('refreshes one minute before expiry', () => {
    assert.equal(needsRefresh(session, session.expiresAt - 120_000), false)
    assert.equal(needsRefresh(session, session.expiresAt - 30_000), true)
  })

  it('only restores well-formed stored state', () => {
    assert.deepEqual(parseStoredSession(JSON.stringify(session)), session)
    assert.equal(parseStoredSession('{"accessToken":"a"}'), null)
    assert.equal(parseStoredSession('not json'), null)
    assert.equal(parseStoredSession(null), null)
    const pending = { state: 's', verifier: 'v', redirectUri: 'lifeos://auth/callback', returnPath: '/recipes' }
    assert.deepEqual(parsePendingSignIn(JSON.stringify(pending)), pending)
    assert.equal(parsePendingSignIn('{"state":"s"}'), null)
  })
})

describe('form posts', () => {
  it('sends a form body and surfaces OAuth errors', async () => {
    let sent: RequestInit | undefined
    const ok = (async (_url: string, init?: RequestInit) => {
      sent = init
      return new Response(JSON.stringify({ access_token: 'a' }), { status: 200 })
    }) as typeof fetch
    assert.deepEqual(await postForm(ok, 'https://api/connect/token', { grant_type: 'refresh_token', refresh_token: 'r' }), { access_token: 'a' })
    assert.equal(sent?.body, 'grant_type=refresh_token&refresh_token=r')
    assert.equal((sent?.headers as Record<string, string>)['Content-Type'], 'application/x-www-form-urlencoded')

    const refused = (async () => new Response(JSON.stringify({ error: 'invalid_grant', error_description: 'Expiré' }), { status: 400 })) as typeof fetch
    await assert.rejects(postForm(refused, 'u', {}), (error: unknown) => error instanceof TokenError && error.code === 'invalid_grant' && error.message === 'Expiré')

    const offline = (async () => { throw new TypeError('Failed to fetch') }) as typeof fetch
    await assert.rejects(postForm(offline, 'u', {}), (error: unknown) => error instanceof TokenError && error.code === 'network')
  })
})
