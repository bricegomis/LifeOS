import { ApplicationConfig } from '@angular/core'
import { provideRouter, withHashLocation } from '@angular/router'
import { provideHttpClient, withInterceptors } from '@angular/common/http'
import { providePrimeNG } from 'primeng/config'
import Aura from '@primeuix/themes/aura'
import { routes } from '@/app/app.routes'
import { supabaseBearerInterceptor } from '@/app/core/api/auth.interceptor'

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withHashLocation()),
    provideHttpClient(withInterceptors([supabaseBearerInterceptor])),
    providePrimeNG({
      ripple: true,
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: false,
        },
      },
    }),
  ],
}
