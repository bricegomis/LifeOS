import { Component, inject, signal } from '@angular/core'
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'

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
export class AppLayoutComponent {
  readonly auth = inject(AuthService)
  private readonly router = inject(Router)
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
