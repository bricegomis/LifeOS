import { Component, computed, input } from '@angular/core'
import { MatIconModule } from '@angular/material/icon'
import { authenticatedImageUrl } from '@/app/shared/ui/authenticated-image'

/**
 * A private health image (decrypted by the API, loaded as a blob); without one, the name's initial or
 * an icon on a tonal background (ADR 0018).
 */
@Component({
  selector: 'app-health-image',
  standalone: true,
  imports: [MatIconModule],
  host: { class: 'lo-media', '[style.width.px]': 'size()', '[style.height.px]': 'size()' },
  template: `
    @if (url(); as source) {
      <img [src]="source" [alt]="alt()" />
    } @else if (icon()) {
      <span class="fallback" role="img" [attr.aria-label]="alt() || null" [attr.aria-hidden]="alt() ? null : true"><mat-icon [svgIcon]="icon()" /></span>
    } @else {
      <span class="fallback" [style.font-size.px]="size() * 0.42" role="img" [attr.aria-label]="alt() ? 'Pas de photo, initiale ' + letter() : null"
        [attr.aria-hidden]="alt() ? null : true">{{ letter() }}</span>
    }
  `,
  styles: [`
    :host { display: block; overflow: hidden; flex: 0 0 auto; border-radius: 12px; }
    img { display: block; width: 100%; height: 100%; object-fit: cover; }
    .fallback {
      display: flex; width: 100%; height: 100%; align-items: center; justify-content: center; font-weight: 600;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container);
    }
  `],
})
export class HealthImageComponent {
  /** API path of the image, e.g. `/health/supplements/{id}/image`; none when there is no image. */
  readonly path = input<string | null>(null)
  readonly name = input('')
  /** Shown instead of the initial when there is no image. */
  readonly icon = input('')
  readonly version = input('')
  readonly alt = input('')
  readonly size = input(56)
  readonly letter = computed(() => (this.name().trim().charAt(0) || '?').toLocaleUpperCase('fr'))
  readonly url = authenticatedImageUrl(() => { this.version(); return this.path() })
}
