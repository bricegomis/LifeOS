import { Component, EventEmitter, Input, Output, signal, inject } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { CookingSessionService } from '@/app/core/native/cooking-session.service'
import { splitInstructionsIntoSteps } from '@/app/core/native/recipe-steps'

@Component({
  selector: 'app-cooking-session-dialog',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent],
  template: `
    <!-- Confirmation before replacing active session -->
    <app-lifeos-dialog
      [open]="showReplaceConfirm()"
      heading="Session de cuisine en cours"
      description="Une session est déjà active pour « {{ cooking.currentSession()?.recipeTitle }} ». Voulez-vous la remplacer ?"
      formId="replace-session-form"
      submitLabel="Remplacer"
      [busy]="busy()"
      (dismissed)="cancelReplace()"
    >
      <form id="replace-session-form" (ngSubmit)="confirmReplace()">
        <p>La session active sera terminée et remplacée par cette nouvelle recette.</p>
      </form>
    </app-lifeos-dialog>

    <!-- Checklist preview & edit dialog -->
    <app-lifeos-dialog
      [open]="open"
      heading="Lancer la session cuisine"
      [description]="recipeTitle"
      formId="start-cooking-form"
      submitLabel="Démarrer la session"
      [busy]="busy()"
      [error]="error()"
      (dismissed)="close()"
    >
      <form id="start-cooking-form" class="cooking-dialog-form" (ngSubmit)="start()">
        <p class="cooking-intro">
          Vérifiez ou complétez la checklist des étapes avant de lancer la session sur cet iPhone et sa Live Activity.
        </p>

        @if (steps().length === 0) {
          <div class="cooking-empty-steps">
            <p>Cette recette ne comporte pas d’instructions rédigées.</p>
            <p>Vous pouvez saisir manuellement les étapes de préparation ci-dessous ou annuler le lancement.</p>
            <button type="button" class="lifeos-button lifeos-button-secondary" (click)="addStep()">
              + Ajouter une étape
            </button>
          </div>
        } @else {
          <div class="cooking-steps-list">
            @for (step of steps(); track $index) {
              <div class="cooking-step-row">
                <span class="step-num">{{ $index + 1 }}.</span>
                <input
                  class="text-input"
                  type="text"
                  [ngModel]="steps()[$index]"
                  (ngModelChange)="updateStep($index, $event)"
                  [name]="'step-' + $index"
                  required
                />
                <button
                  type="button"
                  class="lifeos-button lifeos-button-secondary lifeos-button-sm"
                  (click)="removeStep($index)"
                  aria-label="Supprimer l’étape"
                >
                  ✕
                </button>
              </div>
            }
            <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" (click)="addStep()">
              + Ajouter une étape
            </button>
          </div>
        }
      </form>
    </app-lifeos-dialog>
  `,
  styles: [`
    .cooking-dialog-form { display: grid; gap: 14px; }
    .cooking-intro { margin: 0; color: var(--lifeos-text-soft); font-size: .9rem; line-height: 1.5; }
    .cooking-empty-steps { padding: 14px; border-radius: 12px; background: var(--lifeos-surface-soft); display: grid; gap: 8px; }
    .cooking-steps-list { display: grid; gap: 8px; max-height: 50vh; overflow-y: auto; padding-right: 4px; }
    .cooking-step-row { display: flex; align-items: center; gap: 8px; }
    .step-num { font-weight: 800; min-width: 24px; color: var(--lifeos-text-soft); }
    .cooking-step-row input { flex: 1; min-height: 2.4rem; }
  `],
})
export class CookingSessionDialogComponent {
  readonly cooking = inject(CookingSessionService)

  @Input() open = false
  @Input() recipeTitle = ''
  @Input() recipeId: string | null = null
  @Input() set instructions(val: string | null) {
    this.rawInstructions = val
    this.steps.set(splitInstructionsIntoSteps(val))
  }

  @Output() dismissed = new EventEmitter<void>()
  @Output() sessionStarted = new EventEmitter<void>()

  readonly steps = signal<string[]>([])
  readonly busy = signal(false)
  readonly error = signal('')
  readonly showReplaceConfirm = signal(false)

  private rawInstructions: string | null = null

  updateStep(index: number, value: string): void {
    this.steps.update(list => {
      const copy = [...list]
      copy[index] = value
      return copy
    })
  }

  addStep(): void {
    this.steps.update(list => [...list, ''])
  }

  removeStep(index: number): void {
    this.steps.update(list => list.filter((_, i) => i !== index))
  }

  close(): void {
    this.error.set('')
    this.dismissed.emit()
  }

  cancelReplace(): void {
    this.showReplaceConfirm.set(false)
  }

  async confirmReplace(): Promise<void> {
    this.showReplaceConfirm.set(false)
    try {
      await this.cooking.cancelSession()
      await this.doStart()
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Remplacement cuisine impossible')
    }
  }

  async start(): Promise<void> {
    const validSteps = this.steps().map(s => s.trim()).filter(Boolean)
    if (validSteps.length === 0) {
      this.error.set('Ajoutez au moins une étape pour démarrer la session de cuisine.')
      return
    }

    if (this.cooking.hasActiveSession()) {
      this.showReplaceConfirm.set(true)
      return
    }

    await this.doStart()
  }

  private async doStart(): Promise<void> {
    this.busy.set(true)
    this.error.set('')
    try {
      const validSteps = this.steps().map(s => s.trim()).filter(Boolean)
      const reconstructed = validSteps.map((s, i) => `${i + 1}. ${s}`).join('\n')
      await this.cooking.startSession(this.recipeTitle, this.recipeId, reconstructed)
      this.busy.set(false)
      this.sessionStarted.emit()
      this.close()
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Impossible de démarrer la session de cuisine.')
      this.busy.set(false)
    }
  }
}
