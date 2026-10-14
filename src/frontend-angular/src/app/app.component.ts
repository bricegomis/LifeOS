import { Component, inject } from '@angular/core'
import { RouterOutlet } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'
import { DeepLinkService } from '@/app/core/native/deep-link.service'
import { NativeSyncService } from '@/app/core/native/native-sync.service'

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class AppComponent {
  private readonly auth = inject(AuthService)
  private readonly deepLink = inject(DeepLinkService)
  private readonly nativeSync = inject(NativeSyncService)

  constructor() {
    void this.auth.initialize()
    void this.deepLink.initListeners()
    void this.nativeSync.initListeners()
  }
}
