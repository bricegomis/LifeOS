import { ApplicationConfig, provideZonelessChangeDetection } from '@angular/core'
import { provideRouter, withComponentInputBinding, withHashLocation } from '@angular/router'
import { provideHttpClient, withInterceptors } from '@angular/common/http'
import { routes } from '@/app/app.routes'
import { supabaseBearerInterceptor } from '@/app/core/api/auth.interceptor'

export const appConfig: ApplicationConfig = {
  providers: [
    // No zone.js: change detection follows signals, template events and async results set in signals.
    provideZonelessChangeDetection(),
    provideRouter(routes, withHashLocation(), withComponentInputBinding()),
    provideHttpClient(withInterceptors([supabaseBearerInterceptor])),
  ],
}
