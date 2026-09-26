import { inject } from '@angular/core'
import { HttpInterceptorFn } from '@angular/common/http'
import { from, switchMap } from 'rxjs'
import { AuthService } from '@/app/core/auth/auth.service'

export const supabaseBearerInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService)
  return from(auth.accessToken()).pipe(
    switchMap((token) => {
      if (!token) return next(request)
      return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
    }),
  )
}
