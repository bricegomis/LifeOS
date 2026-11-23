import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatButtonModule } from '@angular/material/button'
import { MatFormFieldModule } from '@angular/material/form-field'
import { MatIconModule } from '@angular/material/icon'
import { MatInputModule } from '@angular/material/input'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom, type Observable } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { cleanCode, garminNotice, waitsForCode, whenText } from './garmin-rules'
import { SportsApiService } from './sports-api.service'
import type { GarminStatusDto, ImportTokenDto } from './sports.models'

/**
 * The Garmin connection (ADR 0019): its state reported by garmin-sync, the code Garmin sends when the
 * account has double authentication, a sync on demand and the import token of the service.
 */
@Component({
  selector: 'app-sport-garmin-page',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <div class="lo-page is-narrow">
      <header class="lo-page-head"><h1 class="lo-bar-title">Garmin</h1></header>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (status(); as s) {
        <section class="lo-panel" aria-labelledby="garmin-state">
          <h2 id="garmin-state">Connexion Garmin</h2>
          <p class="state" [class]="'state is-' + tone()" role="status"><mat-icon [svgIcon]="icon()" />
            <span><strong>{{ s.stateLabel }}</strong>@if (notice(); as n) { · {{ n.text }} }</span></p>
          @if (codeWanted()) {
            <form class="code" (ngSubmit)="sendCode()">
              <p class="lo-help">Garmin vient d’envoyer un code à 6 chiffres (email ou SMS). Il est transmis une seule fois au service de synchro puis
                effacé ; sans code dans les 15 minutes, la demande expire.</p>
              <div class="code-row">
                <mat-form-field appearance="outline">
                  <mat-label>Code Garmin</mat-label>
                  <input matInput name="code" inputmode="numeric" autocomplete="one-time-code" maxlength="9" [(ngModel)]="code" />
                </mat-form-field>
                <button mat-flat-button type="submit" [disabled]="busy()">Envoyer à Garmin</button>
              </div>
              @if (s.codePending) { <p class="lo-help">Code reçu par LifeOS, le service le récupère dans les 15 secondes.</p> }
            </form>
          }
          @if (s.message && (s.state === 'error' || s.state === 'credentials_refused')) { <p class="lo-muted lo-small">{{ s.message }}</p> }
          <div class="lo-actions">
            <button mat-flat-button type="button" [disabled]="busy() || s.syncRequested" (click)="act(api.requestGarminSync(), 'Demande envoyée : le service la prend dans les 5 minutes.')">
              <mat-icon svgIcon="sync" />{{ s.syncRequested ? 'Synchro demandée' : 'Synchroniser maintenant' }}</button>
            <button mat-stroked-button type="button" [disabled]="busy() || s.reconnectRequested" (click)="act(api.reconnectGarmin(), 'Connexion relancée. Si Garmin envoie un code, saisis-le ici.')">
              <mat-icon svgIcon="restart_alt" />Relancer la connexion Garmin</button>
          </div>
          <div class="lo-stats">
            <div class="lo-stat"><div class="lo-stat-label">Dernière synchro réussie</div><div class="lo-stat-value small">{{ when(s.lastSuccessAt) }}</div>
              @if (s.lastSuccessSummary) { <div class="lo-help">{{ s.lastSuccessSummary }}</div> }</div>
            <div class="lo-stat"><div class="lo-stat-label">Prochaine synchro</div><div class="lo-stat-value small">{{ s.nextSyncAt ? when(s.nextSyncAt) : '7 h, 13 h, 19 h et 23 h' }}</div></div>
            <div class="lo-stat"><div class="lo-stat-label">Dernière erreur</div><div class="lo-stat-value small">{{ s.lastErrorAt ? when(s.lastErrorAt) : 'aucune' }}</div>
              @if (s.lastError) { <div class="lo-help">{{ s.lastError }}</div> }</div>
          </div>
        </section>
      }

      <section class="lo-panel" aria-labelledby="garmin-token">
        <h2 id="garmin-token">Jeton d’import</h2>
        <p class="lo-help">Le service garmin-sync écrit dans LifeOS avec ce jeton, et seulement par l’import. Copie-le dans la variable
          LIFEOS_IMPORT_TOKEN de la pile LifeOS (Coolify), avec GARMIN_EMAIL et GARMIN_PASSWORD. Le mot de passe Garmin n’est jamais dans LifeOS.</p>
        @if (secret()) {
          <div class="lo-notice secret">
            <span>Nouveau jeton, affiché une seule fois :</span>
            <code>{{ secret() }}</code>
            <button mat-stroked-button type="button" (click)="copy()"><mat-icon svgIcon="content_copy" />Copier</button>
          </div>
        }
        @for (token of tokens(); track token.id) {
          <div class="token-row">
            <mat-icon svgIcon="key" />
            <span class="token-text"><strong>{{ token.name }} · …{{ token.hint }}</strong>
              <span class="lo-muted lo-small">Créé {{ when(token.createdAt) }} · {{ token.lastUsedAt ? 'utilisé ' + when(token.lastUsedAt) : 'jamais utilisé' }}</span></span>
            <button mat-button type="button" class="danger" [disabled]="busy()" (click)="revoke(token)"><mat-icon svgIcon="block" />{{ confirming() === token.id ? 'Confirmer' : 'Révoquer' }}</button>
          </div>
        } @empty { <p class="lo-muted">Aucun jeton : la synchro Garmin ne peut pas écrire.</p> }
        <div><button mat-stroked-button type="button" [disabled]="busy()" (click)="createToken()"><mat-icon svgIcon="add" />Nouveau jeton</button></div>
      </section>
    </div>
  `,
  styles: [`
    .state { display: flex; align-items: flex-start; gap: 10px; margin: 0; padding: 12px 16px; border-radius: 12px; }
    .state mat-icon { flex: 0 0 auto; }
    .state.is-ok { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .state.is-warn { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .state.is-off { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .code { display: flex; flex-direction: column; gap: 8px; }
    .code-row { display: flex; flex-wrap: wrap; align-items: flex-start; gap: 12px; }
    .code-row mat-form-field { flex: 1 1 180px; }
    .code-row button { margin-top: 8px; }
    .lo-stat-value.small { font: var(--mat-sys-body-large); font-weight: 600; }
    .secret { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .secret code { overflow-wrap: anywhere; font-size: 13px; }
    .token-row { display: flex; align-items: center; gap: 12px; }
    .token-text { display: flex; min-width: 0; flex: 1 1 auto; flex-direction: column; }
    .danger { color: var(--mat-sys-error); }
  `],
})
export class SportGarminPageComponent implements OnInit, OnDestroy {
  readonly api = inject(SportsApiService)
  private readonly snack = inject(MatSnackBar)
  readonly status = signal<GarminStatusDto | null>(null)
  readonly tokens = signal<ImportTokenDto[]>([])
  readonly secret = signal('')
  readonly busy = signal(false)
  readonly error = signal('')
  readonly confirming = signal('')
  code = ''
  private timer: ReturnType<typeof setInterval> | undefined
  readonly notice = computed(() => garminNotice(this.status()))
  readonly codeWanted = computed(() => waitsForCode(this.status()))
  readonly tone = computed(() => this.notice()?.tone ?? 'ok')
  readonly icon = computed(() => this.notice()?.icon ?? (this.status()?.state === 'never' ? 'watch' : 'check_circle'))

  when(instant: string | null): string { return whenText(instant) }

  async ngOnInit(): Promise<void> {
    await this.refresh()
    try { this.tokens.set(await firstValueFrom(this.api.importTokens())) } catch (error) { this.error.set(apiErrorMessage(error)) }
    // The service reports while the page is open: follow a code request or a sync without reloading.
    this.timer = setInterval(() => void this.refresh(), 15000)
  }

  ngOnDestroy(): void { clearInterval(this.timer) }

  async sendCode(): Promise<void> {
    const code = cleanCode(this.code)
    if (!code) { this.error.set('Le code Garmin fait 6 chiffres.'); return }
    await this.act(this.api.sendGarminCode(code), 'Code envoyé : Garmin le vérifie.')
    this.code = ''
  }

  async act(call: Observable<GarminStatusDto>, message: string): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set('')
    try { this.status.set(await firstValueFrom(call)); this.snack.open(message, undefined, { duration: 4000 }) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  async createToken(): Promise<void> {
    this.busy.set(true); this.error.set('')
    try {
      const created = await firstValueFrom(this.api.createImportToken())
      this.secret.set(created.token)
      this.tokens.update(tokens => [...tokens, created.info])
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  async revoke(token: ImportTokenDto): Promise<void> {
    if (this.confirming() !== token.id) { this.confirming.set(token.id); return }
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.revokeImportToken(token.id))
      this.tokens.update(tokens => tokens.filter(item => item.id !== token.id))
      this.snack.open('Jeton révoqué : la synchro s’arrête jusqu’au prochain jeton.', undefined, { duration: 4000 })
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false); this.confirming.set('') }
  }

  async copy(): Promise<void> {
    try { await navigator.clipboard.writeText(this.secret()); this.snack.open('Jeton copié.', undefined, { duration: 3000 }) }
    catch { this.snack.open('Copie impossible : sélectionne le jeton à la main.', undefined, { duration: 4000 }) }
  }

  private async refresh(): Promise<void> {
    try { this.status.set(await firstValueFrom(this.api.garmin())) } catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
