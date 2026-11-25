import { DestroyRef, effect, inject, signal, untracked, type Signal } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'

/**
 * Object URL of an image served by the API. The routes are authenticated, so the image is loaded as a
 * blob with the access token instead of an `<img src>` pointing at the API.
 *
 * `source` returns the API path, or null when there is no image; every signal it reads (an id, a version
 * that changes when the image is replaced) reloads the image. A late answer for a former path is dropped,
 * and each object URL is released once replaced or when the component is destroyed.
 * Must be called in an injection context (a component field or constructor).
 */
export function authenticatedImageUrl(source: () => string | null): Signal<string | null> {
  const api = inject(LifeosApiService)
  const url = signal<string | null>(null)
  let loadId = 0

  const release = () => {
    const current = untracked(url)
    if (current) URL.revokeObjectURL(current)
    url.set(null)
  }

  const load = async (path: string | null) => {
    const current = ++loadId
    release()
    if (!path) return
    try {
      const blob = await firstValueFrom(api.getBlob(path))
      if (current === loadId) url.set(URL.createObjectURL(blob))
    } catch {
      if (current === loadId) url.set(null)
    }
  }

  effect(() => {
    const path = source()
    // Untracked: load() reads and writes `url`, which would otherwise rerun this effect forever.
    untracked(() => void load(path))
  })
  inject(DestroyRef).onDestroy(() => { loadId++; release() })
  return url.asReadonly()
}
