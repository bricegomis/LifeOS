import { Component, DestroyRef, HostListener, OnChanges, SimpleChanges, inject, input, output, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog, type MatDialogRef } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { firstValueFrom } from 'rxjs'
import type { MemberProfileDto, ReviewDto, SaveReviewRequest } from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ReviewDraftsService } from './review-drafts.service'
import { ReviewEditorDialogComponent } from './review-editor-dialog.component'
import { ConfirmService } from '@/app/shared/ui/confirm'

type ReviewTarget = 'food' | 'recipe'

/** Household reviews of a food or a recipe (ADR 0007): one per profile, optional score 0..10 and comment. */
@Component({
  selector: 'app-household-reviews',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <section class="reviews" aria-labelledby="reviews-title">
      <div class="lo-panel-head">
        <h2 id="reviews-title">Avis du foyer</h2>
        @if (profiles().length) {
          <button mat-stroked-button type="button" [disabled]="busy()" (click)="add()"><mat-icon svgIcon="rate_review" />Ajouter un avis</button>
        }
      </div>
      @if (loading()) { <p class="lo-status" role="status">Chargement des avis…</p> }
      @if (error() && !editorOpen()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (reviews().length) {
        <div class="lo-list">
          @for (review of reviews(); track review.id) {
            <div class="lo-row">
              <div class="lo-row-main">
                <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="person" /></span>
                <span class="lo-row-text">
                  <span class="lo-row-title">{{ review.profileName ?? 'Profil supprimé' }} · <strong>{{ review.score === null ? 'Non noté' : review.score + '/10' }}</strong></span>
                  @if (review.comment) { <span class="lo-row-sub">{{ review.comment }}</span> }
                </span>
              </div>
              <span class="lo-row-action">
                <button mat-icon-button type="button" [disabled]="busy()" (click)="edit(review)"
                  [attr.aria-label]="'Modifier l’avis de ' + (review.profileName ?? 'ce profil')" title="Modifier"><mat-icon svgIcon="edit" /></button>
                <button mat-icon-button type="button" [disabled]="busy()" (click)="remove(review)"
                  [attr.aria-label]="'Supprimer l’avis de ' + (review.profileName ?? 'ce profil')" title="Supprimer"><mat-icon svgIcon="delete" /></button>
              </span>
            </div>
          }
        </div>
      } @else if (!loading()) {
        <p class="lo-muted">Aucun avis pour le moment. Les avis restent visibles dans votre foyer.</p>
      }
      @if (!profiles().length) { <p class="lo-muted">Ajoutez d’abord un profil dans Paramètres pour saisir un avis.</p> }
    </section>
  `,
  styles: [`
    .reviews { display: flex; flex-direction: column; gap: 12px; }
    .reviews h2 { margin: 0; font: var(--mat-sys-title-medium); font-weight: 600; }
    .lo-row-action { display: flex; }
  `],
})
export class HouseholdReviewsComponent implements OnChanges {
  readonly targetType = input.required<ReviewTarget>()
  readonly targetId = input.required<string>()
  readonly profiles = input<MemberProfileDto[]>([])
  private readonly api = inject(LifeosApiService)
  private readonly confirm = inject(ConfirmService)
  private readonly dialog = inject(MatDialog)
  private editorRef: MatDialogRef<ReviewEditorDialogComponent> | null = null
  readonly reviews = signal<ReviewDto[]>([])
  readonly reviewsLoaded = output<ReviewDto[]>()
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly editingId = signal('')
  readonly editorOpen = signal(false)
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
    if (!this.targetId()) { this.reviews.set([]); this.loading.set(false); return }
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

  add(): void {
    if (this.busy() || this.editorOpen()) return
    this.cancelEdit()
    this.openEditor()
  }

  edit(review: ReviewDto): void {
    if (this.busy() || this.editorOpen()) return
    this.editingId.set(review.id)
    this.target = review.memberProfileId
    this.score = review.score
    this.comment = review.comment ?? ''
    this.initial = formSnapshot({ target: this.target, score: this.score, comment: this.comment })
    this.openEditor()
  }

  /** The window edits this component's draft; closing it (Annuler, Escape, backdrop) drops the draft. */
  private openEditor(): void {
    this.error.set('')
    this.editorOpen.set(true)
    const ref = openEditor<ReviewEditorDialogComponent, HouseholdReviewsComponent>(this.dialog, ReviewEditorDialogComponent, this)
    this.editorRef = ref
    ref.afterClosed().subscribe(() => {
      if (this.editorRef !== ref) return
      this.editorRef = null
      if (this.editorOpen()) this.cancelEdit()
    })
  }

  cancelEdit(): void {
    const ref = this.editorRef
    this.editorRef = null
    ref?.close()
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
    if (this.busy()
      || !await this.confirm.ask({ message: `Supprimer l’avis de « ${review.profileName ?? 'ce profil'} » ?`, confirmLabel: 'Supprimer', danger: true })) return
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
    return `/reviews/${this.targetType()}s/${encodeURIComponent(this.targetId())}`
  }
}
