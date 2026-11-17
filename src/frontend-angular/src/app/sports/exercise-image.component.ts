import { Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core'
import { MatIconModule } from '@angular/material/icon'
import { firstValueFrom } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'

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
  private readonly api = inject(LifeosApiService)
  readonly exerciseId = input.required<string>()
  readonly hasImage = input(false)
  /** Changes when the image was replaced, to reload it. */
  readonly version = input(0)
  readonly alt = input('')
  readonly size = input(56)
  /** Full height of a card, `size` wide. */
  readonly fill = input(false)
  readonly url = signal<string | null>(null)
  private loadId = 0

  constructor() {
    effect(() => {
      const id = this.exerciseId(); const hasImage = this.hasImage(); this.version()
      // Untracked: load() reads `url` synchronously, which would otherwise rerun this effect forever.
      untracked(() => void this.load(id, hasImage))
    })
    inject(DestroyRef).onDestroy(() => this.release())
  }

  private async load(id: string, hasImage: boolean): Promise<void> {
    const current = ++this.loadId
    this.release()
    if (!hasImage) return
    try {
      const blob = await firstValueFrom(this.api.getBlob(`/sport/exercises/${encodeURIComponent(id)}/image`))
      if (current === this.loadId) this.url.set(URL.createObjectURL(blob))
    } catch {
      if (current === this.loadId) this.url.set(null)
    }
  }

  private release(): void {
    const url = this.url()
    if (url) URL.revokeObjectURL(url)
    this.url.set(null)
  }
}
