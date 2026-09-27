// Pure helpers (no framework / Supabase imports) so they can be unit-tested with `node --test`.

export type MagicLinkOtpType = 'magiclink' | 'email' | 'signup' | 'invite'

export type MagicLinkCallback =
  | { kind: 'none' }
  | { kind: 'pkce-code' }
  | { kind: 'token-hash'; tokenHash: string; otpType: MagicLinkOtpType }
  | { kind: 'error'; errorCode: string | null; description: string | null }

const OTP_TYPES: readonly MagicLinkOtpType[] = ['magiclink', 'email', 'signup', 'invite']
const ERROR_KEYS = ['error', 'error_code', 'error_description']

// The app uses hash routing (`#/login?redirect=...`). Supabase adds its parameters to the
// query string, and on errors it overwrites the whole fragment (`#error=...&sb=`).
function readHashParams(hash: string): URLSearchParams {
  const fragment = hash.startsWith('#') ? hash.slice(1) : hash

  if (fragment.startsWith('/')) {
    const queryIndex = fragment.indexOf('?')
    return new URLSearchParams(queryIndex >= 0 ? fragment.slice(queryIndex + 1) : '')
  }

  return new URLSearchParams(fragment)
}

function isOtpType(value: string | null): value is MagicLinkOtpType {
  return value !== null && (OTP_TYPES as readonly string[]).includes(value)
}

export function readMagicLinkCallback(href: string): MagicLinkCallback {
  const url = new URL(href)
  const hashParams = readHashParams(url.hash)
  const read = (key: string): string | null => url.searchParams.get(key) ?? hashParams.get(key)

  if (ERROR_KEYS.some((key) => read(key) !== null)) {
    return {
      kind: 'error',
      errorCode: read('error_code') || read('error') || null,
      description: read('error_description') || null,
    }
  }

  const tokenHash = read('token_hash')
  const otpType = read('type')

  if (tokenHash && isOtpType(otpType)) {
    return { kind: 'token-hash', tokenHash, otpType }
  }

  if (url.searchParams.get('code')) {
    return { kind: 'pkce-code' }
  }

  return { kind: 'none' }
}

function deleteAuthParams(params: URLSearchParams, removeCode: boolean): void {
  if (params.has('token_hash')) {
    params.delete('token_hash')
    params.delete('type')
  }

  ERROR_KEYS.forEach((key) => params.delete(key))
  params.delete('sb')

  if (removeCode) {
    params.delete('code')
  }
}

export function removeMagicLinkParams(href: string, options: { keepCode: boolean }): string {
  const url = new URL(href)
  const removeCode = !options.keepCode

  deleteAuthParams(url.searchParams, removeCode)

  const fragment = url.hash.slice(1)

  if (fragment.startsWith('/')) {
    const queryIndex = fragment.indexOf('?')

    if (queryIndex >= 0) {
      const params = new URLSearchParams(fragment.slice(queryIndex + 1))
      deleteAuthParams(params, removeCode)
      const query = params.toString()
      url.hash = `${fragment.slice(0, queryIndex)}${query ? `?${query}` : ''}`
    }
  } else if (fragment) {
    const params = new URLSearchParams(fragment)

    if ([...ERROR_KEYS, 'sb', 'access_token'].some((key) => params.has(key))) {
      url.hash = '/login'
    }
  }

  return url.toString()
}
