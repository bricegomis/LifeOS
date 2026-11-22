import { Component, HostListener, inject, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { ActivatedRoute, Router } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'
import { validatedReturnPath } from '@/app/core/auth/auth-flow'

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [MatButtonModule],
  templateUrl: './login-page.component.html',
  styles: [`
    .login-page { display: grid; min-height: 100vh; place-items: center; padding: 32px 16px; background: var(--mat-sys-surface-container-low); }
    .login-panel { display: grid; width: min(100%, 36rem); gap: 24px; padding: clamp(20px, 4vw, 36px); box-shadow: var(--mat-sys-level1); }
    .login-copy { display: grid; gap: 12px; }
    .login-copy h1 { font: var(--mat-sys-display-small); }
    .eyebrow { color: var(--mat-sys-primary); font: var(--mat-sys-label-large); font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; }
    .login-panel button { height: 48px; }
  `],
})
export class LoginPageComponent {
  readonly auth = inject(AuthService)
  private readonly route = inject(ActivatedRoute)
  private readonly router = inject(Router)
  readonly googleSubmitting = signal(false)
  readonly message = signal('')
  readonly redirectPath: string

  constructor() {
    this.redirectPath = validatedReturnPath(this.route.snapshot.queryParamMap.get('redirect'))
    void this.redirectExistingSession()
  }

  @HostListener('window:pageshow')
  onPageShow(): void {
    this.googleSubmitting.set(false)
  }

  async continueWithGoogle(): Promise<void> {
    if (this.googleSubmitting() || !this.auth.ready()) return
    this.googleSubmitting.set(true)
    this.message.set('')
    try {
      await this.auth.signInWithGoogle(this.redirectPath)
    } catch (error) {
      this.message.set(error instanceof Error ? error.message : 'Impossible de lancer la connexion. Réessayez.')
      this.googleSubmitting.set(false)
    }
  }

  private async redirectExistingSession(): Promise<void> {
    await this.auth.ensureReady()
    if (this.auth.authenticated() && !this.auth.error()) {
      await this.router.navigateByUrl(this.redirectPath, { replaceUrl: true })
    }
  }
}
