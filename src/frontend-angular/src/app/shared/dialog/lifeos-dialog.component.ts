import { afterRenderEffect, Component, ElementRef, input, output, viewChild } from '@angular/core'
import { dismissDecision, focusableFieldSelector } from './dialog-guard'

let nextDialogId = 0

/**
 * Shared creation/edition dialog built on the native modal <dialog>:
 * the page behind becomes inert, focus stays inside and returns to the trigger on close.
 * The page owns the projected <form id="..."> and closes the dialog only after a successful save.
 */
@Component({
  selector: 'app-lifeos-dialog',
  standalone: true,
  template: `
    <dialog #dialog class="lifeos-dialog" [class.lifeos-dialog-wide]="wide()"
      [attr.aria-labelledby]="headingId" [attr.aria-describedby]="description() ? descriptionId : null"
      [attr.aria-busy]="busy() || null" (cancel)="onCancel($event)" (close)="onNativeClose()">
      <header class="lifeos-dialog-header">
        <div>
          <h2 [id]="headingId">{{ heading() }}</h2>
          @if (description()) { <p [id]="descriptionId">{{ description() }}</p> }
        </div>
        <button type="button" class="lifeos-dialog-close" aria-label="Fermer" [disabled]="busy()" (click)="requestDismiss()">
          <i class="pi pi-times" aria-hidden="true"></i>
        </button>
      </header>
      <div class="lifeos-dialog-body">
        @if (error()) { <p class="lifeos-dialog-error" role="alert">{{ error() }}</p> }
        <ng-content />
      </div>
      <footer class="lifeos-dialog-footer">
        <ng-content select="[dialogExtraActions]" />
        <span class="lifeos-dialog-spacer"></span>
        <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="requestDismiss()">{{ cancelLabel() }}</button>
        @if (formId()) {
          <button type="submit" class="lifeos-button" [attr.form]="formId()" [disabled]="busy() || submitDisabled()">
            {{ busy() ? pendingLabel() : submitLabel() }}
          </button>
        }
      </footer>
    </dialog>
  `,
})
export class LifeosDialogComponent {
  readonly open = input(false)
  readonly heading = input.required<string>()
  readonly description = input('')
  readonly formId = input('')
  readonly submitLabel = input('Créer')
  readonly pendingLabel = input('Enregistrement…')
  readonly cancelLabel = input('Annuler')
  readonly busy = input(false)
  readonly dirty = input(false)
  readonly submitDisabled = input(false)
  readonly error = input('')
  readonly wide = input(false)
  readonly dismissed = output<void>()

  readonly headingId = `lifeos-dialog-title-${++nextDialogId}`
  readonly descriptionId = `${this.headingId}-description`
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog')
  private returnFocus: HTMLElement | null = null

  constructor() {
    afterRenderEffect(() => {
      const element = this.dialog().nativeElement
      const shouldOpen = this.open()
      if (shouldOpen && !element.open) {
        const active = document.activeElement
        this.returnFocus = active instanceof HTMLElement ? active : null
        element.showModal()
        this.focusFirstField(element)
      } else if (!shouldOpen && element.open) {
        element.close()
      }
    })
  }

  requestDismiss(): void {
    const decision = dismissDecision({ busy: this.busy(), dirty: this.dirty() })
    if (decision === 'ignore') return
    if (decision === 'confirm' && !window.confirm('Abandonner la saisie en cours ? Les informations non enregistrées seront perdues.')) return
    this.dismissed.emit()
  }

  onCancel(event: Event): void {
    event.preventDefault()
    this.requestDismiss()
  }

  onNativeClose(): void {
    const element = this.dialog().nativeElement
    // The browser may force-close on repeated Escape; keep the dialog consistent with the page state.
    if (this.open()) {
      element.showModal()
      return
    }
    const target = this.returnFocus
    this.returnFocus = null
    if (target?.isConnected) target.focus()
  }

  private focusFirstField(element: HTMLDialogElement): void {
    const body = element.querySelector('.lifeos-dialog-body')
    const field = body?.querySelector<HTMLElement>(focusableFieldSelector)
    ;(field ?? element.querySelector<HTMLElement>('.lifeos-dialog-close'))?.focus()
  }
}
