import { Injectable, inject } from '@angular/core'
import { Router } from '@angular/router'
import { App as CapApp } from '@capacitor/app'
import { AuthService } from '@/app/core/auth/auth.service'
import { validatedReturnPath } from '@/app/core/auth/auth-flow'

@Injectable({ providedIn: 'root' })
export class DeepLinkService {
  private readonly router = inject(Router)
  private readonly auth = inject(AuthService)
  private isListening = false

  initListeners(): void {
    if (this.isListening) return
    this.isListening = true

    try {
      // Warm start: app opened from background
      CapApp.addListener('appUrlOpen', (event) => {
        if (event?.url) {
          void this.handleIncomingUrl(event.url)
        }
      })

      // Cold start: app launched from dead state
      void CapApp.getLaunchUrl().then((launch) => {
        if (launch?.url) {
          void this.handleIncomingUrl(launch.url)
        }
      })
    } catch {
      // not running in native app
    }
  }

  async handleIncomingUrl(url: string): Promise<void> {
    if (!url) return

    // 1. Native auth callback (e.g. lifeos://auth/callback?code=... or lifeos://login?code=...)
    if (url.startsWith('lifeos://auth/callback') || url.startsWith('lifeos://login')) {
      try {
        const targetPath = await this.auth.handleNativeCallback(url)
        await this.router.navigateByUrl(targetPath, { replaceUrl: true })
      } catch {
        await this.router.navigateByUrl('/login', { replaceUrl: true })
      }
      return
    }

    // 2. Widget or notification deep link (e.g. lifeos:///planning, lifeos://planning, lifeos:///recipes)
    const match = url.match(/^lifeos:\/\/?\/?(.*)$/)
    if (match) {
      const pathWithQuery = '/' + (match[1] || '')
      const safePath = validatedReturnPath(pathWithQuery)
      await this.router.navigateByUrl(safePath)
    }
  }
}
