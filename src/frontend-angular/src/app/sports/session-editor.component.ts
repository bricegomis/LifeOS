import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { applyTemplate, draftFromSession, duration, emptySessionDraft, FEELS, pace, type SessionDraft, sessionRequest, spanMinutes } from './sport-format'
import { SportsApiService } from './sports-api.service'
import type { SportOption, SportSessionDto, SportTemplateDto } from './sports.models'

export interface SessionEditorData {
  /** The session to edit; null for a new one. */
  session: SportSessionDto | null
  templates: SportTemplateDto[]
  sports: SportOption[]
  today: string
}

/** Adds or edits a logged session; values of a template are copied and stay editable (ADR 0013). Returns a message. */
@Component({
  selector: 'app-session-editor',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame [heading]="session ? 'Modifier la séance' : 'Nouvelle séance'" formId="sport-session-form"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="session && !garmin ? 'Supprimer' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="sport-session-form" class="lo-form" (ngSubmit)="save()">
        @if (garmin) {
          <p class="lo-notice">Séance de la montre : ses valeurs viennent de Garmin et suivent ses modifications. Tu peux changer ici le sport,
            la séance type et les notes ; ils ne sont jamais écrasés.</p>
        }
        @if (activeTemplates.length) {
          <div class="lo-field-group">
            <span class="lo-field-label">Partir d’une séance type</span>
            <div class="lo-chips">
              <button type="button" class="lo-chip" [class.is-on]="draft.templateId === null" [attr.aria-pressed]="draft.templateId === null"
                (click)="draft.templateId = null">@if (draft.templateId === null) { <mat-icon svgIcon="check" /> }Sans séance type</button>
              @for (template of activeTemplates; track template.id) {
                <button type="button" class="lo-chip" [class.is-on]="draft.templateId === template.id" [attr.aria-pressed]="draft.templateId === template.id"
                  (click)="pick(template)">@if (draft.templateId === template.id) { <mat-icon svgIcon="check" /> }{{ template.name }}</button>
              }
            </div>
            @if (!garmin) { <p class="lo-help">Les valeurs de la séance type sont recopiées ; tu peux les modifier.</p> }
          </div>
        }
        <div class="lo-form-row">
          <mat-form-field appearance="outline">
            <mat-label>Sport</mat-label>
            <mat-select name="sport" [(ngModel)]="draft.sport">
              @for (sport of data.sports; track sport.code) { <mat-option [value]="sport.code">{{ sport.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          @if (!garmin) {
            <mat-form-field appearance="outline">
              <mat-label>Date</mat-label>
              <input matInput type="date" name="date" required [max]="data.today" [(ngModel)]="draft.date" />
            </mat-form-field>
          }
        </div>
        @if (!garmin) {
          <div class="lo-form-row">
            <mat-form-field appearance="outline">
              <mat-label>Début</mat-label>
              <input matInput type="time" name="start" required [(ngModel)]="draft.startTime" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Fin</mat-label>
              <input matInput type="time" name="end" required [(ngModel)]="draft.endTime" />
              @if (spanText()) { <mat-hint>{{ spanText() }}</mat-hint> }
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Durée active (min)</mat-label>
              <input matInput type="number" inputmode="numeric" name="duration" min="1" max="1440" step="1" [placeholder]="spanPlaceholder()" [(ngModel)]="draft.durationMinutes" />
              <mat-hint>Vide : fin moins début</mat-hint>
            </mat-form-field>
          </div>
          <div class="lo-form-row">
            <mat-form-field appearance="outline">
              <mat-label>Calories (kcal)</mat-label>
              <input matInput type="number" inputmode="numeric" name="calories" min="0" max="20000" step="1" placeholder="Inconnues" [(ngModel)]="draft.calories" />
            </mat-form-field>
            @if (hasDistance()) {
              <mat-form-field appearance="outline">
                <mat-label>Distance (km)</mat-label>
                <input matInput type="number" inputmode="decimal" name="distance" min="0" max="1000" step="0.01" [(ngModel)]="draft.distanceKm" />
                @if (paceText()) { <mat-hint>{{ paceText() }}</mat-hint> }
              </mat-form-field>
            }
          </div>
          @if (draft.calories === null) {
            <p class="lo-notice">Sans calories, le total de la semaine sera marqué partiel. Rien n’est calculé à ta place.</p>
          }
          <div class="lo-field-group">
            <span class="lo-field-label">Effort perçu (1 = très facile, 10 = maximal)</span>
            <div class="feel-grid" role="group" aria-label="Effort perçu">
              @for (value of efforts; track value) {
                <button type="button" class="lo-chip" [class.is-on]="draft.effort === value" [attr.aria-pressed]="draft.effort === value"
                  (click)="draft.effort = draft.effort === value ? null : value">{{ value }}</button>
              }
            </div>
          </div>
          <div class="lo-field-group">
            <span class="lo-field-label">Sensation</span>
            <div class="lo-chips" role="group" aria-label="Sensation">
              @for (feel of feels; track feel.code) {
                <button type="button" class="lo-chip" [class.is-on]="draft.feel === feel.code" [attr.aria-pressed]="draft.feel === feel.code"
                  (click)="draft.feel = draft.feel === feel.code ? null : feel.code">@if (draft.feel === feel.code) { <mat-icon svgIcon="check" /> }{{ feel.label }}</button>
              }
            </div>
          </div>
          <div class="lo-form-row">
            <mat-form-field appearance="outline">
              <mat-label>FC moyenne (bpm)</mat-label>
              <input matInput type="number" inputmode="numeric" name="hrAvg" min="30" max="250" [(ngModel)]="draft.averageHeartRate" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>FC max (bpm)</mat-label>
              <input matInput type="number" inputmode="numeric" name="hrMax" min="30" max="250" [(ngModel)]="draft.maxHeartRate" />
            </mat-form-field>
          </div>
        }
        <mat-form-field appearance="outline">
          <mat-label>Notes (exercices, séries, WOD, score…)</mat-label>
          <textarea matInput name="notes" rows="4" maxlength="4000" [(ngModel)]="draft.notes"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .feel-grid { display: grid; grid-template-columns: repeat(10, minmax(0, 1fr)); gap: 6px; }
    .feel-grid .lo-chip { justify-content: center; min-height: 40px; padding: 0; }
    @media (max-width: 760px) { .feel-grid { grid-template-columns: repeat(5, minmax(0, 1fr)); } }
  `],
})
export class SessionEditorComponent {
  private readonly api = inject(SportsApiService)
  private readonly ref = inject<MatDialogRef<SessionEditorComponent, string>>(MatDialogRef)
  readonly data = inject<SessionEditorData>(MAT_DIALOG_DATA)
  readonly session = this.data.session
  readonly activeTemplates = this.data.templates.filter(template => !template.isArchived)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly efforts = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
  readonly feels = FEELS
  readonly garmin = this.session?.source === 'garmin'
  draft: SessionDraft = this.session ? draftFromSession(this.session) : emptySessionDraft(this.data.today)
  private readonly initial = formSnapshot(this.draft)

  hasDistance(): boolean { return this.data.sports.find(sport => sport.code === this.draft.sport)?.hasDistance ?? false }
  paceText(): string {
    return this.hasDistance() ? pace(this.draft.sport, this.draft.durationMinutes ?? spanMinutes(this.draft.startTime, this.draft.endTime), this.draft.distanceKm) : ''
  }
  spanText(): string {
    const span = spanMinutes(this.draft.startTime, this.draft.endTime)
    if (span === null) return ''
    return `${duration(span)}${this.draft.endTime < this.draft.startTime ? ', fin le lendemain' : ''}`
  }
  spanPlaceholder(): string { return String(spanMinutes(this.draft.startTime, this.draft.endTime) ?? '') }
  dirty(): boolean { return hasChanges(this.initial, this.draft) }
  pick(template: SportTemplateDto): void { this.draft = applyTemplate(this.draft, template) }

  save(): Promise<void> {
    const request = sessionRequest(this.draft, this.data.sports)
    if (typeof request === 'string') { this.error.set(request); return Promise.resolve() }
    const session = this.session
    return this.run(() => session ? this.api.updateSession(session.id, request) : this.api.createSession(request),
      session ? 'Séance modifiée' : 'Séance enregistrée')
  }

  remove(): Promise<void> {
    const session = this.session
    return session ? this.run(() => this.api.deleteSession(session.id), 'Séance supprimée') : Promise.resolve()
  }

  private async run(call: () => Observable<unknown>, message: string): Promise<void> {
    if (this.busy()) return
    this.busy.set(true)
    this.error.set('')
    try {
      await firstValueFrom(call())
      this.ref.close(message)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
