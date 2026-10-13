import { Component, inject } from '@angular/core'
import { CookingSessionService } from '@/app/core/native/cooking-session.service'

@Component({
  selector: 'app-cooking-session-banner',
  standalone: true,
  template: `
    @if (cooking.currentSession(); as session) {
      <aside class="cooking-banner" aria-label="Session cuisine en cours">
        <div class="cooking-banner-content">
          <div class="cooking-banner-title">
            <span class="cooking-badge" [class.is-done]="session.isCompleted">
              {{ session.isCompleted ? '✓ Terminée' : '🍳 En cours' }}
            </span>
            <strong>{{ session.recipeTitle }}</strong>
            <small>{{ session.completedStepIndices.length }} / {{ session.steps.length }} étape{{ session.steps.length > 1 ? 's' : '' }}</small>
          </div>

          <div class="cooking-banner-steps">
            @for (step of session.steps; track $index) {
              <label class="cooking-step-checkbox">
                <input
                  type="checkbox"
                  [checked]="session.completedStepIndices.includes($index)"
                  (change)="cooking.toggleStep($index)"
                />
                <span [class.step-checked]="session.completedStepIndices.includes($index)">{{ step }}</span>
              </label>
            }
          </div>
        </div>

        <div class="cooking-banner-actions">
          @if (!session.isCompleted) {
            <button type="button" class="lifeos-button lifeos-button-sm" (click)="cooking.completeSession()">
              Terminer
            </button>
          }
          <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" (click)="cooking.cancelSession()">
            {{ session.isCompleted ? 'Fermer' : 'Annuler' }}
          </button>
        </div>
      </aside>
    }
  `,
  styles: [`
    .cooking-banner {
      display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 14px;
      padding: 16px 20px; border: 1px solid var(--lifeos-accent); border-radius: 16px;
      background: var(--lifeos-surface); box-shadow: 0 4px 18px rgb(53 109 79 / 12%);
      margin-bottom: 20px;
    }
    .cooking-banner-content { display: grid; gap: 10px; flex: 1 1 300px; min-width: 0; }
    .cooking-banner-title { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .cooking-badge {
      display: inline-flex; align-items: center; padding: 3px 8px; border-radius: 6px;
      font-size: .75rem; font-weight: 800; background: var(--lifeos-accent-soft); color: var(--lifeos-accent-strong);
    }
    .cooking-badge.is-done { background: #e6f4ea; color: #137333; }
    .cooking-banner-steps { display: grid; gap: 6px; margin-top: 4px; }
    .cooking-step-checkbox { display: flex; align-items: flex-start; gap: 8px; font-size: .88rem; cursor: pointer; }
    .cooking-step-checkbox input { margin-top: 3px; }
    .step-checked { text-decoration: line-through; color: var(--lifeos-text-soft); }
    .cooking-banner-actions { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }
  `],
})
export class CookingSessionBannerComponent {
  readonly cooking = inject(CookingSessionService)
}
