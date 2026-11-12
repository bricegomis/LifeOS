import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core'
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'
import { filter, firstValueFrom } from 'rxjs'
import { AuthService } from '@/app/core/auth/auth.service'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { NativeSyncService } from '@/app/core/native/native-sync.service'
import { NAVIGATION_BLOCKS, MOBILE_TABS, readMenuOpen, writeMenuOpen } from './navigation'

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-layout.component.html',
  styleUrl: './app-layout.component.css',
})
export class AppLayoutComponent implements OnInit {
  readonly auth = inject(AuthService)
  private readonly router = inject(Router)
  private readonly api = inject(LifeosApiService)
  private readonly nativeSync = inject(NativeSyncService)
  readonly blocks = NAVIGATION_BLOCKS
  readonly tabs = MOBILE_TABS
  readonly menuOpen = signal(readMenuOpen())
  readonly pendingReceipts = signal(0)
  readonly signOutError = signal('')
  readonly email = computed(() => this.auth.user()?.email ?? '')
  readonly initial = computed(() => (this.email()[0] ?? 'L').toLocaleUpperCase('fr'))

  constructor() {
    const subscription = this.router.events.pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => void this.refreshPendingReceipts())
    inject(DestroyRef).onDestroy(() => subscription.unsubscribe())
  }

  ngOnInit(): void {
    void this.nativeSync.initListeners()
    void this.nativeSync.syncNativeState()
    void this.refreshPendingReceipts()
  }

  toggleMenu(): void {
    this.menuOpen.update(open => !open)
    writeMenuOpen(this.menuOpen())
  }

  /** Badge of the Tickets entry: receipts still waiting for their prices. */
  private async refreshPendingReceipts(): Promise<void> {
    try {
      const receipts = await firstValueFrom(this.api.get<{ status: string }[]>('/receipts'))
      this.pendingReceipts.set(receipts.filter(receipt => receipt.status === 'pending').length)
    } catch {
      this.pendingReceipts.set(0)
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
      this.signOutError.set(error instanceof Error ? error.message : 'Impossible de terminer la déconnexion.')
    }
  }
}
