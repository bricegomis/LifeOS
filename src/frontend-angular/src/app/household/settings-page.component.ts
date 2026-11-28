import { Component, inject, OnInit, signal } from '@angular/core'
import { Router } from '@angular/router'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom, timeout } from 'rxjs'
import { parseApiBuildInfo, type BuildInfo } from '@/app/core/build-info'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { AssistantConnectionDto, MemberProfileDto } from '@/app/core/api/api.models'
import { AuthService } from '@/app/core/auth/auth.service'
import { apiBaseUrl, buildInfo } from '@/environments/environment'
import { BuildVersionComponent } from '@/app/shared/build-version.component'

import { NativeSyncService } from '@/app/core/native/native-sync.service'
import { DeepLinkService } from '@/app/core/native/deep-link.service'
import { nativeBridgeStatus } from '@/app/core/native/native-bridge'
import { ConfirmService } from '@/app/shared/ui/confirm'

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [BuildVersionComponent, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <div class="lo-page is-narrow">
      <header class="lo-page-head"><h1 class="lo-bar-title">Paramètres</h1></header>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }

      <section class="lo-panel" aria-labelledby="profiles-title">
        <div class="lo-page-title">
          <h2 id="profiles-title">Profils du foyer</h2>
          <p class="lo-muted">Chaque personne donne ses avis. Le coefficient suggère une portion, sans l’imposer. Un profil peut représenter un enfant sans compte.</p>
        </div>
        @if (profiles().length) {
          <div class="lo-list">
            @for (profile of profiles(); track profile.id) {
              <div class="lo-row">
                <div class="lo-row-main">
                  <span class="lo-lead-icon" aria-hidden="true"><mat-icon svgIcon="person" /></span>
                  <span class="lo-row-text"><span class="lo-row-title">{{ profile.displayName }}</span>
                    <span class="lo-row-sub">Coefficient de portion : {{ profile.portionCoefficient }}</span></span>
                </div>
                <span class="lo-row-action">
                  <button mat-icon-button type="button" [disabled]="busy()" (click)="editProfile(profile)"
                    [attr.aria-label]="'Modifier ' + profile.displayName" title="Modifier"><mat-icon svgIcon="edit" /></button>
                  <button mat-icon-button type="button" [disabled]="busy()" (click)="deleteProfile(profile)"
                    [attr.aria-label]="'Supprimer ' + profile.displayName" title="Supprimer"><mat-icon svgIcon="delete" /></button>
                </span>
              </div>
            }
          </div>
        } @else {
          <p class="lo-muted">Aucun profil. Ajoute les personnes du foyer pour saisir leurs avis.</p>
        }
        <form class="profile-form" (ngSubmit)="saveProfile()" [attr.aria-label]="editingProfileId() ? 'Modifier le profil' : 'Ajouter un profil'">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Nom affiché</mat-label>
            <input matInput name="profileName" required maxlength="200" [(ngModel)]="profileName" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="coefficient">
            <mat-label>Coefficient de portion</mat-label>
            <input matInput name="profileCoefficient" type="number" inputmode="decimal" min="0.01" max="100" step="any" required [(ngModel)]="profileCoefficient" />
          </mat-form-field>
          <div class="lo-actions">
            <button mat-flat-button type="submit" [disabled]="busy()">
              <mat-icon [svgIcon]="editingProfileId() ? 'check' : 'add'" />{{ editingProfileId() ? 'Enregistrer' : 'Ajouter un profil' }}</button>
            @if (editingProfileId()) { <button mat-button type="button" (click)="cancelProfileEdit()">Annuler</button> }
          </div>
        </form>
      </section>

      <section class="lo-panel" aria-labelledby="assistants-title">
        <div class="lo-page-title">
          <h2 id="assistants-title">Assistants connectés</h2>
          <p class="lo-muted">Claude peut remplir et lire LifeOS pendant une discussion, avec les droits de ton compte. Ajoute cette adresse comme connecteur personnalisé dans claude.ai, ou avec <code>claude mcp add</code> dans Claude Code.</p>
        </div>
        <div class="mcp-url">
          <code>{{ mcpUrl || 'Adresse de l’API non configurée' }}</code>
          @if (mcpUrl) {
            <button mat-stroked-button type="button" (click)="copyMcpUrl()">
              <mat-icon [svgIcon]="copied() ? 'check' : 'content_copy'" />{{ copied() ? 'Copiée' : 'Copier' }}</button>
          }
        </div>
        @if (connections().length) {
          <div class="lo-list">
            @for (connection of connections(); track connection.id) {
              <div class="lo-row">
                <div class="lo-row-main">
                  <span class="lo-row-text"><span class="lo-row-title">{{ connection.clientName }}</span>
                    @if (connection.grantedAt) { <span class="lo-row-sub">Autorisé le {{ formatDate(connection.grantedAt) }}</span> }</span>
                </div>
                <span class="lo-row-action">
                  <button mat-button type="button" class="danger" [disabled]="busy()" (click)="revokeConnection(connection)"><mat-icon svgIcon="link_off" />Retirer l’accès</button>
                </span>
              </div>
            }
          </div>
        } @else {
          <p class="lo-muted">Aucun assistant autorisé.</p>
        }
      </section>

      <section class="lo-panel" aria-labelledby="versions-title">
        <h2 id="versions-title">Versions</h2>
        <dl class="versions">
          <dt>Interface</dt><dd><app-build-version component="UI" [info]="buildInfo" /></dd>
          <dt>API</dt><dd><app-build-version component="API" [info]="apiBuildInfo()" [status]="apiBuildStatus()" /></dd>
          <dt>Adresse de l’API</dt><dd>{{ apiUrl || 'Non configurée' }}</dd>
        </dl>
      </section>

      <section class="lo-panel" aria-labelledby="account-title">
        <h2 id="account-title">Compte</h2>
        <p>{{ auth.user()?.email ?? 'Compte du foyer' }}</p>
        <p class="lo-muted lo-small">Connexion avec Google, gérée par LifeOS API, qui conserve aussi les données du foyer.</p>
        <div class="lo-phone-only">
          <button mat-stroked-button type="button" [disabled]="busy()" (click)="signOut()"><mat-icon svgIcon="logout" />Déconnexion</button>
        </div>
      </section>

      @if (bridgeStatus() !== 'web') {
        <section class="lo-panel" aria-labelledby="iphone-title">
          <h2 id="iphone-title">iPhone</h2>
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
                @case ('error') { <span class="lo-error-text">Erreur de synchronisation</span> }
                @default { En attente }
              }
            </dd>
            @if (nativeSync.lastSyncError()) { <dt>Dernier incident</dt><dd class="lo-error-text">{{ nativeSync.lastSyncError() }}</dd> }
            @if (nativeSync.lastSyncedAt()) { <dt>Dernière synchronisation</dt><dd>{{ nativeSync.lastSyncedAt() }}</dd> }
          </dl>
          <div>
            <button mat-stroked-button type="button" [disabled]="busy() || nativeSync.syncStatus() === 'syncing'" (click)="triggerNativeSync()">
              <mat-icon svgIcon="sync" />Synchroniser maintenant</button>
          </div>
        </section>
      }
    </div>
  `,
  styles: [`
    .lo-panel h2 { margin: 0; font: var(--mat-sys-title-medium); font-weight: 600; }
    .lo-row-action { display: flex; }
    .profile-form { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
    .profile-form mat-form-field { flex: 1 1 220px; }
    .profile-form .coefficient { flex: 0 1 200px; }
    .mcp-url { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
    .mcp-url code { padding: 6px 10px; border-radius: 8px; background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); overflow-wrap: anywhere; }
    .danger { color: var(--mat-sys-error); }
    .versions { display: grid; grid-template-columns: 160px minmax(0, 1fr); gap: 12px; margin: 0; }
    .versions dt { font-weight: 500; }
    .versions dd { margin: 0; color: var(--mat-sys-on-surface-variant); overflow-wrap: anywhere; }
    @media (max-width: 560px) { .versions { grid-template-columns: minmax(0, 1fr); gap: 4px; } }
  `],
})
export class SettingsPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)
  private readonly confirm = inject(ConfirmService)
  readonly auth = inject(AuthService)
  readonly nativeSync = inject(NativeSyncService)
  readonly deepLink = inject(DeepLinkService)
  readonly bridgeStatus = nativeBridgeStatus
  readonly apiUrl = apiBaseUrl
  readonly buildInfo = buildInfo
  readonly apiBuildInfo = signal<BuildInfo | null>(null)
  readonly apiBuildStatus = signal(apiBaseUrl ? 'chargement…' : 'non configurée')
  private readonly router = inject(Router)
  private readonly snack = inject(MatSnackBar)
  readonly profiles = signal<MemberProfileDto[]>([])
  readonly busy = signal(false)
  readonly error = signal('')
  readonly editingProfileId = signal('')
  readonly connections = signal<AssistantConnectionDto[]>([])
  readonly copied = signal(false)
  readonly mcpUrl = apiBaseUrl ? `${apiBaseUrl}/mcp` : ''
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
      this.connections.set(await firstValueFrom(this.api.get<AssistantConnectionDto[]>('/account/connections')))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    }
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
  }

  async copyMcpUrl(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.mcpUrl)
      this.copied.set(true)
      setTimeout(() => this.copied.set(false), 2000)
    } catch {
      this.error.set('Copie impossible : sélectionne l’adresse à la main.')
    }
  }

  async revokeConnection(connection: AssistantConnectionDto): Promise<void> {
    if (this.busy()
      || !await this.confirm.ask({ message: `Retirer l’accès de « ${connection.clientName} » à LifeOS ?`, confirmLabel: 'Retirer', danger: true })) return
    this.busy.set(true)
    this.error.set('')
    try {
      await firstValueFrom(this.api.delete(`/account/connections/${encodeURIComponent(connection.id)}`))
      this.connections.update(items => items.filter(item => item.id !== connection.id))
      this.snack.open('Accès retiré. L’assistant devra demander à nouveau ton autorisation.', undefined, { duration: 4000 })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  editProfile(profile: MemberProfileDto): void {
    this.editingProfileId.set(profile.id)
    this.profileName = profile.displayName
    this.profileCoefficient = profile.portionCoefficient
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
      this.error.set('Indique un nom et un coefficient entre 0,01 et 100.')
      return
    }
    this.busy.set(true)
    this.error.set('')
    try {
      const id = this.editingProfileId()
      const request = { displayName, portionCoefficient: coefficient }
      const profile = id
        ? await firstValueFrom(this.api.put<MemberProfileDto>(`/households/profiles/${id}`, request))
        : await firstValueFrom(this.api.post<MemberProfileDto>('/households/profiles', request))
      this.profiles.update(items => id ? items.map(item => item.id === id ? profile : item) : [...items, profile])
      this.cancelProfileEdit()
      this.snack.open(id ? 'Personne mise à jour.' : 'Personne ajoutée.', undefined, { duration: 4000 })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async deleteProfile(profile: MemberProfileDto): Promise<void> {
    if (this.busy()
      || !await this.confirm.ask({ message: `Supprimer le profil « ${profile.displayName} » ?`, confirmLabel: 'Supprimer', danger: true })) return
    this.busy.set(true)
    this.error.set('')
    try {
      await firstValueFrom(this.api.delete(`/households/profiles/${profile.id}`))
      this.profiles.update(items => items.filter(item => item.id !== profile.id))
      if (this.editingProfileId() === profile.id) this.cancelProfileEdit()
      this.snack.open('Personne supprimée.', undefined, { duration: 4000 })
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }
}
