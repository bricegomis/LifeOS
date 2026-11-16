import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'

/** An exercise photo served by the API (authenticated, so loaded as a blob); a hatched square without one. */
@Component({
  selector: 'app-exercise-image',
  standalone: true,
  template: `
    @if (url(); as source) {
      <img class="thumb" [src]="source" [alt]="alt()" [style.width.px]="size()" [style.height.px]="size()" />
    } @else {
      <span class="thumb exercise-placeholder" aria-hidden="true" [style.width.px]="size()" [style.height.px]="size()"></span>
    }
  `,
  styles: [`
    :host { display: contents; }
    .exercise-placeholder { border: 1px solid #d8ccb8; background: repeating-linear-gradient(135deg, #ece4d6 0 8px, #f4eddf 8px 16px); }
  `],
})
export class ExerciseImageComponent {
  private readonly api = inject(LifeosApiService)
  readonly exerciseId = input.required<string>()
  readonly hasImage = input(false)
  /** Changes when the image was replaced, to reload it. */
  readonly version = input(0)
  readonly alt = input('')
  readonly size = input(84)
  readonly url = signal<string | null>(null)
  private loadId = 0

  constructor() {
    effect(() => {
      const id = this.exerciseId(); const hasImage = this.hasImage(); this.version()
      void this.load(id, hasImage)
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
