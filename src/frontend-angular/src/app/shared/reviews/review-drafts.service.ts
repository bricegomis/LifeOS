import { Injectable, inject } from '@angular/core'
import { ConfirmService } from '@/app/shared/ui/confirm'

interface ReviewDraft {
  busy(): boolean
  dirty(): boolean
}

@Injectable({ providedIn: 'root' })
export class ReviewDraftsService {
  private readonly drafts = new Set<ReviewDraft>()
  private readonly confirm = inject(ConfirmService)

  register(draft: ReviewDraft): () => void {
    this.drafts.add(draft)
    return () => { this.drafts.delete(draft) }
  }

  async canLeave(): Promise<boolean> {
    const drafts = [...this.drafts]
    if (drafts.some(draft => draft.busy())) return false
    return !drafts.some(draft => draft.dirty())
      || this.confirm.ask({ message: 'Abandonner les avis non enregistrés ?', confirmLabel: 'Abandonner', cancelLabel: 'Rester', danger: true })
  }
}
