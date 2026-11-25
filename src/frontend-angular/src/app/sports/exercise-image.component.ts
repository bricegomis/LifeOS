import { Component, input } from '@angular/core'
import { MatIconModule } from '@angular/material/icon'
import { authenticatedImageUrl } from '@/app/shared/ui/authenticated-image'

/**
 * An exercise photo served by the API (authenticated, so loaded as a blob); an image icon without one
 * (ADR 0015: the photo goes with the exercise everywhere). With `fill`, it takes the full height of its card.
 */
@Component({
  selector: 'app-exercise-image',
  standalone: true,
  imports: [MatIconModule],
  host: { class: 'lo-media', '[class.is-fill]': 'fill()', '[style.width.px]': 'size()', '[style.height.px]': 'fill() ? null : size()' },
  template: `
    @if (url(); as source) {
      <img [src]="source" [alt]="alt()" />
    } @else {
      <span class="placeholder" role="img" [attr.aria-label]="alt() ? 'Pas de photo' : null" [attr.aria-hidden]="alt() ? null : true">
        <mat-icon svgIcon="image" />
      </span>
    }
  `,
  styles: [`
    :host { display: block; overflow: hidden; flex: 0 0 auto; border-radius: 8px; }
    :host(.is-fill) { min-height: 100%; align-self: stretch; border-radius: 0; }
    img { display: block; width: 100%; height: 100%; object-fit: cover; }
    .placeholder {
      display: flex; width: 100%; height: 100%; min-height: inherit; align-items: center; justify-content: center;
      background: var(--mat-sys-surface-container-high); color: var(--mat-sys-on-surface-variant);
    }
  `],
})
export class ExerciseImageComponent {
  readonly exerciseId = input.required<string>()
  readonly hasImage = input(false)
  /** Changes when the image was replaced, to reload it. */
  readonly version = input(0)
  readonly alt = input('')
  readonly size = input(56)
  /** Full height of a card, `size` wide. */
  readonly fill = input(false)
  readonly url = authenticatedImageUrl(() => {
    this.version()
    return this.hasImage() ? `/sport/exercises/${encodeURIComponent(this.exerciseId())}/image` : null
  })
}
