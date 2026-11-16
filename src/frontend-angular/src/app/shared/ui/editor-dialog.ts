import type { ComponentType } from '@angular/cdk/portal'
import type { MatDialog, MatDialogConfig, MatDialogRef } from '@angular/material/dialog'
import { isHandset } from './breakpoints'

/**
 * Opens a creation or edit window (ADR 0016): a dialog on the computer, full screen on the phone.
 * Closing goes through `app-editor-frame`, which asks before dropping changes.
 */
export function openEditor<C, D, R = unknown>(dialog: MatDialog, component: ComponentType<C>, data: D): MatDialogRef<C, R> {
  const size: MatDialogConfig = isHandset()
    ? { width: '100vw', maxWidth: '100vw', height: '100dvh', maxHeight: '100dvh', panelClass: 'lo-fullscreen-dialog' }
    : { width: '640px', maxWidth: 'calc(100vw - 32px)', maxHeight: 'calc(100dvh - 64px)' }
  return dialog.open<C, D, R>(component, { data, disableClose: true, autoFocus: 'first-tabbable', restoreFocus: true, ...size })
}
