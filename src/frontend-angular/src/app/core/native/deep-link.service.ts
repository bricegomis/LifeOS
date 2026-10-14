import { Injectable, inject, signal } from '@angular/core'
import { Router } from '@angular/router'
import { Capacitor } from '@capacitor/core'
import { App as CapApp } from '@capacitor/app'
import { LocalNotifications } from '@capacitor/local-notifications'
import { AuthService } from '@/app/core/auth/auth.service'
import { validatedReturnPath } from '@/app/core/auth/auth-flow'
import { parseNativeUrl } from './native-url'

@Injectable({ providedIn: 'root' })
export class DeepLinkService {
  private readonly router = inject(Router)
  private readonly auth = inject(AuthService)
  readonly lastError = signal<string | null>(null)
  private urlListening = false
  private notificationListening = false
  private initialization: Promise<void> | null = null
  private readonly callbacks = new Map<string, Promise<void>>()

  async initListeners(): Promise<void> {
    if (!Capacitor.isNativePlatform()) return
    if (this.initialization) return this.initialization
    this.initialization = this.registerListenersAndLaunch()
    try { await this.initialization } finally { this.initialization = null }
  }

  private async registerListenersAndLaunch(): Promise<void> {
    const handle = (url: string) => {
      void this.handleIncomingUrl(url).catch(error => this.reportError(error))
    }
    const registrations: Promise<void>[] = []
    if (!this.urlListening) {
      registrations.push(CapApp.addListener('appUrlOpen', event => { if (event?.url) handle(event.url) })
        .then(() => { this.urlListening = true }, error => this.reportError(error)))
    }
    if (!this.notificationListening) {
      registrations.push(LocalNotifications.addListener('localNotificationActionPerformed', action => {
          const extra = action.notification.extra as { deepLink?: string; date?: string } | undefined
          const destination = extra?.deepLink ?? (extra?.date ? `/planning?date=${extra.date}` : '/planning')
          if (destination.startsWith('lifeos:')) handle(destination)
          else void this.router.navigateByUrl(validatedReturnPath(destination)).catch(error => this.reportError(error))
        }).then(() => { this.notificationListening = true }, error => this.reportError(error)))
    }
    await Promise.all(registrations)
    try {
      const launch = await CapApp.getLaunchUrl()
      if (launch?.url) await this.handleIncomingUrl(launch.url)
    } catch (error) {
      this.reportError(error)
    }
  }

  async handleIncomingUrl(url: string): Promise<void> {
    const parsed = parseNativeUrl(url)
    if (parsed.kind === 'route') {
      this.lastError.set(null)
      await this.router.navigateByUrl(parsed.path)
      return
    }
    const existing = this.callbacks.get(url)
    if (existing) return existing
    this.lastError.set(null)
    const result = this.handleAuth(url)
    this.callbacks.set(url, result)
    // Keep only a bounded in-memory deduplication window; no callback tokens on disk.
    if (this.callbacks.size > 10) this.callbacks.delete(this.callbacks.keys().next().value!)
    return result
  }

  private async handleAuth(url: string): Promise<void> {
    await this.auth.ensureReady()
    try {
      const target = await this.auth.handleNativeCallback(url)
      await this.router.navigateByUrl(target, { replaceUrl: true })
    } catch (error) {
      this.reportError(error)
      await this.router.navigateByUrl('/login', { replaceUrl: true })
    }
  }

  private reportError(error: unknown): void {
    this.lastError.set(error instanceof Error ? error.message : 'Impossible de traiter le lien LifeOS')
  }
}
