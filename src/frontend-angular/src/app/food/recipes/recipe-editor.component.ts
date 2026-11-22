import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { buildRecipeRequest, type RecipeFormValues } from './recipe-form'
import type { RecipeDto } from './recipe.models'
import { RecipesApiService } from './recipes-api.service'

/** Creates a recipe (data null) or edits its name, yield, duration, categories and preparation; returns the saved recipe. */
@Component({
  selector: 'app-recipe-editor',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatFormFieldModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="recipe ? 'Modifier la recette' : 'Nouvelle recette'" formId="recipe-form"
      [saveLabel]="recipe ? 'Enregistrer' : 'Créer'" [busy]="busy()" [dirty]="dirty()" [error]="error()">
      <form id="recipe-form" class="lo-form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" maxlength="200" required [(ngModel)]="values.name" />
        </mat-form-field>
        <div class="lo-form-row">
          <mat-form-field appearance="outline">
            <mat-label>Portions (rendement)</mat-label>
            <input matInput name="servings" type="number" inputmode="numeric" min="1" step="1" required [(ngModel)]="values.servings" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Durée (minutes)</mat-label>
            <input matInput name="duration" type="number" inputmode="numeric" min="0" step="1" required [(ngModel)]="values.durationMinutes" />
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Catégories</mat-label>
          <input matInput name="tags" placeholder="plat, végétarien, batch" [(ngModel)]="values.tagsText" />
          <mat-hint>Séparées par des virgules.</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Préparation</mat-label>
          <textarea matInput name="instructions" rows="8" maxlength="10000" [(ngModel)]="values.instructions"></textarea>
          <mat-hint>Une étape par ligne.</mat-hint>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
})
export class RecipeEditorComponent {
  private readonly api = inject(RecipesApiService)
  private readonly ref = inject<MatDialogRef<RecipeEditorComponent, RecipeDto>>(MatDialogRef)
  readonly recipe = inject<RecipeDto | null>(MAT_DIALOG_DATA)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly values: RecipeFormValues = this.recipe
    ? {
        name: this.recipe.name, servings: this.recipe.servings, durationMinutes: this.recipe.durationMinutes,
        tagsText: this.recipe.tags.join(', '), instructions: this.recipe.instructions ?? '',
      }
    : { name: '', servings: 4, durationMinutes: 30, tagsText: '', instructions: '' }
  private readonly initial = formSnapshot(this.values)

  dirty(): boolean { return hasChanges(this.initial, this.values) }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = buildRecipeRequest(this.values)
    if (!request.ok) { this.error.set(request.error); return }
    this.busy.set(true)
    this.error.set('')
    try {
      this.ref.close(this.recipe
        ? await firstValueFrom(this.api.update(this.recipe.id, request.value))
        : await firstValueFrom(this.api.create(request.value)))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }
}
