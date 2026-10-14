import { Injectable } from '@angular/core'

interface ReviewDraft {
  busy(): boolean
  dirty(): boolean
}

@Injectable({ providedIn: 'root' })
export class ReviewDraftsService {
  private readonly drafts = new Set<ReviewDraft>()

  register(draft: ReviewDraft): () => void {
    this.drafts.add(draft)
    return () => { this.drafts.delete(draft) }
  }

  canLeave(): boolean {
    const drafts = [...this.drafts]
    return !drafts.some(draft => draft.busy())
      && (!drafts.some(draft => draft.dirty()) || window.confirm('Abandonner les avis non enregistrés ?'))
  }
}
