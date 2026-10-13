import { validatedReturnPath } from '../auth/auth-flow.ts'

export type NativeUrl = { kind: 'auth' } | { kind: 'route'; path: string }

export function nativeAuthReturnPath(path: string): string {
  if (path === '/login' || path.startsWith('/login?')) {
    const query = new URLSearchParams(path.split('?')[1] ?? '')
    return validatedReturnPath(query.get('redirect'))
  }
  return validatedReturnPath(path)
}

export function parseNativeUrl(value: string): NativeUrl {
  const url = new URL(value)
  if (url.protocol !== 'lifeos:' || url.username || url.password || url.port) {
    throw new Error('Lien LifeOS invalide')
  }
  if ((url.hostname === 'auth' && url.pathname === '/callback')
    || (url.hostname === 'login' && (url.pathname === '' || url.pathname === '/'))) {
    return { kind: 'auth' }
  }
  if (url.hash) throw new Error('Fragment non autorisé dans une destination LifeOS')
  const path = `/${url.hostname}${url.pathname}`.replace(/^\/\//, '/') + url.search
  const safe = validatedReturnPath(path)
  if (safe === '/' && path !== '/') throw new Error('Destination LifeOS non autorisée')
  return { kind: 'route', path: safe }
}
