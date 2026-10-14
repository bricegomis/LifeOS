import { Component, Input, OnChanges, SimpleChanges, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import type { HouseholdGroupDto, MemberProfileDto, ReviewDto, SaveReviewRequest } from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'

type ReviewTarget = 'food' | 'recipe' | 'planned-line'

@Component({
  selector: 'app-household-reviews',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="household-reviews" aria-label="Avis du foyer">
      <h3>Avis du foyer</h3>
      @if (loading()) { <p role="status">Chargement des avis…</p> }
      @if (error()) { <p class="review-error" role="alert">{{ error() }}</p> }
      @if (reviews().length) {
        <ul class="review-list">
          @for (review of reviews(); track review.id) {
            <li>
              <div>
                <strong>{{ review.targetName ?? 'Cible indisponible' }}</strong>
                <span>{{ review.score === null ? 'Sans note' : review.score + '/10' }}</span>
                @if (review.comment) { <p>{{ review.comment }}</p> }
                <small>Saisi par le compte {{ review.createdByUserId }}</small>
              </div>
              <div class="review-actions">
                <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="busy()" (click)="edit(review)">Modifier</button>
                <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="busy()" (click)="remove(review)">Supprimer</button>
              </div>
            </li>
          }
        </ul>
      } @else if (!loading()) {
        <p>Aucun avis pour le moment. Les avis restent visibles dans votre foyer.</p>
      }

      @if (profiles.length || groups.length) {
        <form class="review-form" (ngSubmit)="save()">
          <label class="dialog-field">
            <span>Personne ou groupe</span>
            <select class="text-input" name="target" required [(ngModel)]="target">
              <option value="">Choisir</option>
              @for (profile of profiles; track profile.id) { <option [value]="'profile:' + profile.id">{{ profile.displayName }}</option> }
              @for (group of groups; track group.id) { <option [value]="'group:' + group.id">{{ group.name }} · groupe</option> }
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
          <div class="review-actions">
            <button class="lifeos-button lifeos-button-secondary" type="submit" [disabled]="busy()">
              {{ busy() ? 'Enregistrement…' : editingId() ? 'Enregistrer l’avis' : 'Ajouter un avis' }}
            </button>
            @if (editingId()) { <button class="lifeos-button lifeos-button-text" type="button" [disabled]="busy()" (click)="cancelEdit()">Annuler</button> }
          </div>
        </form>
      } @else {
        <p>Ajoutez d’abord une personne ou un groupe dans Réglages pour saisir un avis.</p>
      }
    </section>
  `,
  styles: [`
    .household-reviews { display: grid; gap: 12px; }
    .household-reviews h3 { margin: 0; font-size: 1.05rem; }
    .household-reviews p { margin: 6px 0 0; color: var(--lifeos-text-soft); }
    .review-list { display: grid; gap: 8px; margin: 0; padding: 0; list-style: none; }
    .review-list li { display: flex; flex-wrap: wrap; align-items: start; justify-content: space-between; gap: 10px; padding: 12px; border-radius: 10px; background: var(--lifeos-surface-soft); }
    .review-list span { margin-left: 10px; color: var(--lifeos-accent-strong); font-weight: 750; }
    .review-form { display: grid; gap: 12px; }
    .review-actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .review-error { color: #8f2017 !important; }
    @media (max-width: 560px) { .review-form .dialog-row { grid-template-columns: minmax(0, 1fr); } }
  `],
})
export class HouseholdReviewsComponent implements OnChanges {
  @Input({ required: true }) targetType: ReviewTarget = 'food'
  @Input({ required: true }) targetId = ''
  @Input() profiles: MemberProfileDto[] = []
  @Input() groups: HouseholdGroupDto[] = []
  private readonly api = inject(LifeosApiService)
  readonly reviews = signal<ReviewDto[]>([])
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly editingId = signal('')
  target = ''
  score: number | null = null
  comment = ''
  private initial = formSnapshot({ target: '', score: null, comment: '' })

  dirty(): boolean { return hasChanges(this.initial, { target: this.target, score: this.score, comment: this.comment }) }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['targetId'] || changes['targetType']) {
      this.cancelEdit()
      void this.load()
    }
  }

  async load(): Promise<void> {
    if (!this.targetId) { this.reviews.set([]); return }
    this.loading.set(true)
    this.error.set('')
    try {
      this.reviews.set(await firstValueFrom(this.api.get<ReviewDto[]>(this.path())))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  edit(review: ReviewDto): void {
    this.editingId.set(review.id)
    this.target = review.memberProfileId ? `profile:${review.memberProfileId}` : `group:${review.householdGroupId}`
    this.score = review.score
    this.comment = review.comment ?? ''
    this.initial = formSnapshot({ target: this.target, score: this.score, comment: this.comment })
    this.error.set('')
  }

  cancelEdit(): void {
    this.editingId.set('')
    this.target = ''
    this.score = null
    this.comment = ''
    this.initial = formSnapshot({ target: this.target, score: this.score, comment: this.comment })
  }

  async save(): Promise<void> {
    if (this.busy()) return
    const [kind, id] = this.target.split(':')
    const text = this.comment.trim()
    if (!id || (kind !== 'profile' && kind !== 'group') || (this.score === null && !text)
      || (this.score !== null && (!Number.isInteger(this.score) || this.score < 0 || this.score > 10))) {
      this.error.set('Choisissez une personne ou un groupe et renseignez une note de 0 à 10 ou un commentaire.')
      return
    }
    const body: SaveReviewRequest = {
      memberProfileId: kind === 'profile' ? id : null,
      householdGroupId: kind === 'group' ? id : null,
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
      this.cancelEdit()
      await this.load()
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async remove(review: ReviewDto): Promise<void> {
    if (this.busy() || !window.confirm(`Supprimer l’avis de « ${review.targetName ?? 'cette cible'} » ?`)) return
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
    return `/reviews/${this.targetType === 'planned-line' ? 'planned-lines' : `${this.targetType}s`}/${encodeURIComponent(this.targetId)}`
  }
}
