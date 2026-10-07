import { ApplicationConfig } from '@angular/core'
import { provideRouter, withHashLocation } from '@angular/router'
import { provideHttpClient, withInterceptors } from '@angular/common/http'
import { routes } from '@/app/app.routes'
import { supabaseBearerInterceptor } from '@/app/core/api/auth.interceptor'

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withHashLocation()),
    provideHttpClient(withInterceptors([supabaseBearerInterceptor])),
  ],
}
