import { DestroyRef, inject, Injectable } from '@angular/core'
import { NavigationEnd, Router } from '@angular/router'
import { Capacitor } from '@capacitor/core'
import { isLaunchHash, LAST_ROUTE_KEY, restorableRoute, serializeLastRoute } from '@/app/core/navigation/last-route'

// Read before the router rewrites the address: was the app opened on its start address?
const openedOnStartAddress = typeof window !== 'undefined' && isLaunchHash(window.location.hash)

/** Installed on the home screen (web app) or running as the iPhone app, where iOS reloads the app from its start address. */
function runsAsInstalledApp(): boolean {
  if (Capacitor.isNativePlatform()) return true
  if (typeof window === 'undefined') return false
  const legacyStandalone = (navigator as Navigator & { standalone?: boolean }).standalone === true
  return legacyStandalone || window.matchMedia?.('(display-mode: standalone)').matches === true
}

/**
 * Reopens the installed app on the page left last (within a few hours), like a native app:
 * iOS often relaunches a home screen app from scratch after a while in the background.
 */
@Injectable({ providedIn: 'root' })
export class LastRouteService {
  private readonly router = inject(Router)
  private readonly destroyRef = inject(DestroyRef)
  private launchTarget: string | null = openedOnStartAddress && runsAsInstalledApp() ? this.read() : null

  initListeners(): void {
    const subscription = this.router.events.subscribe((event) => {
      if (!(event instanceof NavigationEnd)) return
      const raw = serializeLastRoute(event.urlAfterRedirects, Date.now())
      if (raw) this.write(raw)
    })
    this.destroyRef.onDestroy(() => subscription.unsubscribe())
  }

  /** The page to reopen at launch, once. */
  takeLaunchTarget(): string | null {
    const target = this.launchTarget
    this.launchTarget = null
    return target
  }

  private read(): string | null {
    try { return restorableRoute(localStorage.getItem(LAST_ROUTE_KEY), Date.now()) } catch { return null }
  }

  private write(raw: string): void {
    try { localStorage.setItem(LAST_ROUTE_KEY, raw) } catch { /* private mode: nothing to reopen */ }
  }
}
