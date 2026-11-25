import { Component, Injectable, inject } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog'
import { firstValueFrom } from 'rxjs'

export interface ConfirmOptions {
  /** The question, e.g. « Supprimer ce prix relevé ? ». */
  message: string
  /** Label of the button that goes ahead (default « Confirmer »). */
  confirmLabel?: string
  /** Label of the button that keeps things as they are (default « Annuler »). */
  cancelLabel?: string
  /** Shows the confirm button in the error colour (deleting, archiving, dropping changes). */
  danger?: boolean
}

/** A short Material confirmation window (ADR 0016), themed like the rest of LifeOS. */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <mat-dialog-content><p class="message">{{ data.message }}</p></mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="false" cdkFocusInitial>{{ data.cancelLabel ?? 'Annuler' }}</button>
      <button mat-flat-button type="button" [class.danger]="data.danger" [mat-dialog-close]="true">
        {{ data.confirmLabel ?? 'Confirmer' }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .message { margin: 0; color: var(--mat-sys-on-surface); }
    .danger { --mat-button-filled-container-color: var(--mat-sys-error); --mat-button-filled-label-text-color: var(--mat-sys-on-error); }
  `],
})
export class ConfirmDialogComponent {
  readonly data = inject<ConfirmOptions>(MAT_DIALOG_DATA)
}

/** Asks before an action; resolves to true only when the user confirms. */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog)

  async ask(options: ConfirmOptions): Promise<boolean> {
    const ref = this.dialog.open<ConfirmDialogComponent, ConfirmOptions, boolean>(ConfirmDialogComponent, {
      data: options, maxWidth: 'min(420px, calc(100vw - 32px))', role: 'alertdialog', ariaLabel: options.message,
    })
    return (await firstValueFrom(ref.afterClosed())) === true
  }
}
