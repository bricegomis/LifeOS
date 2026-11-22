import { BreakpointObserver } from '@angular/cdk/layout'
import { Component, DestroyRef, inject, input, output, signal } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { MatButtonModule } from '@angular/material/button'
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { filter, map } from 'rxjs'
import { dismissDecision } from '@/app/shared/dialog/dialog-guard'
import { HANDSET_QUERY } from './breakpoints'

/**
 * Frame of every creation and edit window (ADR 0016). On the computer: title, content, then the
 * dangerous action bottom left and Annuler / Enregistrer bottom right. On the phone: full screen with
 * close top left and Enregistrer top right. The save button submits the form named `formId`; without
 * `formId` the window only has its close button (labelled `cancelLabel`, e.g. "Fermer"), for windows
 * whose rows save themselves.
 */
@Component({
  selector: 'app-editor-frame',
  standalone: true,
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
  template: `
    @if (handset()) {
      <div class="frame-bar">
        <button mat-icon-button type="button" (click)="requestClose()" aria-label="Fermer la fenêtre"><mat-icon svgIcon="close" /></button>
        <h2 mat-dialog-title class="frame-bar-title">{{ heading() }}</h2>
        @if (formId()) { <button mat-button type="submit" [attr.form]="formId()" [disabled]="busy()">{{ saveLabel() }}</button> }
      </div>
    } @else {
      <h2 mat-dialog-title>{{ heading() }}</h2>
    }
    <mat-dialog-content class="frame-content">
      <ng-content />
      @if (error()) { <p class="frame-error" role="alert"><mat-icon svgIcon="error" /><span>{{ error() }}</span></p> }
      @if (handset() && dangerLabel()) { <div class="frame-danger-row"><button mat-button type="button" class="frame-danger" [class.is-confirming]="confirming()" [disabled]="busy()"
          (click)="dangerClick()"><mat-icon [svgIcon]="dangerIcon()" />{{ confirming() ? 'Confirmer : ' + dangerLabel().toLowerCase() : dangerLabel() }}</button></div> }
    </mat-dialog-content>
    @if (!handset()) {
      <mat-dialog-actions class="frame-actions">
        @if (dangerLabel()) { <button mat-button type="button" class="frame-danger" [class.is-confirming]="confirming()" [disabled]="busy()"
          (click)="dangerClick()"><mat-icon [svgIcon]="dangerIcon()" />{{ confirming() ? 'Confirmer : ' + dangerLabel().toLowerCase() : dangerLabel() }}</button> }
        <span class="frame-spacer"></span>
        <button mat-button type="button" (click)="requestClose()" [disabled]="busy()">{{ cancelLabel() }}</button>
        @if (formId()) { <button mat-flat-button type="submit" [attr.form]="formId()" [disabled]="busy()">{{ saveLabel() }}</button> }
      </mat-dialog-actions>
    }
  `,
  styles: [`
    .frame-bar { display: flex; align-items: center; gap: 4px; min-height: 64px; padding: 0 8px 0 4px; padding-top: env(safe-area-inset-top); }
    .frame-bar-title.mat-mdc-dialog-title { flex: 1 1 auto; min-width: 0; margin: 0; padding: 0 0 0 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .frame-bar-title.mat-mdc-dialog-title::before { display: none; }
    /* Doubled class: Material removes the top padding after a title, which clips the first field's label. */
    .frame-content.frame-content { display: flex; flex-direction: column; gap: 20px; padding-top: 12px; }
    .frame-error { display: flex; align-items: center; gap: 8px; margin: 0; padding: 12px 16px; border-radius: 12px;
      background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); font-weight: 600; }
    .frame-actions { flex-wrap: wrap; gap: 8px; }
    .frame-spacer { flex: 1 1 auto; }
    .frame-danger { color: var(--mat-sys-error); }
    .frame-danger.is-confirming { background: var(--mat-sys-error); color: var(--mat-sys-on-error); }
    .frame-danger-row { display: flex; padding-bottom: 16px; }
  `],
})
export class EditorFrameComponent {
  private readonly ref = inject(MatDialogRef)
  readonly heading = input.required<string>()
  readonly formId = input('')
  readonly saveLabel = input('Enregistrer')
  readonly cancelLabel = input('Annuler')
  readonly busy = input(false)
  readonly dirty = input(false)
  readonly error = input('')
  /** Archive or stop; asks a second click before acting. */
  readonly dangerLabel = input('')
  /** `archive` (default), `delete` for a removal. */
  readonly dangerIcon = input('archive')
  readonly danger = output<void>()
  readonly confirming = signal(false)
  readonly handset = toSignal(inject(BreakpointObserver).observe(HANDSET_QUERY).pipe(map(state => state.matches)),
    { initialValue: globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false })

  constructor() {
    const backdrop = this.ref.backdropClick().subscribe(() => this.requestClose())
    const escape = this.ref.keydownEvents().pipe(filter(event => event.key === 'Escape')).subscribe(event => {
      event.preventDefault()
      this.requestClose()
    })
    inject(DestroyRef).onDestroy(() => { backdrop.unsubscribe(); escape.unsubscribe() })
  }

  /** Closes, asking first when the draft was changed; ignored while saving. */
  requestClose(): void {
    const decision = dismissDecision({ busy: this.busy(), dirty: this.dirty() })
    if (decision === 'ignore') return
    if (decision === 'confirm' && !globalThis.confirm('Abandonner les modifications ?')) return
    this.ref.close()
  }

  dangerClick(): void {
    if (!this.confirming()) { this.confirming.set(true); return }
    this.confirming.set(false)
    this.danger.emit()
  }
}
