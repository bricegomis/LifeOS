import { Component, computed, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { initial } from './task-format'

/**
 * A todo image served by the API (authenticated, so loaded as a blob); without one, the title's
 * initial on a tonal background (ADR 0017).
 */
@Component({
  selector: 'app-task-image',
  standalone: true,
  host: { class: 'lo-media', '[style.width.px]': 'size()', '[style.height.px]': 'size()' },
  template: `
    @if (url(); as source) {
      <img [src]="source" [alt]="alt()" />
    } @else {
      <span class="initial" [style.font-size.px]="size() * 0.42" role="img" [attr.aria-label]="alt() ? 'Pas de photo, initiale ' + letter() : null"
        [attr.aria-hidden]="alt() ? null : true">{{ letter() }}</span>
    }
  `,
  styles: [`
    :host { display: block; overflow: hidden; flex: 0 0 auto; border-radius: 12px; }
    img { display: block; width: 100%; height: 100%; object-fit: cover; }
    .initial {
      display: flex; width: 100%; height: 100%; align-items: center; justify-content: center; font-weight: 600;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container);
    }
  `],
})
export class TaskImageComponent {
  private readonly api = inject(LifeosApiService)
  readonly todoId = input.required<string>()
  readonly hasImage = input(false)
  readonly title = input('')
  /** Changes when the image was replaced, to reload it. */
  readonly version = input('')
  readonly alt = input('')
  readonly size = input(56)
  readonly letter = computed(() => initial(this.title()))
  readonly url = signal<string | null>(null)
  private loadId = 0

  constructor() {
    effect(() => {
      const id = this.todoId(); const hasImage = this.hasImage(); this.version()
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
      const blob = await firstValueFrom(this.api.getBlob(`/tasks/todos/${encodeURIComponent(id)}/image`))
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
