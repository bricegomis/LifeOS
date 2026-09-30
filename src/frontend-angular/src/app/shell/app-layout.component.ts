import { Component, inject, OnInit, signal } from '@angular/core'
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'
import { firstValueFrom, timeout } from 'rxjs'
import { AuthService } from '@/app/core/auth/auth.service'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { BuildInfoDto } from '@/app/core/api/api.models'
import { apiBaseUrl, buildId } from '@/environments/environment'

interface NavigationItem {
  path: string
  title: string
  mobileTitle: string
  icon: string
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-layout.component.html',
})
export class AppLayoutComponent implements OnInit {
  readonly auth = inject(AuthService)
  private readonly router = inject(Router)
  private readonly api = inject(LifeosApiService)
  readonly buildId = buildId
  readonly apiBuildId = signal(apiBaseUrl ? 'chargement…' : 'non configurée')
  readonly signOutError = signal('')

  readonly navigation: NavigationItem[] = [
    { path: '/', title: "Aujourd'hui", mobileTitle: 'Aujourd’hui', icon: 'pi pi-sun' },
    { path: '/planning', title: 'Planning', mobileTitle: 'Semaine', icon: 'pi pi-calendar' },
    { path: '/recipes', title: 'Recettes', mobileTitle: 'Recettes', icon: 'pi pi-book' },
    { path: '/meals', title: 'Repas composés', mobileTitle: 'Repas', icon: 'pi pi-objects-column' },
    { path: '/foods', title: 'Aliments', mobileTitle: 'Aliments', icon: 'pi pi-apple' },
    { path: '/stores', title: 'Magasins et articles', mobileTitle: 'Achats', icon: 'pi pi-shop' },
    { path: '/stock', title: 'Stock et courses', mobileTitle: 'Courses', icon: 'pi pi-shopping-cart' },
    { path: '/library', title: 'Catalogue partagé', mobileTitle: 'Catalogue', icon: 'pi pi-list' },
    { path: '/settings', title: 'Réglages', mobileTitle: 'Réglages', icon: 'pi pi-cog' },
  ]

  readonly mobileNavigation = this.navigation.filter((item) =>
    ['/', '/planning', '/recipes', '/stock', '/settings'].includes(item.path),
  )

  displayBuildId(value: string): string {
    return /^[a-f0-9]{13,}$/i.test(value) ? value.slice(0, 12) : value
  }

  async ngOnInit(): Promise<void> {
    if (!apiBaseUrl) return

    try {
      const version = await firstValueFrom(
        this.api.get<BuildInfoDto>('/version').pipe(timeout({ first: 5000 })),
      )
      if (version.component !== 'api' || !version.buildId) {
        throw new Error('Réponse de version API invalide')
      }
      this.apiBuildId.set(version.buildId)
    } catch {
      this.apiBuildId.set('indisponible')
    }
  }

  async signOut(): Promise<void> {
    this.signOutError.set('')
    try {
      await this.auth.signOut()
      await this.router.navigateByUrl('/login')
    } catch (error) {
      this.signOutError.set(
        error instanceof Error ? error.message : 'Impossible de terminer la déconnexion.',
      )
    }
  }
}
