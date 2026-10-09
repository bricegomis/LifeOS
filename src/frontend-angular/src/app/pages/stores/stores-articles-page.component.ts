import { firstValueFrom } from 'rxjs'
import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { StoreDto, StoreRequest } from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'

const emptyStoreEditor = () => ({ name: '', address: '', isOrganic: false, isLocal: false })

@Component({
  selector: 'app-stores-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent],
  template: `
    <section class="page-stack stores-page" aria-labelledby="stores-page-title">
      <header class="page-hero">
        <div>
          <h1 id="stores-page-title">Magasins</h1>
          <p>Les lieux où vous faites vos courses. Les achats et les prix se renseignent dans la <a routerLink="/products">fiche produit</a>.</p>
        </div>
        <button class="lifeos-button" type="button" [disabled]="busy()" (click)="openStoreDialog()">
          <i class="pi pi-plus" aria-hidden="true"></i> Nouveau magasin
        </button>
      </header>
      @if (error()) { <p class="stores-status stores-status-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="stores-status stores-status-success" role="status">{{ notice() }}</p> }
      <section class="stores-directory" aria-labelledby="stores-heading">
        <div class="stores-directory-heading">
          <div>
            <h2 id="stores-heading">Vos magasins</h2>
            <p>{{ stores().length }} {{ stores().length === 1 ? 'magasin enregistré' : 'magasins enregistrés' }}</p>
          </div>
          <label class="stores-search">
            <span class="sr-only">Filtrer les magasins</span>
            <input class="stores-native-input" type="search" name="storeSearch" placeholder="Filtrer les magasins" [(ngModel)]="storeSearch" />
          </label>
        </div>
        @if (loading() && stores().length === 0) {
          <p class="stores-empty-inline" role="status">Chargement des magasins…</p>
        } @else if (filteredStores().length === 0) {
          <div class="stores-empty">
            <h3>{{ storeSearch ? 'Aucun magasin correspondant' : 'Aucun magasin pour le moment' }}</h3>
            <p>{{ storeSearch ? 'Essayez un autre nom ou effacez le filtre.' : 'Ajoutez un magasin pour relever les prix par lieu dans vos fiches produits.' }}</p>
          </div>
        } @else {
          <div class="stores-list">
            @for (store of filteredStores(); track store.id) {
              <article class="store-row">
                <span class="store-row-icon" aria-hidden="true">⌂</span>
                <div class="store-row-content">
                  <h3>{{ store.name }}</h3>
                  @if (store.address) { <p>{{ store.address }}</p> }
                  <div class="store-badges">
                    @if (store.isOrganic) { <span>Bio</span> }
                    @if (store.isLocal) { <span>Local</span> }
                  </div>
                </div>
                <div class="store-row-actions">
                  <button class="stores-native-button" type="button" [disabled]="busy()" (click)="openStoreDialog(store)">Modifier</button>
                  @if (storeDeleteConfirmationId() === store.id) {
                    <button class="stores-native-button is-danger" type="button" [disabled]="busy()" (click)="deleteStore(store)">Confirmer la suppression</button>
                    <button class="stores-native-button" type="button" (click)="storeDeleteConfirmationId.set('')">Conserver</button>
                  } @else {
                    <button class="stores-native-button is-danger" type="button" [disabled]="busy()" (click)="storeDeleteConfirmationId.set(store.id)">Supprimer</button>
                  }
                </div>
              </article>
            }
          </div>
        }
      </section>
    </section>
    <app-lifeos-dialog [open]="editorOpen()" [heading]="editingStoreId() ? 'Modifier le magasin' : 'Nouveau magasin'"
      formId="store-dialog-form" [submitLabel]="editingStoreId() ? 'Enregistrer' : 'Créer'"
      [busy]="busy()" [dirty]="isDirty()" [error]="dialogError()" (dismissed)="closeDialog()">
      <form id="store-dialog-form" class="dialog-form" ngNativeValidate (ngSubmit)="saveStore()">
        <label class="dialog-field"><span>Nom du magasin</span>
          <input class="text-input" name="storeName" type="text" required maxlength="160" [(ngModel)]="storeEditor.name" /></label>
        <details class="dialog-more" [open]="showMore">
          <summary>Adresse, bio, commerce local</summary>
          <div>
            <label class="dialog-field"><span>Adresse (facultatif)</span>
              <input class="text-input" name="storeAddress" type="text" maxlength="300" [(ngModel)]="storeEditor.address" /></label>
            <label class="dialog-check"><input name="isOrganic" type="checkbox" [(ngModel)]="storeEditor.isOrganic" /> Magasin bio</label>
            <label class="dialog-check"><input name="isLocal" type="checkbox" [(ngModel)]="storeEditor.isLocal" /> Commerce local</label>
          </div>
        </details>
      </form>
    </app-lifeos-dialog>
  `,
  styles: [`
    :host { display: block; }
    .stores-native-input { width: 100%; min-height: 42px; padding: 9px 11px; border: 1px solid var(--lifeos-line); border-radius: 10px; background: var(--lifeos-surface); color: var(--lifeos-text); font: inherit; }
    .stores-native-input:focus-visible { border-color: var(--lifeos-accent); outline: 3px solid rgb(53 109 79 / 24%); }
    .stores-native-button { min-height: 38px; padding: 8px 11px; border: 1px solid var(--lifeos-line); border-radius: 9px; background: var(--lifeos-surface-soft); color: var(--lifeos-text); font: inherit; font-size: .86rem; font-weight: 750; cursor: pointer; }
    .stores-native-button:hover:not(:disabled) { background: var(--lifeos-accent-soft); }
    .stores-native-button:focus-visible { outline: 3px solid rgb(53 109 79 / 28%); outline-offset: 1px; }
    .stores-native-button:disabled { cursor: wait; opacity: .65; }
    .stores-native-button.is-danger { color: #8b2c20; }
    .stores-search { min-width: 190px; }
    .stores-list { width: 100%; }
    .store-row-actions { justify-content: flex-end; }
    .stores-status { margin: 0; padding: 12px 14px; border-radius: 12px; font-weight: 750; line-height: 1.45; }
    .stores-status-error { background: #fff0ed; color: #8b2c20; }
    .stores-status-success { background: var(--lifeos-accent-soft); color: var(--lifeos-accent-strong); }
    @media (max-width: 620px) {
      .stores-search { width: 100%; min-width: 0; }
      .store-row-actions { justify-content: flex-start; }
    }
  `],
})
export class StoresArticlesPageComponent {
  private readonly api = inject(LifeosApiService)
  readonly stores = signal<StoreDto[]>([])
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly editingStoreId = signal('')
  readonly storeDeleteConfirmationId = signal('')
  readonly editorOpen = signal(false)
  readonly dialogError = signal('')
  storeEditor = emptyStoreEditor()
  storeSearch = ''
  showMore = false
  private initialSnapshot = ''

