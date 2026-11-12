import { Component, inject, input, OnInit, output, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { buildRecipeRequest, type RecipeFormValues } from './recipe-form'
import type { RecipeDto } from './recipe.models'
import { RecipesApiService } from './recipes-api.service'

let nextEditorId = 0

/** Creates a recipe (`recipe` null) or edits its name, yield, duration, categories and preparation. */
@Component({
  selector: 'app-recipe-editor',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent],
  template: `
    <app-lifeos-dialog [open]="true" [heading]="recipe() ? 'Modifier la recette' : 'Nouvelle recette'" [wide]="true"
      [formId]="formId" [submitLabel]="recipe() ? 'Enregistrer' : 'Créer la recette'" [busy]="busy()" [dirty]="dirty()"
      [error]="error()" (dismissed)="closed.emit()">
      <form [id]="formId" class="editor-form" (ngSubmit)="save()">
        <label class="field">
          <span>Nom</span>
          <input class="lifeos-input" name="name" maxlength="200" required [(ngModel)]="values.name" />
        </label>
        <div class="field-row">
          <label class="field">
            <span>Portions (rendement)</span>
            <input class="lifeos-input" name="servings" type="number" min="1" step="1" required [(ngModel)]="values.servings" />
          </label>
          <label class="field">
            <span>Durée (minutes)</span>
            <input class="lifeos-input" name="duration" type="number" min="0" step="1" required [(ngModel)]="values.durationMinutes" />
          </label>
        </div>
        <label class="field">
          <span>Catégories, séparées par des virgules</span>
          <input class="lifeos-input" name="tags" placeholder="plat, végétarien, batch" [(ngModel)]="values.tagsText" />
        </label>
        <label class="field">
          <span>Préparation : une étape par ligne</span>
          <textarea class="lifeos-input" name="instructions" rows="8" maxlength="10000" [(ngModel)]="values.instructions"></textarea>
        </label>
      </form>
    </app-lifeos-dialog>
  `,
  styles: [`.editor-form { display: flex; flex-direction: column; gap: 16px; }`],
})
export class RecipeEditorComponent implements OnInit {
  private readonly api = inject(RecipesApiService)
  readonly recipe = input.required<RecipeDto | null>()
  readonly saved = output<RecipeDto>()
  readonly closed = output<void>()
  readonly busy = signal(false)
  readonly error = signal('')
  readonly formId = `recipe-editor-${++nextEditorId}`
  values: RecipeFormValues = { name: '', servings: 4, durationMinutes: 30, tagsText: '', instructions: '' }
  private initial = formSnapshot(this.values)

  ngOnInit(): void {
    const recipe = this.recipe()
    if (recipe) {
      this.values = {
        name: recipe.name, servings: recipe.servings, durationMinutes: recipe.durationMinutes,
        tagsText: recipe.tags.join(', '), instructions: recipe.instructions ?? '',
      }
    }
    this.initial = formSnapshot(this.values)
  }

  dirty(): boolean { return hasChanges(this.initial, this.values) }

  async save(): Promise<void> {
    const request = buildRecipeRequest(this.values)
    if (!request.ok) { this.error.set(request.error); return }
    const recipe = this.recipe()
    this.busy.set(true)
    this.error.set('')
    try {
      const result = recipe
        ? await firstValueFrom(this.api.update(recipe.id, request.value))
        : await firstValueFrom(this.api.create(request.value))
      this.initial = formSnapshot(this.values)
      this.saved.emit(result)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }
}
