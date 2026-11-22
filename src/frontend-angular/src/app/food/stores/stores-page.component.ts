import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { FoodApiService } from '../food-api.service'
import type { StoreDto } from '../food.models'
import { StoreDialogComponent } from './store-dialog.component'

/** Stores where the household buys, used by prices and tickets. */
@Component({
  selector: 'app-stores-page',
  standalone: true,
  imports: [ListPageComponent, MatIconModule],
  template: `
    <app-list-page heading="Magasins" intro="Les magasins où le foyer fait ses courses, pour y relever des prix." addLabel="Ajouter un magasin"
      [(query)]="query" (add)="open(null)">
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (loading()) {
        <p class="lo-status" role="status">Chargement des magasins…</p>
      } @else if (shown().length) {
        <div class="lo-list">
          @for (store of shown(); track store.id) {
            <div class="lo-row">
              <button type="button" class="lo-row-main" (click)="open(store)">
                <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="storefront" /></span>
                <span class="lo-row-text"><span class="lo-row-title">{{ store.name }}</span>
                  @if (store.address) { <span class="lo-row-sub">{{ store.address }}</span> }</span>
                <span class="lo-tags">
                  @if (store.isOrganic) { <span class="lo-tag is-ok">Bio</span> }
                  @if (store.isLocal) { <span class="lo-tag is-ok">Local</span> }
                </span>
              </button>
            </div>
          }
        </div>
      } @else {
        <p class="lo-empty">{{ stores().length ? 'Aucun résultat.' : 'Aucun magasin. Ajoutez ceux où le foyer fait ses courses pour y relever des prix.' }}</p>
      }
    </app-list-page>
  `,
})
export class StoresPageComponent implements OnInit {
  private readonly api = inject(FoodApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly stores = signal<StoreDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  readonly query = signal('')
  readonly shown = computed(() => this.stores().filter(store => matchesQuery(`${store.name} ${store.address ?? ''}`, this.query())))

  async ngOnInit(): Promise<void> { await this.load() }

  async open(store: StoreDto | null): Promise<void> {
    const message = await firstValueFrom(openEditor<StoreDialogComponent, StoreDto | null, string>(this.dialog, StoreDialogComponent, store).afterClosed())
    if (!message) return
    this.snack.open(message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try {
      const stores = await firstValueFrom(this.api.stores())
      this.stores.set([...stores].sort((a, b) => a.name.localeCompare(b.name, 'fr')))
      this.error.set('')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }
}
