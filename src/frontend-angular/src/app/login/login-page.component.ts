import { Component, HostListener, inject, signal } from '@angular/core'
import { ActivatedRoute, Router } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'
import { validatedReturnPath } from '@/app/core/auth/auth-flow'

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [],
  templateUrl: './login-page.component.html',
  styles: [`
    .login-page { min-height: 100vh; display: grid; place-items: center; padding: 2rem; background: var(--lifeos-bg); }
    .login-panel { width: min(100%, 36rem); display: grid; gap: 1.5rem; padding: clamp(1.25rem, 4vw, 2.2rem); border: 1px solid var(--lifeos-line); border-radius: 24px; background: var(--lifeos-surface); box-shadow: var(--lifeos-shadow); }
    .login-copy h1 { margin: 0; font-size: clamp(2rem, 5vw, 3rem); }
    .login-copy p:last-child { color: var(--lifeos-text-soft); line-height: 1.6; }
    .login-hint, .login-feedback { margin: 0; color: var(--lifeos-text-soft); }
    .login-feedback { color: var(--lifeos-accent); font-weight: 700; }
    .login-panel button { min-height: 2.8rem; justify-content: center; }
    .login-panel button:focus-visible { outline: 3px solid var(--lifeos-accent); outline-offset: 3px; }
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
