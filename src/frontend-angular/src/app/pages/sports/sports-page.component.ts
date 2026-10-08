import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { SportTemplateDto, SportTemplateRequest } from '@/app/core/api/api.models'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'

const empty = (): SportTemplateRequest => ({
  name: '', sport: 'run', durationMinutes: 30, distanceKm: null, intensity: 'moderate', calories: 0,
})

@Component({
  selector: 'app-sports-page',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent],
  template: `
    <section class="page-stack sports-page">
      <header class="page-hero">
        <div><h1>Séances sportives</h1><p>Vos modèles, réutilisables sans modifier les séances déjà planifiées.</p></div>
        <button type="button" class="lifeos-button" (click)="openCreate()"><i class="pi pi-plus" aria-hidden="true"></i> Nouvelle séance</button>
      </header>
      @if (error()) { <p class="sport-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      <section class="surface-card">
        <div class="sport-library-heading">
          <h2>Votre bibliothèque</h2>
          <label class="sport-search"><span class="sr-only">Rechercher une séance</span><input type="search" class="text-input" placeholder="Rechercher" [(ngModel)]="search" /></label>
        </div>
        @if (loading()) { <p role="status">Chargement…</p> }
        @if (!loading() && !items().length) { <p>Aucun modèle. Ajoutez votre première séance ; aucun exemple n’est importé automatiquement.</p> }
        @for (item of visible(); track item.id) {
          <div class="sport-row"><div><strong>{{ item.name }}</strong><p>{{ item.sport }} · {{ item.durationMinutes }} min · {{ item.calories }} kcal saisies</p>
            @if (item.distanceKm !== null) { <p>{{ item.distanceKm }} km</p> }</div>
            <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="edit(item)">Modifier</button>
            <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="archive(item)">Archiver</button>
          </div>
        }
      </section>
    </section>

    <app-lifeos-dialog [open]="dialogOpen()" [heading]="editingId ? 'Modifier la séance' : 'Nouvelle séance'"
      description="Les calories sont un total saisi : modifier la durée ou la distance ne les recalcule pas."
      formId="sport-dialog-form" [submitLabel]="editingId ? 'Enregistrer' : 'Créer'" [busy]="busy()" [dirty]="isDirty()"
      [error]="dialogError()" (dismissed)="closeDialog()">
      <form id="sport-dialog-form" class="dialog-form" ngNativeValidate (ngSubmit)="save()">
        <label class="dialog-field"><span>Nom</span><input class="text-input" name="name" required maxlength="200" [(ngModel)]="draft.name" /></label>
        <div class="dialog-row">
          <label class="dialog-field"><span>Sport</span><input class="text-input" name="sport" required maxlength="50" list="sport-types" [(ngModel)]="draft.sport" /></label>
          <label class="dialog-field"><span>Durée (min)</span><input class="text-input" name="duration" type="number" min="1" max="1440" step="1" required [(ngModel)]="draft.durationMinutes" /></label>
          <label class="dialog-field"><span>Calories estimées</span><input class="text-input" name="calories" type="number" min="0" step="any" required [(ngModel)]="draft.calories" /></label>
        </div>
        <datalist id="sport-types"><option value="run">Course</option><option value="bike">Vélo</option><option value="strength">Musculation</option><option value="walk">Marche</option></datalist>
        <details class="dialog-more" [open]="showMore">
          <summary>Distance et intensité</summary>
          <div class="dialog-row">
            <label class="dialog-field"><span>Distance (km, facultative)</span><input class="text-input" name="distance" type="number" min="0" step="any" [(ngModel)]="draft.distanceKm" /></label>
            <label class="dialog-field"><span>Intensité</span><select class="text-input" name="intensity" [(ngModel)]="draft.intensity"><option value="low">Faible</option><option value="moderate">Modérée</option><option value="high">Élevée</option></select></label>
          </div>
        </details>
      </form>
    </app-lifeos-dialog>
  `,
  styles: [`
    h2 { margin-top: 0; } p { line-height: 1.5; }
    .sport-library-heading { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 8px; }
    .sport-library-heading h2 { margin: 0; }
    .sport-search { width: min(280px, 100%); }
    .sport-row { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; padding: 18px 0; border-bottom: 1px solid var(--lifeos-line); }
    .sport-row div { flex: 1; min-width: 150px; } .sport-row p { margin: 6px 0; }
    .sport-error { color: #8b2c20; } button { min-height: 44px; }
    @media (max-width: 760px) { .page-hero { flex-wrap: wrap; } }
  `],
})
export class SportsPageComponent {
  private readonly api = inject(LifeosApiService)
  readonly items = signal<SportTemplateDto[]>([])
  readonly error = signal('')
  readonly notice = signal('')
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly dialogOpen = signal(false)
  readonly dialogError = signal('')
  search = ''
  editingId = ''
  draft = empty()
  showMore = false
  private initialDraft = formSnapshot(this.draft)
  constructor() { void this.load() }
  visible(): SportTemplateDto[] {
    return this.items().filter(i => !i.isArchived && `${i.name} ${i.sport}`.toLocaleLowerCase('fr').includes(this.search.toLocaleLowerCase('fr')))
  }
  isDirty(): boolean { return hasChanges(this.initialDraft, this.draft) }
  openCreate(): void { this.startDraft('', empty()) }
  edit(item: SportTemplateDto): void {
    this.startDraft(item.id, { ...item })
  }
  closeDialog(): void { this.dialogOpen.set(false); this.dialogError.set(''); this.editingId = ''; this.draft = empty() }
  private startDraft(id: string, draft: SportTemplateRequest): void {
    this.editingId = id; this.draft = draft; this.initialDraft = formSnapshot(draft)
    this.showMore = draft.distanceKm !== null || draft.intensity !== 'moderate'
    this.dialogError.set(''); this.notice.set(''); this.dialogOpen.set(true)
  }
  async load(): Promise<void> {
    this.loading.set(true)
    try { this.items.set(await firstValueFrom(this.api.get<SportTemplateDto[]>('/sport-templates'))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }
  async save(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.dialogError.set(''); this.notice.set('')
    try {
      const item = await firstValueFrom(this.editingId
        ? this.api.put<SportTemplateDto>(`/sport-templates/${this.editingId}`, this.draft)
        : this.api.post<SportTemplateDto>('/sport-templates', this.draft))
      this.items.update(items => [...items.filter(i => i.id !== item.id), item])
      this.busy.set(false); this.closeDialog(); this.notice.set('Modèle enregistré. Les séances existantes ne changent pas.')
    } catch (error) { this.dialogError.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }
  async archive(item: SportTemplateDto): Promise<void> {
    if (!window.confirm(`Archiver « ${item.name} » ? Les séances existantes sont conservées.`)) return
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.delete(`/sport-templates/${item.id}`))
      this.items.update(items => items.map(i => i.id === item.id ? { ...i, isArchived: true } : i))
      this.notice.set('Modèle archivé.')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
