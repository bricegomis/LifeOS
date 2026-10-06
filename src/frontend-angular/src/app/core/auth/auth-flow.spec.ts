import assert from 'node:assert/strict'
import { describe, it, mock } from 'node:test'
import { AuthError, createClient, type Session } from '@supabase/supabase-js'
import { buildLoginRedirectPath, restoreAuthSession, startGoogleSignIn, validatedReturnPath } from './auth-flow.ts'
import { readMagicLinkCallback, removeMagicLinkParams } from './magic-link-callback.ts'

const session: Session = {
  access_token: 'supabase-access-token',
  refresh_token: 'supabase-refresh-token',
  token_type: 'bearer',
  expires_in: 3600,
  user: { id: 'existing-user', app_metadata: {}, user_metadata: {}, aud: 'authenticated', created_at: '' },
}

function authClient(existingSession: Session | null = null): Parameters<typeof restoreAuthSession>[0] {
  return {
    getSession: async () => existingSession
      ? { data: { session: existingSession }, error: null }
      : { data: { session: null }, error: null },
    exchangeCodeForSession: async () => ({ data: { session, user: session.user, redirectType: null }, error: null }),
    verifyOtp: async () => ({ data: { session, user: session.user }, error: null }),
    signInWithOAuth: async () => ({ data: { provider: 'google', url: 'https://accounts.google.com/' }, error: null }),
  }
}

describe('internal return route', () => {
  it('accepts current routes and encodes them inside the login callback', () => {
    for (const path of ['/', '/planning', '/settings', '/planner', '/recipes?search=soupe']) {
      assert.equal(validatedReturnPath(path), path)
      const url = new URL(`https://lifeos.example/#${buildLoginRedirectPath(path)}`)
      assert.equal(new URLSearchParams(url.hash.split('?')[1]).get('redirect'), path)
    }
  })

  it('rejects external, protocol-relative, encoded, auxiliary, login and unknown routes', () => {
    for (const path of [null, '', 'https://evil.example', '//evil.example', '/\\evil', '/%2f%2fevil', '/login', '/planning(other:login)', '/unknown', '/planning#x', '/planning\n']) {
      assert.equal(validatedReturnPath(path), '/')
    }
  })
})

describe('Google OAuth launch', () => {
  it('uses the official SDK with Google, an explicit return URL and same-tab redirection', async () => {
    const auth = authClient()
    const launch = mock.method(auth, 'signInWithOAuth')
    const redirectTo = 'https://lifeos.example/#/login?redirect=%2Fplanning'
    await startGoogleSignIn(auth, redirectTo)
    assert.deepEqual(launch.mock.calls[0]?.arguments, [{
      provider: 'google',
      options: { redirectTo, skipBrowserRedirect: false },
    }])
  })

  it('surfaces disabled-provider/network failures and absent redirect URLs', async () => {
    const auth = authClient()
    mock.method(auth, 'signInWithOAuth', async () => ({
      data: { provider: 'google' as const, url: null },
      error: new AuthError('Provider disabled'),
    }))
    await assert.rejects(startGoogleSignIn(auth, 'https://lifeos.example/'), /Provider disabled/)
    mock.method(auth, 'signInWithOAuth', async () => ({
      data: { provider: 'google' as const, url: null }, error: null,
    }))
    await assert.rejects(startGoogleSignIn(auth, 'https://lifeos.example/'), /page de connexion/)
  })
})