  constructor() { void this.loadData() }

  filteredStores(): StoreDto[] {
    const query = this.storeSearch.trim().toLocaleLowerCase('fr')
    return this.stores().filter(store => `${store.name} ${store.address ?? ''}`.toLocaleLowerCase('fr').includes(query))
  }

  async loadData(): Promise<void> {
    this.loading.set(true)
    try {
      this.stores.set(await firstValueFrom(this.api.get<StoreDto[]>('/stores')))
      this.error.set('')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }

  isDirty(): boolean { return hasChanges(this.initialSnapshot, this.storeEditor) }
  closeDialog(): void { this.editorOpen.set(false); this.dialogError.set('') }

  openStoreDialog(store?: StoreDto): void {
    this.editingStoreId.set(store?.id ?? '')
    this.storeEditor = store ? { name: store.name, address: store.address ?? '', isOrganic: store.isOrganic, isLocal: store.isLocal } : emptyStoreEditor()
    this.showMore = Boolean(this.storeEditor.address || this.storeEditor.isOrganic || this.storeEditor.isLocal)
    this.initialSnapshot = formSnapshot(this.storeEditor)
    this.dialogError.set('')
    this.notice.set('')
    this.editorOpen.set(true)
  }

  async saveStore(): Promise<void> {
    if (this.busy()) return
    const request: StoreRequest = { ...this.storeEditor, name: this.storeEditor.name.trim(), address: this.storeEditor.address.trim() || null }
    if (!request.name) { this.dialogError.set('Indiquez le nom du magasin.'); return }
    this.busy.set(true)
    this.dialogError.set('')
    let persisted = false
    try {
      const id = this.editingStoreId()
      await firstValueFrom(id ? this.api.put<StoreDto>(`/stores/${id}`, request) : this.api.post<StoreDto>('/stores', request))
      persisted = true
      this.closeDialog()
      this.stores.set(await firstValueFrom(this.api.get<StoreDto[]>('/stores')))
      this.error.set('')
      this.notice.set('Magasin enregistré.')
    } catch (error) {
      if (persisted) this.error.set(`Magasin enregistré, mais l’actualisation a échoué. ${apiErrorMessage(error)}`)
      else this.dialogError.set(apiErrorMessage(error))
    } finally { this.busy.set(false) }
  }

  async deleteStore(store: StoreDto): Promise<void> {
    this.busy.set(true)
    this.error.set('')
    this.notice.set('')
    try {
      await firstValueFrom(this.api.delete<void>(`/stores/${store.id}`))
      this.storeDeleteConfirmationId.set('')
      this.stores.set(await firstValueFrom(this.api.get<StoreDto[]>('/stores')))
      this.notice.set(`Le magasin « ${store.name} » a été supprimé.`)
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
