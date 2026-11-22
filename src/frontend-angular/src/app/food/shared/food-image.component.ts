import { Component, computed, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { tintFor } from './tint'

/**
 * A food or recipe photo served by the API (authenticated, so loaded as a blob). Without a
 * photo, a tint derived from the name keeps lists readable.
 */
@Component({
  selector: 'app-food-image',
  standalone: true,
  template: `
    @if (url(); as source) {
      <img class="lo-thumb" [src]="source" [alt]="alt()" [style.width]="width()" [style.height]="height()" />
    } @else {
      <span class="lo-thumb placeholder" [attr.role]="alt() ? 'img' : null" [attr.aria-label]="alt() || null"
        [attr.aria-hidden]="alt() ? null : 'true'" [style.width]="width()" [style.height]="height()" [style.background]="tint()">
        @if (label()) { <span>{{ label() }}</span> }
      </span>
    }
  `,
  styles: [`
    :host { display: contents; }
    .placeholder { display: flex; align-items: flex-end; padding: 10px 12px; color: var(--mat-sys-on-surface-variant); font-size: 12px; font-weight: 600; }
  `],
})
export class FoodImageComponent {
  private readonly api = inject(LifeosApiService)
  /** `/foods/{id}/image` or `/recipes/{id}/image`. */
  readonly path = input.required<string>()
  readonly hasImage = input(false)
  /** Changes when the image may have changed (updatedAt), to reload it. */
  readonly version = input('')
  readonly name = input('')
  readonly alt = input('')
  readonly label = input('')
  readonly width = input('48px')
  readonly height = input('48px')
  readonly url = signal<string | null>(null)
  /** Warm tint of the name; in the dark scheme it is mixed into the dark surface so the label stays readable. */
  readonly tint = computed(() => {
    const tint = tintFor(this.name())
    return `light-dark(${tint}, color-mix(in srgb, ${tint} 30%, var(--mat-sys-surface-container-high)))`
  })
  private loadId = 0

  constructor() {
    effect(() => {
      const path = this.path(); const hasImage = this.hasImage(); this.version()
      // Untracked: load() reads `url` synchronously, which would otherwise rerun this effect forever.
      untracked(() => void this.load(path, hasImage))
    })
    inject(DestroyRef).onDestroy(() => this.release())
  }

  private async load(path: string, hasImage: boolean): Promise<void> {
    const current = ++this.loadId
    this.release()
    if (!hasImage) return
    try {
      const blob = await firstValueFrom(this.api.getBlob(path))
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
