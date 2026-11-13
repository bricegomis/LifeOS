import { Component } from '@angular/core'
import { RouterLink, RouterLinkActive } from '@angular/router'

/** Sport sections on the phone, where the module has a single tab; the desktop menu lists them. */
@Component({
  selector: 'app-sport-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="sport-subnav mobile-only" aria-label="Sections du sport">
      @for (link of links; track link.path) {
        <a [routerLink]="link.path" routerLinkActive="is-active" [routerLinkActiveOptions]="{ exact: true }" ariaCurrentWhenActive="page">{{ link.label }}</a>
      }
    </nav>
  `,
  styleUrl: './sports.css',
})
export class SportNavComponent {
  readonly links = [
    { path: '/sports', label: 'Aujourd’hui' },
    { path: '/sports/history', label: 'Historique' },
    { path: '/sports/progress', label: 'Progression' },
    { path: '/sports/settings', label: 'Réglages' },
  ]
}
