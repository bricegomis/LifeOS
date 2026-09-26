import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { ActivatedRoute, Router } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login-page.component.html',
  styles: [`
    .login-page { min-height: 100vh; display: grid; place-items: center; padding: 2rem; background: var(--lifeos-bg); }
    .login-panel { width: min(100%, 36rem); display: grid; gap: 1.5rem; padding: clamp(1.25rem, 4vw, 2.2rem); border: 1px solid var(--lifeos-line); border-radius: 24px; background: var(--lifeos-surface); box-shadow: var(--lifeos-shadow); }
    .login-copy h1 { margin: 0; font-size: clamp(2rem, 5vw, 3rem); }
    .login-copy p:last-child { color: var(--lifeos-text-soft); line-height: 1.6; }
    .login-form { display: grid; gap: 1rem; }
    .login-field { display: grid; gap: .45rem; font-weight: 700; }
    .login-field input { min-height: 2.8rem; padding: .7rem .8rem; border: 1px solid var(--lifeos-line); border-radius: 9px; background: white; font: inherit; }
    .login-hint, .login-feedback { margin: 0; color: var(--lifeos-text-soft); }
    .login-feedback { color: var(--lifeos-accent); font-weight: 700; }
  `],
})
export class LoginPageComponent {
  readonly auth = inject(AuthService)
  private readonly route = inject(ActivatedRoute)
  private readonly router = inject(Router)
  readonly email = signal('')
  readonly submitting = signal(false)
  readonly message = signal('')
  readonly redirectPath: string

  constructor() {
    this.redirectPath = this.route.snapshot.queryParamMap.get('redirect') || '/'
    void this.redirectExistingSession()
  }

  get emailValue(): string {
    return this.email()
  }

  set emailValue(value: string) {
    this.email.set(value)
  }

  async sendLink(): Promise<void> {
    const email = this.email().trim()
    if (!email) {
      this.message.set('Saisissez une adresse e-mail valide.')
      return
    }
    this.submitting.set(true)
    this.message.set('')
    try {
      const callback = `/login?redirect=${encodeURIComponent(this.redirectPath)}`
      await this.auth.sendMagicLink(email, callback)
      this.message.set('Un lien de connexion a été envoyé.')
    } catch (error) {
      this.message.set(error instanceof Error ? error.message : 'Impossible d’envoyer le lien de connexion.')
    } finally {
      this.submitting.set(false)
    }
  }

  private async redirectExistingSession(): Promise<void> {
    await this.auth.ensureReady()
    if (this.auth.authenticated()) {
      await this.router.navigateByUrl(this.redirectPath, { replaceUrl: true })
    }
  }
}
