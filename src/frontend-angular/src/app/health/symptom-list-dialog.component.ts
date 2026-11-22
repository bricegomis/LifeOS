import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MAT_DIALOG_DATA } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { HealthApiService } from './health-api.service'
import type { SymptomKind, SymptomTypeDto } from './health.models'

/** The owner's list of symptoms: add one, archive or restore (ADR 0018). Each row saves itself. */
@Component({
  selector: 'app-symptom-list-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame heading="Ma liste de symptômes" cancelLabel="Fermer" [busy]="busy()" [error]="error()">
      <div class="lo-card list">
        @for (t of types(); track t.id) {
          <div class="row" [class.is-off]="t.isArchived">
            <span class="name">{{ t.name }} <span class="lo-muted">· {{ t.kind === 'mental' ? 'mental' : 'physique' }}</span></span>
            <button mat-button type="button" [disabled]="busy()" (click)="archive(t)">{{ t.isArchived ? 'Rétablir' : 'Archiver' }}</button>
          </div>
        }
      </div>
      <form class="add" (ngSubmit)="add()">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Nouveau symptôme</mat-label>
          <input matInput name="name" maxlength="100" placeholder="Ex. maux de tête" [(ngModel)]="name" />
        </mat-form-field>
        <mat-button-toggle-group name="kind" aria-label="Type" [(ngModel)]="kind" hideSingleSelectionIndicator="false">
          <mat-button-toggle value="physique">Physique</mat-button-toggle>
          <mat-button-toggle value="mental">Mental</mat-button-toggle>
        </mat-button-toggle-group>
        <button mat-flat-button type="submit" [disabled]="busy() || !name.trim()">Ajouter</button>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .list { display: flex; flex-direction: column; }
    .row { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 4px 4px 4px 16px; }
    .row + .row { border-top: 1px solid var(--mat-sys-outline-variant); }
    .row.is-off .name { color: var(--mat-sys-outline); text-decoration: line-through; }
    .add { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .add mat-form-field { flex: 1 1 200px; }
  `],
})
export class SymptomListDialogComponent {
  private readonly api = inject(HealthApiService)
  readonly types = signal(inject<SymptomTypeDto[]>(MAT_DIALOG_DATA))
  readonly busy = signal(false)
  readonly error = signal('')
  name = ''
  kind: SymptomKind = 'physique'

  async add(): Promise<void> {
    if (!this.name.trim() || this.busy()) return
    await this.run(async () => {
      const type = await firstValueFrom(this.api.createSymptomType(this.name.trim(), this.kind))
      this.types.update(list => [...list, type])
      this.name = ''
    })
  }

  archive(t: SymptomTypeDto): Promise<void> {
    return this.run(async () => {
      const updated = await firstValueFrom(this.api.archiveSymptomType(t.id, t.isArchived))
      this.types.update(list => list.map(x => x.id === updated.id ? updated : x))
    })
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true); this.error.set('')
    try { await action() } catch (error) { this.error.set(apiErrorMessage(error)) } finally { this.busy.set(false) }
  }
}
