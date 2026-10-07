import { Component, inject } from '@angular/core'
import { RouterLink } from '@angular/router'
import { AuthService } from '@/app/core/auth/auth.service'
import { apiBaseUrl, buildId } from '@/environments/environment'

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="page-stack">
      <header class="page-hero"><div><h1>Réglages</h1><p>Compte et connexion du semainier manuel.</p></div></header>
      <section class="surface-card">
        <h2>Votre compte</h2>
        <p>{{ auth.user()?.email ?? 'Compte du foyer' }}</p>
        <p>Vos données sont sauvegardées par LifeOS API et isolées dans votre foyer. Supabase assure la connexion.</p>
        <dl><dt>Version interface</dt><dd>{{ buildId }}</dd><dt>API</dt><dd>{{ apiUrl || 'Non configurée' }}</dd></dl>
      </section>
      <section class="surface-card">
        <h2>Planifier sans configuration</h2>
        <p>La présence des enfants se règle sur chaque repas. Les horaires et les portions appartiennent uniquement à l’événement.</p>
        <p>Alternance, semaines types, règles et génération sont reportées. Leurs données historiques sont conservées, mais ne pilotent pas vos nouvelles semaines.</p>
        <a class="lifeos-button" routerLink="/planning">Ouvrir le semainier</a>
      </section>
    </section>
  `,
  styles: ['p { max-width: 70ch; line-height: 1.6; } dd { margin: 8px 0 20px; overflow-wrap: anywhere; }'],
})
export class SettingsPageComponent {
  readonly auth = inject(AuthService)
  readonly apiUrl = apiBaseUrl
  readonly buildId = buildId
}
