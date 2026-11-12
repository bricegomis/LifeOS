import { Component, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { FoodApiService } from '../food-api.service'
import type { StoreDto, StoreRequest } from '../food.models'

@Component({
  selector: 'app-stores-page',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent],
  template: `
    <div class="page page-narrow">
      <div class="page-header">
        <h1>Magasins</h1>
        <button type="button" class="lifeos-button" (click)="open(null)">Nouveau magasin</button>
      </div>
      @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
      @if (loading()) {
        <p class="status-text" role="status">Chargement des magasins…</p>
      } @else if (!stores().length) {
        <p class="empty-state">Aucun magasin. Ajoutez ceux où le foyer fait ses courses pour y relever des prix.</p>
      } @else {
        <div class="table">
          @for (store of stores(); track store.id) {
            <button type="button" class="table-row store-row" (click)="open(store)">
              <span class="store-text"><strong>{{ store.name }}</strong>
                @if (store.address) { <span class="muted small">{{ store.address }}</span> }</span>
              <span class="tags">
                @if (store.isOrganic) { <span class="chip chip-ok">Bio</span> }
                @if (store.isLocal) { <span class="chip chip-ok">Local</span> }
              </span>
            </button>
          }
        </div>
      }
    </div>

    <app-lifeos-dialog [open]="editing() !== undefined" [heading]="editing() ? 'Modifier le magasin' : 'Nouveau magasin'" [centered]="true"
      formId="store-form" [submitLabel]="editing() ? 'Enregistrer' : 'Créer le magasin'" [busy]="busy()" [dirty]="dirty()"
      [error]="dialogError()" (dismissed)="editing.set(undefined)">
      <form id="store-form" class="store-form" (ngSubmit)="save()">
        <label class="field"><span>Nom</span>
          <input class="lifeos-input" name="name" maxlength="200" required [(ngModel)]="values.name" /></label>
        <label class="field"><span>Adresse (facultatif)</span>
          <input class="lifeos-input" name="address" maxlength="400" [(ngModel)]="values.address" /></label>
        <label class="check"><input type="checkbox" name="organic" [(ngModel)]="values.isOrganic" /> Magasin bio</label>
        <label class="check"><input type="checkbox" name="local" [(ngModel)]="values.isLocal" /> Commerce local</label>
      </form>
      @if (editing()) {
        <button dialogExtraActions type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="remove()">Supprimer</button>
      }
    </app-lifeos-dialog>
  `,
  styles: [`
    .store-row { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; padding: 16px 20px; }
    .store-row:first-child { border-top: 0; }
    .store-text { display: flex; flex-direction: column; }
    .store-form { display: flex; flex-direction: column; gap: 14px; }
    .check { display: flex; align-items: center; gap: 10px; min-height: 44px; }
    .check input { width: 20px; height: 20px; }
  `],
})
export class StoresPageComponent implements OnInit {
  private readonly api = inject(FoodApiService)
  readonly stores = signal<StoreDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  /** `undefined`: closed; `null`: new store. */
  readonly editing = signal<StoreDto | null | undefined>(undefined)
  readonly busy = signal(false)
  readonly dialogError = signal('')
  values: StoreRequest = { name: '', address: '', isOrganic: false, isLocal: false }
  private initial = formSnapshot(this.values)

  async ngOnInit(): Promise<void> {
    try { this.stores.set(await firstValueFrom(this.api.stores())) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }

  dirty(): boolean { return hasChanges(this.initial, this.values) }

  open(store: StoreDto | null): void {
    this.values = store
      ? { name: store.name, address: store.address ?? '', isOrganic: store.isOrganic, isLocal: store.isLocal }
      : { name: '', address: '', isOrganic: false, isLocal: false }
    this.initial = formSnapshot(this.values)
    this.dialogError.set('')
    this.editing.set(store)
  }

  async save(): Promise<void> {
    const name = this.values.name.trim()
    if (!name) { this.dialogError.set('Indiquez le nom du magasin.'); return }
    const request = { ...this.values, name, address: this.values.address.trim() }
    const store = this.editing()
    await this.run(async () => {
      const saved = store ? await firstValueFrom(this.api.updateStore(store.id, request)) : await firstValueFrom(this.api.createStore(request))
      this.stores.update(list => [...list.filter(s => s.id !== saved.id), saved].sort((a, b) => a.name.localeCompare(b.name, 'fr')))
      this.editing.set(undefined)
    })
  }

  async remove(): Promise<void> {
    const store = this.editing()
    if (!store || !window.confirm(`Supprimer « ${store.name} » ? Impossible tant que des prix ou des tickets y sont rattachés.`)) return
    await this.run(async () => {
      await firstValueFrom(this.api.deleteStore(store.id))
      this.stores.update(list => list.filter(s => s.id !== store.id))
      this.editing.set(undefined)
    })
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true)
    this.dialogError.set('')
    try { await action() } catch (error) { this.dialogError.set(apiErrorMessage(error)) } finally { this.busy.set(false) }
  }
}
