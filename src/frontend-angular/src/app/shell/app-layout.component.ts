import { BreakpointObserver } from '@angular/cdk/layout'
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { MatSidenavModule } from '@angular/material/sidenav'
import { MatToolbarModule } from '@angular/material/toolbar'
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'
import { filter, firstValueFrom, map } from 'rxjs'
import { AuthService } from '@/app/core/auth/auth.service'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { NativeSyncService } from '@/app/core/native/native-sync.service'
import { HANDSET_QUERY } from '@/app/shared/ui/breakpoints'
import { currentEntry, NAVIGATION_BLOCKS, pageTitle, readMenuOpen, writeMenuOpen } from './navigation'


@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatSidenavModule, MatToolbarModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-layout.component.html',
  styleUrl: './app-layout.component.css',
})
export class AppLayoutComponent implements OnInit {
  readonly auth = inject(AuthService)
  private readonly router = inject(Router)
  private readonly api = inject(LifeosApiService)
  private readonly nativeSync = inject(NativeSyncService)
  readonly blocks = NAVIGATION_BLOCKS
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })
  /** Desktop menu shown or hidden (remembered); the phone panel opens on demand. */
  readonly desktopOpen = signal(readMenuOpen())
  readonly phoneOpen = signal(false)
  readonly menuOpened = computed(() => this.handset() ? this.phoneOpen() : this.desktopOpen())
  private readonly url = signal(this.router.url)
  readonly title = computed(() => pageTitle(this.url()))
  /** Blocks unfolded by hand; the block of the current page is always open on arrival. */
  readonly openBlocks = signal(new Set<string>())
  readonly pendingReceipts = signal(0)
  readonly signOutError = signal('')
  readonly email = computed(() => this.auth.user()?.email ?? '')
  readonly initial = computed(() => (this.email()[0] ?? 'L').toLocaleUpperCase('fr'))

  constructor() {
    this.openCurrentBlock()
    const subscription = this.router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(() => {
      this.url.set(this.router.url)
      this.openCurrentBlock()
      this.phoneOpen.set(false)
      void this.refreshPendingReceipts()
    })
    inject(DestroyRef).onDestroy(() => subscription.unsubscribe())
  }

  ngOnInit(): void {
    void this.nativeSync.initListeners()
    void this.nativeSync.syncNativeState()
    void this.refreshPendingReceipts()
  }

  isOpen(blockId: string): boolean { return this.openBlocks().has(blockId) }

  toggleBlock(blockId: string): void {
    this.openBlocks.update(open => {
      const next = new Set(open)
      if (next.has(blockId)) next.delete(blockId)
      else next.add(blockId)
      return next
    })
  }

  openMenu(): void {
    if (this.handset()) this.phoneOpen.set(true)
    else this.setDesktop(true)
  }

  closeMenu(): void {
    if (this.handset()) this.phoneOpen.set(false)
    else this.setDesktop(false)
  }

  /** The panel was closed by its backdrop or the Escape key. */
  menuClosed(): void {
    if (this.handset()) this.phoneOpen.set(false)
  }

  private setDesktop(open: boolean): void {
    this.desktopOpen.set(open)
    writeMenuOpen(open)
  }

  private openCurrentBlock(): void {
    const block = currentEntry(this.router.url)?.block.id
    if (block && !this.openBlocks().has(block)) this.openBlocks.update(open => new Set([...open, block]))
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
