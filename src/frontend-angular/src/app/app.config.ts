import { ApplicationConfig, provideZonelessChangeDetection } from '@angular/core'
import { provideRouter, withComponentInputBinding, withHashLocation } from '@angular/router'
import { provideHttpClient, withInterceptors } from '@angular/common/http'
import { routes } from '@/app/app.routes'
import { bearerInterceptor } from '@/app/core/api/auth.interceptor'
import { provideLifeosIcons } from '@/app/shared/ui/icons'

export const appConfig: ApplicationConfig = {
  providers: [
    // No zone.js: change detection follows signals, template events and async results set in signals.
    provideZonelessChangeDetection(),
    provideRouter(routes, withHashLocation(), withComponentInputBinding()),
    provideHttpClient(withInterceptors([bearerInterceptor])),
    provideLifeosIcons(),
  ],
}
