import { Component, DestroyRef, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatButtonToggleModule } from '@angular/material/button-toggle'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { EditorFrameComponent } from '@/app/shared/ui/editor-frame.component'
import { HealthImageComponent } from './health-image.component'
import { HealthApiService } from './health-api.service'
import { MOMENTS, supplementDraftOf, supplementRequest, UNITS } from './supplement-rules'
import type { SupplementDto } from './health.models'

export interface SupplementDialogData { supplement: SupplementDto | null, today: string }
export interface SupplementDialogResult { supplement: SupplementDto, message: string }

/**
 * Creates or edits a supplement (ADR 0018): photo, name, daily dose and unit, moment, package (contents,
 * price, shop), purchase links, period (all year or a cure, repeated after a pause). Stopped from here.
 */
@Component({
  selector: 'app-supplement-dialog',
  standalone: true,
  imports: [EditorFrameComponent, FormsModule, HealthImageComponent, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <app-editor-frame [heading]="supplement ? 'Modifier le complément' : 'Nouveau complément'" formId="supplement-form"
      [saveLabel]="supplement ? 'Enregistrer' : 'Ajouter'" [busy]="busy()" [dirty]="dirty()" [error]="error()"
      [dangerLabel]="supplement && !supplement.isStopped ? 'Arrêter le complément' : ''" dangerIcon="block" (danger)="stop()">
      <form id="supplement-form" class="lo-form" (ngSubmit)="save()">
        <div class="lo-field-group">
          <span class="lo-field-label">Photo</span>
          <div class="photo-row">
            <div class="photo">
              @if (preview(); as source) {
                <img [src]="source" alt="Nouvelle photo du complément" />
              } @else if (supplement && supplement.hasImage && !removePhoto()) {
                <app-health-image [path]="'/health/supplements/' + supplement.id + '/image'" [name]="supplement.name" [size]="104" [alt]="'Photo : ' + supplement.name" />
              } @else {
                <span class="photo-empty" role="img" [attr.aria-label]="'Pas de photo, initiale ' + letter()">{{ letter() }}</span>
              }
              <label class="photo-edit" [attr.title]="hasPhoto() ? 'Changer la photo' : 'Ajouter une photo'">
                <mat-icon svgIcon="photo_camera" /><span class="sr-only">{{ hasPhoto() ? 'Changer la photo' : 'Ajouter une photo' }}</span>
                <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" (change)="pick($event)" />
              </label>
            </div>
            <div class="photo-side">
              <span class="lo-help">Facultative, chiffrée sur le serveur. Sans photo, la carte affiche l’initiale.</span>
              @if (hasPhoto()) { <button mat-button type="button" class="danger" (click)="dropPhoto()"><mat-icon svgIcon="delete" />Retirer la photo</button> }
            </div>
          </div>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Nom</mat-label>
          <input matInput name="name" required maxlength="200" placeholder="Ex. Magnésium bisglycinate" [(ngModel)]="draft.name" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="short">
          <mat-label>Dose par jour</mat-label>
          <input matInput name="dose" required inputmode="decimal" [(ngModel)]="draft.dose" />
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Unité</span>
          <div class="lo-chips" role="group" aria-label="Unité">
            @for (u of units; track u.value) {
              <button type="button" class="lo-filter-chip" [class.is-on]="draft.unit === u.value" [attr.aria-pressed]="draft.unit === u.value" (click)="draft.unit = u.value">
                @if (draft.unit === u.value) { <mat-icon svgIcon="check" /> }{{ u.label }}</button>
            }
          </div>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Moment</span>
          <div class="lo-chips" role="group" aria-label="Moment">
            @for (m of moments; track m.value) {
              <button type="button" class="lo-filter-chip" [class.is-on]="draft.moment === m.value" [attr.aria-pressed]="draft.moment === m.value" (click)="draft.moment = m.value">
                @if (draft.moment === m.value) { <mat-icon svgIcon="check" /> }{{ m.label }}</button>
            }
          </div>
        </div>
        <div class="lo-form-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Contenu du conditionnement</mat-label>
            <input matInput name="content" required inputmode="decimal" placeholder="Ex. 120" [(ngModel)]="draft.packageContent" />
            <mat-hint>Dans l’unité de la dose : 120 gélules, 500 g…</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Prix payé (€)</mat-label>
            <input matInput name="price" inputmode="decimal" placeholder="Ex. 24,90" [(ngModel)]="draft.packagePrice" />
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Acheté chez</mat-label>
          <input matInput name="shop" maxlength="200" placeholder="Magasin ou site" [(ngModel)]="draft.shop" />
        </mat-form-field>
        <div class="lo-field-group">
          <span class="lo-field-label">Liens d’achat</span>
          @for (link of draft.links; track $index; let i = $index) {
            <div class="link-row">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="link-name">
                <mat-label>Nom</mat-label>
                <input matInput [name]="'linkLabel' + i" maxlength="100" placeholder="Facultatif" [(ngModel)]="link.label" />
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="link-url">
                <mat-label>Adresse</mat-label>
                <input matInput [name]="'linkUrl' + i" type="url" maxlength="1000" placeholder="https://…" [(ngModel)]="link.url" />
              </mat-form-field>
              <button mat-icon-button type="button" class="danger" (click)="removeLink(i)" [attr.aria-label]="'Retirer le lien ' + (i + 1)" title="Retirer le lien">
                <mat-icon svgIcon="delete" /></button>
            </div>
          }
          <div><button mat-stroked-button type="button" (click)="addLink()"><mat-icon svgIcon="add" />Ajouter un lien</button></div>
        </div>
        <div class="lo-field-group">
          <span class="lo-field-label">Période</span>
          <mat-button-toggle-group name="kind" aria-label="Période" [(ngModel)]="draft.kind" hideSingleSelectionIndicator="false">
            <mat-button-toggle value="year">À l’année</mat-button-toggle>
            <mat-button-toggle value="cure">En cure</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        @if (draft.kind === 'cure') {
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Durée de la cure</mat-label>
              <input matInput name="cure" type="number" min="1" max="52" step="1" [(ngModel)]="draft.cureLength" />
            </mat-form-field>
            <mat-button-toggle-group name="cureUnit" aria-label="Unité de la cure" [(ngModel)]="draft.cureUnit" hideSingleSelectionIndicator="false">
              <mat-button-toggle value="week">Semaines</mat-button-toggle>
              <mat-button-toggle value="month">Mois</mat-button-toggle>
            </mat-button-toggle-group>
          </div>
          <div class="lo-form-row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Pause avant de recommencer</mat-label>
              <input matInput name="pause" type="number" min="0" max="52" step="1" [(ngModel)]="draft.pauseLength" />
              <mat-hint>0 : une seule cure.</mat-hint>
            </mat-form-field>
            <mat-button-toggle-group name="pauseUnit" aria-label="Unité de la pause" [(ngModel)]="draft.pauseUnit" hideSingleSelectionIndicator="false">
              <mat-button-toggle value="week">Semaines</mat-button-toggle>
              <mat-button-toggle value="month">Mois</mat-button-toggle>
            </mat-button-toggle-group>
          </div>
        }
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="short">
          <mat-label>À partir du</mat-label>
          <input matInput type="date" name="startOn" required [(ngModel)]="draft.startOn" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Notes</mat-label>
          <textarea matInput name="notes" rows="3" maxlength="4000" [(ngModel)]="draft.notes"></textarea>
        </mat-form-field>
      </form>
    </app-editor-frame>
  `,
  styles: [`
    .photo-row { display: flex; align-items: flex-end; gap: 16px; }
    .photo { position: relative; width: 104px; height: 104px; flex: 0 0 auto; }
    .photo img, .photo-empty { display: flex; width: 104px; height: 104px; border-radius: 16px; object-fit: cover; }
    .photo-empty { align-items: center; justify-content: center; font-size: 44px; font-weight: 600;
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
    .photo-edit { position: absolute; right: -8px; bottom: -8px; display: inline-flex; width: 40px; height: 40px; align-items: center; justify-content: center;
      border-radius: 12px; background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); box-shadow: var(--mat-sys-level2); cursor: pointer; }
    .photo-edit:focus-within { outline: 3px solid var(--mat-sys-primary); outline-offset: 2px; }
    .photo-side { display: flex; min-width: 0; flex-direction: column; align-items: flex-start; gap: 4px; }
    .danger { color: var(--mat-sys-error); }
    .short { max-width: 280px; }
    .lo-form-row { align-items: center; }
    .link-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .link-name { flex: 1 1 140px; max-width: 220px; }
    .link-url { flex: 3 1 240px; }
  `],
})
export class SupplementDialogComponent {
  private readonly api = inject(HealthApiService)
  private readonly ref = inject<MatDialogRef<SupplementDialogComponent, SupplementDialogResult>>(MatDialogRef)
  private readonly data = inject<SupplementDialogData>(MAT_DIALOG_DATA)
  readonly supplement = this.data.supplement
  readonly units = UNITS
  readonly moments = MOMENTS
  readonly busy = signal(false)
  readonly error = signal('')
  readonly preview = signal<string | null>(null)
  readonly removePhoto = signal(false)
  readonly draft = supplementDraftOf(this.supplement, this.data.today)
  private file: File | null = null
  private readonly initial = formSnapshot(this.draft)

  constructor() {
    inject(DestroyRef).onDestroy(() => this.releasePreview())
  }

  letter(): string { return (this.draft.name.trim().charAt(0) || '?').toLocaleUpperCase('fr') }
  hasPhoto(): boolean { return !!this.preview() || (!!this.supplement?.hasImage && !this.removePhoto()) }
  dirty(): boolean { return hasChanges(this.initial, this.draft) || !!this.file || this.removePhoto() }
  addLink(): void { this.draft.links = [...this.draft.links, { label: '', url: '' }] }
  removeLink(index: number): void { this.draft.links = this.draft.links.filter((_, i) => i !== index) }

  pick(event: Event): void {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return
    this.releasePreview()
    this.file = file
    this.removePhoto.set(false)
    this.preview.set(URL.createObjectURL(file))
  }

  dropPhoto(): void {
    this.releasePreview()
    this.file = null
    this.removePhoto.set(!!this.supplement?.hasImage)
  }

  async save(): Promise<void> {
    if (this.busy()) return
    const request = supplementRequest(this.draft)
    if (typeof request === 'string') { this.error.set(request); return }
    this.busy.set(true); this.error.set('')
    try {
      let supplement = await firstValueFrom(this.supplement ? this.api.updateSupplement(this.supplement.id, request) : this.api.createSupplement(request))
      if (this.file) supplement = await firstValueFrom(this.api.uploadSupplementImage(supplement.id, this.file))
      else if (this.removePhoto() && supplement.hasImage) {
        await firstValueFrom(this.api.removeSupplementImage(supplement.id))
        supplement = { ...supplement, hasImage: false }
      }
      this.ref.close({ supplement, message: this.supplement ? 'Complément enregistré' : 'Complément ajouté' })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  async stop(): Promise<void> {
    if (!this.supplement || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      const supplement = await firstValueFrom(this.api.stopSupplement(this.supplement.id))
      this.ref.close({ supplement, message: `« ${this.supplement.name} » arrêté ; son historique reste` })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.busy.set(false)
    }
  }

  private releasePreview(): void {
    const url = this.preview()
    if (url) URL.revokeObjectURL(url)
    this.preview.set(null)
  }
}
