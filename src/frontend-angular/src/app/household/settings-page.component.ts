import { Component, inject, OnInit, signal } from '@angular/core'
import { Router } from '@angular/router'
import { FormsModule } from '@angular/forms'
import { firstValueFrom, timeout } from 'rxjs'
import { parseApiBuildInfo, type BuildInfo } from '@/app/core/build-info'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { MemberProfileDto } from '@/app/core/api/api.models'
import { AuthService } from '@/app/core/auth/auth.service'
import { apiBaseUrl, buildInfo } from '@/environments/environment'
import { BuildVersionComponent } from '@/app/shared/build-version.component'

import { NativeSyncService } from '@/app/core/native/native-sync.service'
import { DeepLinkService } from '@/app/core/native/deep-link.service'
import { nativeBridgeStatus } from '@/app/core/native/native-bridge'

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [FormsModule, BuildVersionComponent],
  template: `
    <section class="page page-narrow">
      <div class="page-header"><h1>Paramètres</h1></div>
      @if (error()) { <p class="api-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="muted" role="status">{{ notice() }}</p> }

      <section class="card settings-section">
        <div>
          <h2>Profils du foyer</h2>
          <p class="muted">Chaque personne donne ses avis. Le coefficient suggère une portion, sans l’imposer. Un profil peut représenter un enfant sans compte.</p>
        </div>
        @if (profiles().length) {
          <div class="profile-grid table-head" aria-hidden="true"><span>Profil</span><span>Coefficient de portion</span><span></span></div>
          @for (profile of profiles(); track profile.id) {
            <div class="profile-grid profile-row">
              <strong>{{ profile.displayName }}</strong>
              <span>{{ profile.portionCoefficient }}</span>
              <span class="page-actions">
                <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="busy()" (click)="editProfile(profile)">Modifier</button>
                <button type="button" class="lifeos-button lifeos-button-secondary lifeos-button-sm" [disabled]="busy()" (click)="deleteProfile(profile)">Supprimer</button>
              </span>
            </div>
          }
        } @else {
          <p class="muted">Aucun profil. Ajoutez les personnes du foyer pour saisir leurs avis.</p>
        }
        <form class="settings-form" (ngSubmit)="saveProfile()">
          <label class="field"><span>Nom affiché</span><input class="lifeos-input" name="profileName" required maxlength="200" [(ngModel)]="profileName" /></label>
          <label class="field"><span>Coefficient de portion</span><input class="lifeos-input" name="profileCoefficient" type="number" min="0.01" max="100" step="any" required [(ngModel)]="profileCoefficient" /></label>
          <button class="lifeos-button" type="submit" [disabled]="busy()">{{ editingProfileId() ? 'Enregistrer' : 'Ajouter un profil' }}</button>
          @if (editingProfileId()) { <button class="lifeos-button lifeos-button-secondary" type="button" (click)="cancelProfileEdit()">Annuler</button> }
        </form>
      </section>

      <section class="card">
        <h2>Versions</h2>
        <dl class="versions">
          <dt>Interface</dt><dd><app-build-version component="UI" [info]="buildInfo" /></dd>
          <dt>API</dt><dd><app-build-version component="API" [info]="apiBuildInfo()" [status]="apiBuildStatus()" /></dd>
          <dt>Adresse de l’API</dt><dd>{{ apiUrl || 'Non configurée' }}</dd>
        </dl>
      </section>

      <section class="card">
        <h2>Compte</h2>
        <p>{{ auth.user()?.email ?? 'Compte du foyer' }}</p>
        <p class="muted small">Les données du foyer sont conservées par LifeOS API ; Supabase assure uniquement la connexion.</p>
        <div class="mobile-only" style="margin-top: 12px">
          <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="signOut()">Déconnexion</button>
        </div>
      </section>

      @if (bridgeStatus() !== 'web') {
        <section class="card">
          <h2>iPhone</h2>
          <dl class="versions">
            <dt>Pont natif</dt>
            <dd>{{ bridgeStatus() === 'available' ? 'Disponible' : 'Non enregistré : reconstruire dans Xcode' }}</dd>
            @if (deepLink.lastError()) { <dt>Incident ouverture de lien</dt><dd role="alert">{{ deepLink.lastError() }}</dd> }
            <dt>Synchronisation</dt>
            <dd>
              @switch (nativeSync.syncStatus()) {
                @case ('synced') { À jour }
                @case ('syncing') { Synchronisation en cours… }
                @case ('stale') { Données en cache (réseau indisponible) }
                @case ('error') { <span class="error-text">Erreur de synchronisation</span> }
                @default { En attente }
              }
            </dd>
            @if (nativeSync.lastSyncError()) { <dt>Dernier incident</dt><dd class="error-text">{{ nativeSync.lastSyncError() }}</dd> }
            @if (nativeSync.lastSyncedAt()) { <dt>Dernière synchronisation</dt><dd>{{ nativeSync.lastSyncedAt() }}</dd> }
          </dl>
          <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy() || nativeSync.syncStatus() === 'syncing'" (click)="triggerNativeSync()">
            Synchroniser maintenant
          </button>
        </section>
      }
    </section>
  `,
  styles: [`
    .settings-section { display: flex; flex-direction: column; gap: 12px; }
    .settings-section h2 { margin-bottom: 4px; }
    .profile-grid { display: grid; grid-template-columns: minmax(0, 1fr) 160px auto; align-items: center; gap: 12px; padding: 0; }
    .profile-row { padding: 12px 0; border-top: 1px solid var(--lifeos-line); }
    .settings-form { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 12px; padding-top: 12px; border-top: 1px solid var(--lifeos-line); }
    .versions { display: grid; grid-template-columns: 160px minmax(0, 1fr); gap: 12px; margin: 0; }
    .versions dt { font-weight: 500; }
    .versions dd { margin: 0; color: var(--lifeos-text-soft); overflow-wrap: anywhere; }
    @media (max-width: 560px) {
      .profile-grid { grid-template-columns: minmax(0, 1fr) auto; }
      .profile-grid > :last-child { grid-column: 1 / -1; }
      .versions { grid-template-columns: minmax(0, 1fr); gap: 4px; }
    }
  `],
})
export class SettingsPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)
  readonly auth = inject(AuthService)
  readonly nativeSync = inject(NativeSyncService)
  readonly deepLink = inject(DeepLinkService)
  readonly bridgeStatus = nativeBridgeStatus
  readonly apiUrl = apiBaseUrl
  readonly buildInfo = buildInfo
  readonly apiBuildInfo = signal<BuildInfo | null>(null)
  readonly apiBuildStatus = signal(apiBaseUrl ? 'chargement…' : 'non configurée')
  private readonly router = inject(Router)
  readonly profiles = signal<MemberProfileDto[]>([])
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly editingProfileId = signal('')
  profileName = ''
  profileCoefficient = 1

  ngOnInit(): void {
    void this.load()
    void this.loadApiVersion()
  }

  private async loadApiVersion(): Promise<void> {
    if (!apiBaseUrl) return
    try {
      this.apiBuildInfo.set(parseApiBuildInfo(await firstValueFrom(this.api.get<unknown>('/version').pipe(timeout({ first: 5000 })))))
    } catch {
      this.apiBuildStatus.set('indisponible')
    }
  }

  /** On the phone there is no side menu: sign-out lives here. */
  async signOut(): Promise<void> {
    this.busy.set(true)
    this.error.set('')
    try {
      await this.nativeSync.purgeAll()
      await this.auth.signOut()
      await this.router.navigateByUrl('/login')
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Impossible de terminer la déconnexion.')
    } finally {
      this.busy.set(false)
    }
  }

  async triggerNativeSync(): Promise<void> {
    await this.nativeSync.syncNativeState()
  }

  async load(): Promise<void> {
    this.error.set('')
    try {
      const household = await firstValueFrom(this.api.get<{ memberProfiles: MemberProfileDto[] }>('/households/members'))
      this.profiles.set(household.memberProfiles)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    }
  }

  editProfile(profile: MemberProfileDto): void {
    this.editingProfileId.set(profile.id)
    this.profileName = profile.displayName
    this.profileCoefficient = profile.portionCoefficient
    this.notice.set('')
  }

  cancelProfileEdit(): void {
    this.editingProfileId.set('')
    this.profileName = ''
    this.profileCoefficient = 1
  }

  async saveProfile(): Promise<void> {
    const displayName = this.profileName.trim()
    const coefficient = Number(this.profileCoefficient)
    if (!displayName || !Number.isFinite(coefficient) || coefficient < 0.01 || coefficient > 100) {
      this.error.set('Indiquez un nom et un coefficient entre 0,01 et 100.')
      return
    }
    this.busy.set(true)
    this.error.set('')
    this.notice.set('')
    try {
      const id = this.editingProfileId()
      const request = { displayName, portionCoefficient: coefficient }
      const profile = id
        ? await firstValueFrom(this.api.put<MemberProfileDto>(`/households/profiles/${id}`, request))
        : await firstValueFrom(this.api.post<MemberProfileDto>('/households/profiles', request))
      this.profiles.update(items => id ? items.map(item => item.id === id ? profile : item) : [...items, profile])
      this.cancelProfileEdit()
      this.notice.set(id ? 'Personne mise à jour.' : 'Personne ajoutée.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async deleteProfile(profile: MemberProfileDto): Promise<void> {
    if (this.busy() || !window.confirm(`Supprimer le profil « ${profile.displayName} » ?`)) return
    this.busy.set(true)
    this.error.set('')
    try {
      await firstValueFrom(this.api.delete(`/households/profiles/${profile.id}`))
      this.profiles.update(items => items.filter(item => item.id !== profile.id))
      if (this.editingProfileId() === profile.id) this.cancelProfileEdit()
      this.notice.set('Personne supprimée.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }
}
