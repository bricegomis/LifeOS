import { Component, inject, OnInit, signal } from '@angular/core'
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'
import { firstValueFrom, timeout } from 'rxjs'
import { AuthService } from '@/app/core/auth/auth.service'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { parseApiBuildInfo, type BuildInfo } from '@/app/core/build-info'
import { BuildVersionComponent } from '@/app/shared/build-version.component'
import { apiBaseUrl, buildInfo } from '@/environments/environment'
import { NativeSyncService } from '@/app/core/native/native-sync.service'

interface NavigationItem {
  path: string
  title: string
  mobileTitle: string
  icon: string
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, BuildVersionComponent],
  templateUrl: './app-layout.component.html',
})
export class AppLayoutComponent implements OnInit {
  readonly auth = inject(AuthService)
  private readonly router = inject(Router)
  private readonly api = inject(LifeosApiService)
  private readonly nativeSync = inject(NativeSyncService)
  readonly buildInfo = buildInfo
  readonly apiBuildInfo = signal<BuildInfo | null>(null)
  readonly apiBuildStatus = signal(apiBaseUrl ? 'chargement…' : 'non configurée')
  readonly signOutError = signal('')

  readonly navigation: NavigationItem[] = [
    { path: '/', title: "Aujourd'hui", mobileTitle: 'Aujourd’hui', icon: 'pi pi-sun' },
    { path: '/planning', title: 'Semainier', mobileTitle: 'Semaine', icon: 'pi pi-calendar' },
    { path: '/recipes', title: 'Recettes', mobileTitle: 'Recettes', icon: 'pi pi-book' },
    { path: '/foods', title: 'Aliments', mobileTitle: 'Aliments', icon: 'pi pi-apple' },
    { path: '/sports', title: 'Séances sportives', mobileTitle: 'Sport', icon: 'pi pi-bolt' },
    { path: '/stores', title: 'Magasins', mobileTitle: 'Magasins', icon: 'pi pi-shop' },
    {
      path: '/tickets',
      title: 'Tickets',
      mobileTitle: 'Tickets',
      icon: 'pi pi-shopping-cart',
    },
    { path: '/settings', title: 'Réglages', mobileTitle: 'Réglages', icon: 'pi pi-cog' },
  ]

  readonly mobileNavigation = this.navigation.filter((item) =>
    ['/', '/planning', '/foods', '/recipes', '/sports'].includes(item.path),
  )

  async ngOnInit(): Promise<void> {
    void this.nativeSync.initListeners()
    void this.nativeSync.syncNativeState()

    if (!apiBaseUrl) return

    try {
      const version = await firstValueFrom(
        this.api.get<unknown>('/version').pipe(timeout({ first: 5000 })),
      )
      this.apiBuildInfo.set(parseApiBuildInfo(version))
    } catch {
      this.apiBuildStatus.set('indisponible')
    }
  }

  async signOut(): Promise<void> {
    this.signOutError.set('')
    let purgeError: unknown
    try {
      try { await this.nativeSync.purgeAll() } catch (error) { purgeError = error }
      await this.auth.signOut()
      await this.router.navigateByUrl('/login')
      if (purgeError) throw purgeError
    } catch (error) {
      this.signOutError.set(
        error instanceof Error ? error.message : 'Impossible de terminer la déconnexion.',
      )
    }
  }
}
