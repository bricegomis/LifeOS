import { ApplicationConfig } from '@angular/core'
import { provideRouter, withComponentInputBinding, withHashLocation } from '@angular/router'
import { provideHttpClient, withInterceptors } from '@angular/common/http'
import { routes } from '@/app/app.routes'
import { supabaseBearerInterceptor } from '@/app/core/api/auth.interceptor'

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withHashLocation(), withComponentInputBinding()),
    provideHttpClient(withInterceptors([supabaseBearerInterceptor])),
  ],
}
