import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable, throwError } from 'rxjs'
import { apiBaseUrl } from '@/environments/environment'

export type ApiMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
export type ApiQuery = Record<string, string | number | boolean | null | undefined>

@Injectable({ providedIn: 'root' })
export class LifeosApiService {
  private readonly http = inject(HttpClient)

  get<T>(path: string, query?: ApiQuery): Observable<T> {
    return this.request<T>('GET', path, undefined, query)
  }

  post<T>(path: string, body?: unknown): Observable<T> {
    return this.request<T>('POST', path, body)
  }

  put<T>(path: string, body?: unknown): Observable<T> {
    return this.request<T>('PUT', path, body)
  }

  patch<T>(path: string, body?: unknown): Observable<T> {
    return this.request<T>('PATCH', path, body)
  }

  delete<T = void>(path: string): Observable<T> {
    return this.request<T>('DELETE', path)
  }

  private request<T>(method: ApiMethod, path: string, body?: unknown, query?: ApiQuery): Observable<T> {
    if (!apiBaseUrl) {
      return throwError(() => new Error('Configurez VITE_LIFEOS_API_URL pour connecter l’application à LifeOS API.'))
    }
    const cleanPath = path.startsWith('/') ? path : `/${path}`
    let params = new HttpParams()
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value !== undefined && value !== null) params = params.set(key, String(value))
    }
    return this.http.request<T>(method, `${apiBaseUrl}/api${cleanPath}`, {
      body,
      params,
    })
  }
}

export function apiErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error
    if (typeof body === 'string' && body.trim()) return body

    if (body && typeof body === 'object') {
      const payload = body as { detail?: unknown; title?: unknown; errors?: unknown }
      if (typeof payload.detail === 'string') return payload.detail
      if (typeof payload.title === 'string') return payload.title

      if (payload.errors && typeof payload.errors === 'object') {
        const messages = Object.values(payload.errors).flatMap((value) =>
          Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [],
        )
        if (messages.length) return messages.join(' ')
      }
    }

    if (error.status) return `LifeOS API a répondu avec le statut HTTP ${error.status}.`
    return 'LifeOS API est inaccessible. Vérifiez le réseau et la configuration CORS.'
  }

  if (error instanceof Error) {
    return error.message
  }
  return 'Une erreur inattendue est survenue lors de la communication avec LifeOS API.'
}
