import { Component, effect, inject, input, output, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { applyTemplate, draftFromSession, emptySessionDraft, pace, type SessionDraft, sessionRequest } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { SportOption, SportSessionDto, SportTemplateDto } from './sports.models'

/** Adds or edits a logged session; values of a template are copied and stay editable (ADR 0013). */
@Component({
  selector: 'app-session-editor',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent],
  template: `
    <app-lifeos-dialog [open]="open()" [heading]="session() ? 'Modifier la séance' : 'Nouvelle séance'" [wide]="true"
      formId="sport-session-form" [submitLabel]="session() ? 'Enregistrer' : 'Enregistrer la séance'" [busy]="busy()"
      [dirty]="dirty()" [error]="error()" (dismissed)="dismissed.emit()">
      <form id="sport-session-form" class="dialog-form" (ngSubmit)="save()">
        @if (activeTemplates().length) {
          <fieldset class="session-fieldset">
            <legend>Partir d'une séance type</legend>
            <div class="choice-grid">
              <button type="button" class="pill" [attr.aria-pressed]="draft.templateId === null" (click)="draft.templateId = null">Sans séance type</button>
              @for (template of activeTemplates(); track template.id) {
                <button type="button" class="pill" [attr.aria-pressed]="draft.templateId === template.id" (click)="pick(template)">{{ template.name }}</button>
              }
            </div>
            <p class="dialog-hint">Les valeurs de la séance type sont recopiées ; tu peux les modifier.</p>
          </fieldset>
        }
        <div class="dialog-row">
          <label class="dialog-field"><span>Sport</span>
            <select class="text-input" name="sport" [(ngModel)]="draft.sport">
              @for (sport of sports(); track sport.code) { <option [value]="sport.code">{{ sport.label }}</option> }
            </select></label>
          <label class="dialog-field"><span>Date</span>
            <input class="text-input" type="date" name="date" required [max]="today()" [(ngModel)]="draft.date" /></label>
        </div>
        <div class="dialog-row">
          <label class="dialog-field"><span>Durée (min)</span>
            <input class="text-input" type="number" inputmode="numeric" name="duration" min="1" max="1440" step="1" required [(ngModel)]="draft.durationMinutes" /></label>
          <label class="dialog-field"><span>Calories (kcal)</span>
            <input class="text-input" type="number" inputmode="numeric" name="calories" min="0" max="20000" step="1" placeholder="Inconnues" [(ngModel)]="draft.calories" /></label>
          @if (hasDistance()) {
            <label class="dialog-field"><span>Distance (km)</span>
              <input class="text-input" type="number" inputmode="decimal" name="distance" min="0" max="1000" step="0.01" [(ngModel)]="draft.distanceKm" /></label>
          }
        </div>
        @if (paceText()) { <p class="dialog-hint">{{ paceText() }}</p> }
        @if (draft.calories === null) {
          <p class="notice-warning">Sans calories, le total de la semaine sera marqué partiel. Rien n'est calculé à ta place.</p>
        }
        <fieldset class="session-fieldset">
          <legend>Ressenti <span class="muted">(1 = très dur, 10 = excellent)</span></legend>
          <div class="feel-grid">
            @for (value of feelings; track value) {
              <button type="button" class="pill" [attr.aria-pressed]="draft.feeling === value" (click)="draft.feeling = draft.feeling === value ? null : value">{{ value }}</button>
            }
          </div>
        </fieldset>
        <div class="dialog-row">
          <label class="dialog-field"><span>FC moyenne (bpm)</span>
            <input class="text-input" type="number" inputmode="numeric" name="hrAvg" min="30" max="250" [(ngModel)]="draft.averageHeartRate" /></label>
          <label class="dialog-field"><span>FC max (bpm)</span>
            <input class="text-input" type="number" inputmode="numeric" name="hrMax" min="30" max="250" [(ngModel)]="draft.maxHeartRate" /></label>
        </div>
        <label class="dialog-field"><span>Notes (exercices, séries, WOD, score…)</span>
          <textarea class="text-input" name="notes" rows="4" maxlength="4000" [(ngModel)]="draft.notes"></textarea></label>
      </form>
      @if (session()) {
        <button dialogExtraActions type="button" class="lifeos-button lifeos-button-secondary danger-text" [disabled]="busy()" (click)="remove()">Supprimer</button>
      }
    </app-lifeos-dialog>
  `,
  styleUrl: './sports.css',
  styles: [`
    .session-fieldset { display: flex; flex-direction: column; gap: 8px; margin: 0; padding: 0; border: 0; }
    .session-fieldset legend { margin-bottom: 8px; padding: 0; font-size: 14px; font-weight: 600; }
    .notice-warning { padding: 10px 12px; border-radius: var(--lifeos-radius-sm); background: var(--lifeos-warning-soft); color: var(--lifeos-warning); font-size: 14px; font-weight: 500; }
  `],
})
export class SessionEditorComponent {
  private readonly api = inject(SportsApiService)
  readonly open = input(false)
  /** The session to edit; null for a new one. */
  readonly session = input<SportSessionDto | null>(null)
  readonly templates = input<SportTemplateDto[]>([])
  readonly sports = input<SportOption[]>([])
  readonly today = input.required<string>()
  readonly saved = output<string>()
  readonly dismissed = output<void>()

  readonly busy = signal(false)
  readonly error = signal('')
  readonly feelings = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
  draft: SessionDraft = emptySessionDraft('')
  private initial = formSnapshot(this.draft)

  constructor() {
    effect(() => {
      if (!this.open()) return
      const session = this.session()
      this.draft = session ? draftFromSession(session) : emptySessionDraft(this.today())
      this.initial = formSnapshot(this.draft)
      this.error.set('')
    })
  }

  activeTemplates(): SportTemplateDto[] { return this.templates().filter(template => !template.isArchived) }
  hasDistance(): boolean { return this.sports().find(sport => sport.code === this.draft.sport)?.hasDistance ?? false }
  paceText(): string { return this.hasDistance() ? pace(this.draft.sport, this.draft.durationMinutes, this.draft.distanceKm) : '' }
  dirty(): boolean { return hasChanges(this.initial, this.draft) }
  pick(template: SportTemplateDto): void { this.draft = applyTemplate(this.draft, template) }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = sessionRequest(this.draft, this.sports())
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      const session = this.session()
      await firstValueFrom(session ? this.api.updateSession(session.id, request) : this.api.createSession(request))
      this.busy.set(false)
      this.saved.emit(session ? 'Séance modifiée.' : 'Séance enregistrée.')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  async remove(): Promise<void> {
    const session = this.session()
    if (!session || this.busy() || !window.confirm('Supprimer cette séance ?')) return
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.deleteSession(session.id))
      this.busy.set(false)
      this.saved.emit('Séance supprimée.')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
