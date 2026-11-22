import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatCheckboxModule } from '@angular/material/checkbox'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { FoodApiService } from '../food-api.service'
import type { StoreDto, StoreRequest } from '../food.models'

/** Creates or edits a store; returns a message for the snack bar. Removal is refused while prices or tickets use it. */
@Component({
  selector: 'app-store-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatCheckboxModule, MatFormFieldModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="store ? 'Modifier le magasin' : 'Nouveau magasin'" formId="store-form" [saveLabel]="store ? 'Enregistrer' : 'Ajouter'"
      [busy]="busy()" [dirty]="dirty()" [error]="error()" [dangerLabel]="store ? 'Supprimer' : ''" dangerIcon="delete" (danger)="remove()">
      <form id="store-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" maxlength="200" required [(ngModel)]="values.name" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Adresse (facultatif)</mat-label>
          <input matInput name="address" maxlength="400" [(ngModel)]="values.address" />
        </mat-form-field>
        <mat-checkbox name="organic" [(ngModel)]="values.isOrganic">Magasin bio</mat-checkbox>
        <mat-checkbox name="local" [(ngModel)]="values.isLocal">Commerce local</mat-checkbox>
        @if (store) { <p class="lo-help">Un magasin ne peut pas être supprimé tant que des prix ou des tickets y sont rattachés.</p> }
      </form>
    </app-editor-frame>
  `,
})
export class StoreDialogComponent {
  private readonly api = inject(FoodApiService)
  private readonly ref = inject<MatDialogRef<StoreDialogComponent, string>>(MatDialogRef)
  readonly store = inject<StoreDto | null>(MAT_DIALOG_DATA)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly values: StoreRequest = this.store
    ? { name: this.store.name, address: this.store.address ?? '', isOrganic: this.store.isOrganic, isLocal: this.store.isLocal }
    : { name: '', address: '', isOrganic: false, isLocal: false }
  private readonly initial = formSnapshot(this.values)

  dirty(): boolean { return hasChanges(this.initial, this.values) }

  save(): Promise<void> {
    const name = this.values.name.trim()
    if (!name) { this.error.set('Indiquez le nom du magasin.'); return Promise.resolve() }
    const request = { ...this.values, name, address: this.values.address.trim() }
    return this.run(() => this.store ? this.api.updateStore(this.store.id, request) : this.api.createStore(request),
      this.store ? 'Magasin enregistré' : 'Magasin ajouté')
  }

  remove(): Promise<void> {
    if (!this.store) return Promise.resolve()
    return this.run(() => this.api.deleteStore(this.store!.id), `« ${this.store.name} » supprimé`)
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
