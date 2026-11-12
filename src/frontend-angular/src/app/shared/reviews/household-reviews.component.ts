import { Component, DestroyRef, HostListener, Input, OnChanges, SimpleChanges, inject, output, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import type { MemberProfileDto, ReviewDto, SaveReviewRequest } from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { ReviewDraftsService } from './review-drafts.service'

type ReviewTarget = 'food' | 'recipe'
let nextReviewFormId = 0

@Component({
  selector: 'app-household-reviews',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent],
  template: `
    <section class="household-reviews" aria-label="Avis du foyer">
      <h2>Avis du foyer</h2>
      @if (loading()) { <p role="status">Chargement des avis…</p> }
      @if (error() && !editorOpen()) { <p class="review-error" role="alert">{{ error() }}</p> }
      @if (reviews().length) {
        <ul class="review-list">
          @for (review of reviews(); track review.id) {
            <li>
              <strong>{{ review.profileName ?? 'Profil supprimé' }}</strong>
              <span class="review-score">{{ review.score === null ? 'Non noté' : review.score + '/10' }}</span>
              <span class="review-comment">{{ review.comment }}</span>
              <div class="review-actions">
                <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="busy()" (click)="edit(review, $event)">Modifier</button>
                <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="busy()" (click)="remove(review)">Supprimer</button>
              </div>
            </li>
          }
        </ul>
      } @else if (!loading()) {
        <p>Aucun avis pour le moment. Les avis restent visibles dans votre foyer.</p>
      }

      @if (profiles.length) {
        <div class="review-actions">
          <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="add($event)">Ajouter un avis</button>
        </div>
      } @else {
        <p>Ajoutez d’abord un profil dans Paramètres pour saisir un avis.</p>
      }
    </section>
    <app-lifeos-dialog [open]="editorOpen()" [heading]="editingId() ? 'Modifier l’avis' : 'Ajouter un avis'"
      [centered]="true" [formId]="formId" submitLabel="Enregistrer l’avis"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [restoreFocusTo]="editorTrigger" (dismissed)="cancelEdit()">
      <form [id]="formId" class="review-form" ngNativeValidate (ngSubmit)="save()" (submit)="$event.stopPropagation()">
        <fieldset class="review-fields" [disabled]="busy()">
          <label class="dialog-field">
            <span>Profil</span>
            <select class="text-input" name="target" required [disabled]="!!editingId()" [(ngModel)]="target">
              <option value="">Choisir</option>
              @for (profile of profiles; track profile.id) { <option [value]="profile.id">{{ profile.displayName }}</option> }
            </select>
          </label>
          <div class="dialog-row">
            <label class="dialog-field">
              <span>Note (0 à 10, facultative)</span>
              <input class="text-input" name="score" type="number" min="0" max="10" step="1" [(ngModel)]="score" />
            </label>
            <label class="dialog-field">
              <span>Commentaire (facultatif)</span>
              <textarea class="text-input" name="comment" maxlength="2000" rows="2" [(ngModel)]="comment"></textarea>
            </label>
          </div>
        </fieldset>
      </form>
    </app-lifeos-dialog>
  `,
  styles: [`
    .household-reviews { display: grid; gap: 12px; }
    .household-reviews h2 { margin: 0; font-size: 17px; font-weight: 600; }
    .household-reviews p { margin: 0; color: var(--lifeos-text-soft); }
    .review-list { display: grid; margin: 0; padding: 0; list-style: none; }
    .review-list li { display: grid; grid-template-columns: 110px 80px minmax(0, 1fr) auto; align-items: center; gap: 12px; padding: 10px 0; border-top: 1px solid var(--lifeos-line); }
    .review-score { font-weight: 600; }
    .review-comment { color: var(--lifeos-text-soft); }
    @media (max-width: 560px) { .review-list li { grid-template-columns: minmax(0, 1fr) auto; } .review-comment, .review-list .review-actions { grid-column: 1 / -1; } }
    .review-form, .review-fields { display: grid; gap: 12px; min-width: 0; }
    .review-fields { margin: 0; padding: 0; border: 0; }
    .review-actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .review-error { color: #8f2017 !important; }
    @media (max-width: 560px) { .review-form .dialog-row { grid-template-columns: minmax(0, 1fr); } }
  `],
})
export class HouseholdReviewsComponent implements OnChanges {
  @Input({ required: true }) targetType: ReviewTarget = 'food'
  @Input({ required: true }) targetId = ''
  @Input() profiles: MemberProfileDto[] = []
  private readonly api = inject(LifeosApiService)
  readonly reviews = signal<ReviewDto[]>([])
  readonly reviewsLoaded = output<ReviewDto[]>()
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly editingId = signal('')
  readonly editorOpen = signal(false)
  readonly formId = `household-review-form-${++nextReviewFormId}`
  editorTrigger: HTMLElement | null = null
  target = ''
  score: number | null = null
  comment = ''
  private initial = formSnapshot({ target: '', score: null, comment: '' })
  private loadVersion = 0

