import { Component, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { firstValueFrom, forkJoin } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { HouseholdGroupDto, MemberProfileDto } from '@/app/core/api/api.models'
import { AuthService } from '@/app/core/auth/auth.service'
import { apiBaseUrl, buildInfo } from '@/environments/environment'
import { BuildVersionComponent } from '@/app/shared/build-version.component'

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [RouterLink, FormsModule, BuildVersionComponent],
  template: `
    <section class="page-stack settings-page">
      <header class="page-hero">
        <div><h1>Réglages</h1><p>Compte, personnes du foyer et groupes d’avis.</p></div>
      </header>
      @if (error()) { <p class="settings-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="settings-notice" role="status">{{ notice() }}</p> }
      <section class="surface-card">
        <h2>Votre compte</h2>
        <p>{{ auth.user()?.email ?? 'Compte du foyer' }}</p>
        <p>Les comptes connectés et les profils de personnes sont distincts. Un profil peut représenter un enfant sans compte.</p>
        <p>Les données du foyer sont sauvegardées par LifeOS API. Supabase assure uniquement la connexion.</p>
        <dl>
          <dt>Version interface</dt><dd><app-build-version component="UI" [info]="buildInfo" /></dd>
          <dt>API</dt><dd>{{ apiUrl || 'Non configurée' }}</dd>
        </dl>
      </section>

      <section class="surface-card settings-social">
        <div class="settings-heading"><div><h2>Personnes du foyer</h2><p>Les coefficients suggèrent une portion initiale, sans imposer la même quantité à chaque recette.</p></div></div>
        <form class="settings-form" (ngSubmit)="saveProfile()">
          <label class="dialog-field"><span>Nom affiché</span><input class="text-input" name="profileName" required maxlength="200" [(ngModel)]="profileName" /></label>
          <label class="dialog-field"><span>Coefficient de portion indicatif</span><input class="text-input" name="profileCoefficient" type="number" min="0.01" max="100" step="any" required [(ngModel)]="profileCoefficient" /></label>
          <button class="lifeos-button" type="submit" [disabled]="busy()">{{ editingProfileId() ? 'Enregistrer' : 'Ajouter une personne' }}</button>
          @if (editingProfileId()) { <button class="lifeos-button lifeos-button-secondary" type="button" (click)="cancelProfileEdit()">Annuler</button> }
        </form>
        @if (!profiles().length) { <p>Aucune personne ajoutée. Vous pouvez saisir les portions totales sans répartition.</p> }
        @for (profile of profiles(); track profile.id) {
          <div class="settings-row">
            <div><strong>{{ profile.displayName }}</strong><small>Coefficient indicatif {{ profile.portionCoefficient }}</small></div>
            <div class="settings-row-actions">
              <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="editProfile(profile)">Modifier</button>
              <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="deleteProfile(profile)">Supprimer</button>
            </div>
          </div>
        }
      </section>

      <section class="surface-card settings-social">
        <div class="settings-heading"><div><h2>Groupes pour les avis</h2><p>Par exemple, « Enfants ». Le groupe permet un avis commun et n’est pas un profil de consommation collectif.</p></div></div>
        <form class="settings-form" (ngSubmit)="saveGroup()">
          <label class="dialog-field"><span>Nom du groupe</span><input class="text-input" name="groupName" required maxlength="100" [(ngModel)]="groupName" /></label>
          <button class="lifeos-button" type="submit" [disabled]="busy()">{{ editingGroupId() ? 'Enregistrer' : 'Ajouter un groupe' }}</button>
          @if (editingGroupId()) { <button class="lifeos-button lifeos-button-secondary" type="button" (click)="cancelGroupEdit()">Annuler</button> }
        </form>
        @if (!groups().length) { <p>Aucun groupe ajouté.</p> }
        @for (group of groups(); track group.id) {
          <div class="settings-row">
            <strong>{{ group.name }}</strong>
            <div class="settings-row-actions">
              <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="editGroup(group)">Modifier</button>
              <button type="button" class="lifeos-button lifeos-button-secondary" [disabled]="busy()" (click)="deleteGroup(group)">Supprimer</button>
            </div>
          </div>
        }
      </section>

      <section class="surface-card">
        <h2>Planifier sans configuration</h2>
        <p>Les portions se règlent séparément pour chaque ligne d’un repas. Les règles et la génération ne pilotent pas le semainier manuel.</p>
        <a class="lifeos-button" routerLink="/planning">Ouvrir le semainier</a>
      </section>
    </section>
  `,
  styles: [`
    p { max-width: 70ch; line-height: 1.6; }
    dd { margin: 8px 0 20px; overflow-wrap: anywhere; }
    .settings-social { display: grid; gap: 14px; }
    .settings-heading h2, .settings-heading p { margin: 0 0 6px; }
    .settings-form { display: flex; flex-wrap: wrap; align-items: end; gap: 12px; }
    .settings-form .dialog-field { flex: 1 1 220px; }
    .settings-row { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 10px; padding: 12px; border-radius: 12px; background: var(--lifeos-surface-soft); }
    .settings-row small { display: block; margin-top: 4px; color: var(--lifeos-text-soft); }
    .settings-row-actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .settings-error { color: #8f2017; font-weight: 700; }
    .settings-notice { color: var(--lifeos-accent-strong); font-weight: 700; }
  `],
})
export class SettingsPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)
  readonly auth = inject(AuthService)
  readonly apiUrl = apiBaseUrl
  readonly buildInfo = buildInfo
  readonly profiles = signal<MemberProfileDto[]>([])
  readonly groups = signal<HouseholdGroupDto[]>([])
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly editingProfileId = signal('')
  readonly editingGroupId = signal('')
  profileName = ''
  profileCoefficient = 1
  groupName = ''

  ngOnInit(): void { void this.load() }

  async load(): Promise<void> {
    this.error.set('')
    try {
      const result = await firstValueFrom(forkJoin({
        household: this.api.get<{ memberProfiles: MemberProfileDto[] }>('/households/members'),
        groups: this.api.get<HouseholdGroupDto[]>('/household-groups'),
      }))
      this.profiles.set(result.household.memberProfiles)
      this.groups.set(result.groups)
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

  editGroup(group: HouseholdGroupDto): void {
    this.editingGroupId.set(group.id)
    this.groupName = group.name
    this.notice.set('')
  }

  cancelGroupEdit(): void {
    this.editingGroupId.set('')
    this.groupName = ''
  }

  async saveGroup(): Promise<void> {
    const name = this.groupName.trim()
    if (!name || name.length > 100) {
      this.error.set('Indiquez un nom de groupe de 100 caractères maximum.')
      return
    }
    this.busy.set(true)
    this.error.set('')
    this.notice.set('')
    try {
      const id = this.editingGroupId()
      const group = id
        ? await firstValueFrom(this.api.put<HouseholdGroupDto>(`/household-groups/${id}`, { name }))
        : await firstValueFrom(this.api.post<HouseholdGroupDto>('/household-groups', { name }))
      this.groups.update(items => id ? items.map(item => item.id === id ? group : item) : [...items, group])
      this.cancelGroupEdit()
      this.notice.set(id ? 'Groupe mis à jour.' : 'Groupe ajouté.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async deleteGroup(group: HouseholdGroupDto): Promise<void> {
    if (this.busy() || !window.confirm(`Supprimer le groupe « ${group.name} » ?`)) return
    this.busy.set(true)
    this.error.set('')
    try {
      await firstValueFrom(this.api.delete(`/household-groups/${group.id}`))
      this.groups.update(items => items.filter(item => item.id !== group.id))
      if (this.editingGroupId() === group.id) this.cancelGroupEdit()
      this.notice.set('Groupe supprimé.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }
}
