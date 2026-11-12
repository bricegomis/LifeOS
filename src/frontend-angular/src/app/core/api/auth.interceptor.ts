import { inject } from '@angular/core'
import { HttpInterceptorFn } from '@angular/common/http'
import { from, switchMap } from 'rxjs'
import { AuthService } from '@/app/core/auth/auth.service'
import { apiBaseUrl } from '@/environments/environment'

/** Adds the LifeOS access token to LifeOS API calls only. */
export const bearerInterceptor: HttpInterceptorFn = (request, next) => {
  if (!apiBaseUrl || !request.url.startsWith(`${apiBaseUrl}/`)) return next(request)
  const auth = inject(AuthService)
  return from(auth.accessToken()).pipe(
    switchMap((token) => {
      if (!token) return next(request)
      return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
    }),
  )
}