  constructor() {
    inject(DestroyRef).onDestroy(inject(ReviewDraftsService).register(this))
  }

  dirty(): boolean { return hasChanges(this.initial, { target: this.target, score: this.score, comment: this.comment }) }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.busy() || this.dirty()) { event.preventDefault(); event.returnValue = '' }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['targetId'] || changes['targetType']) {
      this.cancelEdit()
      void this.load()
    }
  }

  async load(): Promise<void> {
    const version = ++this.loadVersion
    if (!this.targetId) { this.reviews.set([]); this.loading.set(false); return }
    this.loading.set(true)
    this.error.set('')
    try {
      const reviews = await firstValueFrom(this.api.get<ReviewDto[]>(this.path()))
      if (version === this.loadVersion) {
        this.reviews.set(reviews)
        this.reviewsLoaded.emit(reviews)
      }
    } catch (error) {
      if (version === this.loadVersion) this.error.set(apiErrorMessage(error))
    } finally {
      if (version === this.loadVersion) this.loading.set(false)
    }
  }

  add(event?: Event): void {
    if (this.busy() || this.editorOpen()) return
    this.cancelEdit()
    this.openEditor(event)
  }

  edit(review: ReviewDto, event?: Event): void {
    if (this.busy() || this.editorOpen()) return
    this.editingId.set(review.id)
    this.target = review.memberProfileId
    this.score = review.score
    this.comment = review.comment ?? ''
    this.initial = formSnapshot({ target: this.target, score: this.score, comment: this.comment })
    this.openEditor(event)
  }

  private openEditor(event?: Event): void {
    this.editorTrigger = event?.currentTarget instanceof HTMLElement ? event.currentTarget : null
    this.error.set('')
    this.editorOpen.set(true)
  }

  cancelEdit(): void {
    this.editorOpen.set(false)
    this.error.set('')
    this.editingId.set('')
    this.target = ''
    this.score = null
    this.comment = ''
    this.initial = formSnapshot({ target: this.target, score: this.score, comment: this.comment })
  }

  async save(): Promise<void> {
    if (this.busy() || !this.editorOpen()) return
    const id = this.target
    const text = this.comment.trim()
    if (!id || (this.score === null && !text)
      || (this.score !== null && (!Number.isInteger(this.score) || this.score < 0 || this.score > 10))) {
      this.error.set('Choisissez un profil et renseignez une note de 0 à 10 ou un commentaire.')
      return
    }
    const body: SaveReviewRequest = {
      memberProfileId: id,
      score: this.score,
      comment: text || null,
    }
    const reviewId = this.editingId()
    this.busy.set(true)
    this.error.set('')
    try {
      const path = this.path()
      if (reviewId) await firstValueFrom(this.api.put(`${path}/${reviewId}`, { score: body.score, comment: body.comment }))
      else await firstValueFrom(this.api.post(path, body))
      await this.load()
      const refreshError = this.error()
      this.cancelEdit()
      this.error.set(refreshError)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async remove(review: ReviewDto): Promise<void> {
    if (this.busy() || !window.confirm(`Supprimer l’avis de « ${review.profileName ?? 'ce profil'} » ?`)) return
    this.busy.set(true)
    this.error.set('')
    try {
      await firstValueFrom(this.api.delete(`${this.path()}/${review.id}`))
      if (this.editingId() === review.id) this.cancelEdit()
      await this.load()
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  private path(): string {
    return `/reviews/${this.targetType}s/${encodeURIComponent(this.targetId)}`
  }
}
