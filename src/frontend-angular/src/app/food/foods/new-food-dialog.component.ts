import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { MatSelectModule } from '@angular/material/select'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, FoodFamily } from '../food.models'
import { familyOptions } from '../shared/classification'

/** A food starts with its name and family (ADR 0012); the sheet then shows what is left to fill in. */
@Component({
  selector: 'app-new-food-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <app-editor-frame heading="Nouvel aliment" formId="new-food-form" saveLabel="Créer" [busy]="busy()" [dirty]="!!name.trim() || !!family" [error]="error()">
      <form id="new-food-form" class="lo-form" (ngSubmit)="create()">
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" maxlength="200" required [(ngModel)]="name" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Famille</mat-label>
          <mat-select name="family" required [(ngModel)]="family">
            @for (option of familyOptions; track option[0]) { <mat-option [value]="option[0]">{{ option[1] }}</mat-option> }
          </mat-select>
          <mat-hint>Le reste du classement, la nutrition, la digestion et la photo se complètent ensuite sur la fiche.</mat-hint>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class NewFoodDialogComponent {
  private readonly api = inject(FoodApiService)
  private readonly ref = inject<MatDialogRef<NewFoodDialogComponent, FoodDto>>(MatDialogRef)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly familyOptions = familyOptions
  name = ''
  family: FoodFamily | '' = ''

  async create(): Promise<void> {
    const name = this.name.trim()
    const family = this.family
    if (!name) { this.error.set('Indiquez le nom de l’aliment.'); return }
    if (!family) { this.error.set('Choisissez la famille de l’aliment.'); return }
    this.busy.set(true)
    this.error.set('')
    try {
      this.ref.close(await firstValueFrom(this.api.createFood({
        name, referenceUnit: '', nutrition: null, fodmap: null, nova: null, digestiveNotes: null,
        family, roles: [], conservation: null, seasonMonths: [], tags: [],
      })))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
