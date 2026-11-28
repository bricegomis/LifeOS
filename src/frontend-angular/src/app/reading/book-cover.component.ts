import { Component, computed, input } from '@angular/core'
import { authenticatedImageUrl } from '@/app/shared/ui/authenticated-image'
import { initial } from './reading-rules'

/**
 * A book cover served by the API (authenticated, so loaded as a blob), at the 2:3 ratio of a book; without one, the
 * title on a tonal background (ADR 0022).
 */
@Component({
  selector: 'app-book-cover',
  standalone: true,
  host: { class: 'lo-media', '[style.width]': 'width()', '[style.aspect-ratio]': '"2 / 3"' },
  template: `
    @if (url(); as source) {
      <img [src]="source" [alt]="alt()" />
    } @else if (showTitle()) {
      <span class="blank" [attr.aria-hidden]="alt() ? null : true" [attr.role]="alt() ? 'img' : null" [attr.aria-label]="alt() || null">
        <span class="blank-title">{{ title() }}</span>
      </span>
    } @else {
      <span class="blank is-small" aria-hidden="true">{{ letter() }}</span>
    }
  `,
  styles: [`
    :host { display: block; overflow: hidden; flex: 0 0 auto; border-radius: 8px; }
    img { display: block; width: 100%; height: 100%; object-fit: cover; }
    .blank {
      display: flex; box-sizing: border-box; width: 100%; height: 100%; align-items: flex-end; padding: 10px; overflow: hidden;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); font-weight: 700; line-height: 1.2;
    }
    .blank-title { display: -webkit-box; overflow: hidden; -webkit-box-orient: vertical; -webkit-line-clamp: 5; overflow-wrap: anywhere; font-size: 13px; }
    .blank.is-small { align-items: center; justify-content: center; padding: 0; font-size: 18px; }
  `],
})
export class BookCoverComponent {
  readonly bookId = input.required<string>()
  readonly hasImage = input(false)
  readonly title = input('')
  /** Changes when the cover was replaced, to reload it. */
  readonly version = input('')
  readonly alt = input('')
  /** CSS width; the height follows the 2:3 ratio. */
  readonly width = input('100%')
  /** The title on a blank cover; the initial alone on small ones. */
  readonly showTitle = input(true)
  readonly letter = computed(() => initial(this.title()))
  readonly url = authenticatedImageUrl(() => {
    this.version()
    return this.hasImage() ? `/reading/books/${encodeURIComponent(this.bookId())}/image` : null
  })
}