describe('auth callback and restoration', () => {
  it('exchanges a PKCE code exactly once, even with a previous session', async () => {
    const auth = authClient(session)
    const exchange = mock.method(auth, 'exchangeCodeForSession')
    const read = mock.method(auth, 'getSession')
    const href = 'https://lifeos.example/?code=oauth-code#/login?redirect=%2Fplanning'
    assert.equal(await restoreAuthSession(auth, readMagicLinkCallback(href)), session)
    assert.equal(exchange.mock.callCount(), 1)
    assert.deepEqual(exchange.mock.calls[0]?.arguments, ['oauth-code'])
    assert.equal(read.mock.callCount(), 0)
    assert.equal(removeMagicLinkParams(href, { keepCode: false }), 'https://lifeos.example/#/login?redirect=%2Fplanning')
  })

  it('shows missing-verifier or expired-code errors instead of treating an old session as success', async () => {
    const auth = authClient(session)
    mock.method(auth, 'exchangeCodeForSession', async () => ({
      data: { session: null, user: null, redirectType: null },
      error: new AuthError('PKCE code verifier not found'),
    }))
    await assert.rejects(restoreAuthSession(auth, { kind: 'pkce-code', code: 'code' }), /PKCE code verifier.*fenêtre privée/)
  })

  it('rejects empty PKCE results and session restoration errors explicitly', async () => {
    const auth = authClient()
    mock.method(auth, 'exchangeCodeForSession', async () => ({
      data: { session: null, user: null, redirectType: null }, error: null,
    }))
    await assert.rejects(restoreAuthSession(auth, { kind: 'pkce-code', code: 'code' }), /Aucune session/)
    mock.method(auth, 'getSession', async () => ({
      data: { session: null }, error: new AuthError('Refresh failed'),
    }))
    await assert.rejects(restoreAuthSession(auth, { kind: 'none' }), /Refresh failed/)
  })

  it('reports refusal/cancellation, OTP expiration and other provider errors', async () => {
    const auth = authClient(session)
    const href = 'https://lifeos.example/#error=access_denied&error_description=User+denied'
    await assert.rejects(restoreAuthSession(auth, readMagicLinkCallback(href)), /annulée ou refusée/)
    assert.equal(removeMagicLinkParams(href, { keepCode: false }), 'https://lifeos.example/#/login')
    await assert.rejects(restoreAuthSession(auth, { kind: 'error', errorCode: 'otp_expired', description: null }), /expiré/)
    await assert.rejects(restoreAuthSession(auth, { kind: 'error', errorCode: 'server_error', description: 'Provider unavailable' }), /Provider unavailable/)
  })

  it('restores existing sessions and keeps cross-browser token-hash email support', async () => {
    const auth = authClient(session)
    const verify = mock.method(auth, 'verifyOtp')
    assert.equal(await restoreAuthSession(auth, { kind: 'none' }), session)
    assert.equal(await restoreAuthSession(auth, { kind: 'token-hash', tokenHash: 'hash', otpType: 'email' }), session)
    assert.equal(verify.mock.callCount(), 0)
    const emailAuth = authClient()
    const emailVerify = mock.method(emailAuth, 'verifyOtp')
    assert.equal(await restoreAuthSession(emailAuth, { kind: 'token-hash', tokenHash: 'hash', otpType: 'email' }), session)
    assert.deepEqual(emailVerify.mock.calls[0]?.arguments, [{ token_hash: 'hash', type: 'email' }])
    assert.equal(await restoreAuthSession(authClient(), { kind: 'none' }), null)
  })

  it('surfaces invalid token-hash errors', async () => {
    const auth = authClient()
    mock.method(auth, 'verifyOtp', async () => ({
      data: { session: null, user: null }, error: new AuthError('Invalid email token'),
    }))
    await assert.rejects(restoreAuthSession(auth, { kind: 'token-hash', tokenHash: 'hash', otpType: 'email' }), /Invalid email token/)
  })

  it('integrates with the real SDK PKCE exchange and persisted Supabase session', async () => {
    const storage = new Map<string, string>([['lifeos.supabase.auth-code-verifier', JSON.stringify('verifier')]])
    const requests: { url: string; body: string }[] = []
    const client = createClient('https://supabase.example', 'public-anon-key', {
      auth: {
        storageKey: 'lifeos.supabase.auth',
        storage: {
          getItem: (key) => storage.get(key) ?? null,
          setItem: (key, value) => { storage.set(key, value) },
          removeItem: (key) => { storage.delete(key) },
        },
        persistSession: true,
        autoRefreshToken: false,
        detectSessionInUrl: false,
        flowType: 'pkce',
      },
      global: {
        fetch: async (input, init) => {
          requests.push({ url: String(input), body: String(init?.body) })
          return new Response(JSON.stringify(session), { status: 200, headers: { 'Content-Type': 'application/json' } })
        },
      },
    })
    const restored = await restoreAuthSession(client.auth, { kind: 'pkce-code', code: 'google-code' })
    assert.equal(restored?.user.id, 'existing-user')
    assert.match(requests[0]?.url ?? '', /token\?grant_type=pkce/)
    assert.deepEqual(JSON.parse(requests[0]?.body ?? '{}'), { auth_code: 'google-code', code_verifier: 'verifier' })
    assert.equal(storage.has('lifeos.supabase.auth-code-verifier'), false)
    assert.equal((await restoreAuthSession(client.auth, { kind: 'none' }))?.access_token, session.access_token)
    assert.equal(requests.length, 1)
  })

  it('refreshes an expired persisted session and propagates sign-out with the real SDK', async () => {
    const storage = new Map<string, string>([['lifeos.supabase.auth', JSON.stringify({
      ...session, expires_at: Math.floor(Date.now() / 1000) - 60,
    })]])
    const requests: string[] = []
    const events: string[] = []
    const client = createClient('https://supabase.example', 'public-anon-key', {
      auth: {
        storageKey: 'lifeos.supabase.auth',
        storage: {
          getItem: (key) => storage.get(key) ?? null,
          setItem: (key, value) => { storage.set(key, value) },
          removeItem: (key) => { storage.delete(key) },
        },
        persistSession: true,
        autoRefreshToken: false,
        detectSessionInUrl: false,
        flowType: 'pkce',
      },
      global: {
        fetch: async (input) => {
          const url = String(input)
          requests.push(url)
          return url.includes('/logout')
            ? new Response(null, { status: 204 })
            : new Response(JSON.stringify({ ...session, access_token: 'refreshed-supabase-token' }), {
              status: 200, headers: { 'Content-Type': 'application/json' },
            })
        },
      },
    })
    const { data: { subscription } } = client.auth.onAuthStateChange((event) => { events.push(event) })
    const restored = await restoreAuthSession(client.auth, { kind: 'none' })
    assert.equal(restored?.access_token, 'refreshed-supabase-token')
    assert.equal(restored?.user.id, session.user.id)
    assert.match(requests[0] ?? '', /token\?grant_type=refresh_token/)
    assert.ok(events.includes('TOKEN_REFRESHED'))
    const { error } = await client.auth.signOut()
    assert.equal(error, null)
    assert.equal(storage.has('lifeos.supabase.auth'), false)
    assert.equal(await restoreAuthSession(client.auth, { kind: 'none' }), null)
    assert.ok(events.includes('SIGNED_OUT'))
    subscription.unsubscribe()
  })
})
