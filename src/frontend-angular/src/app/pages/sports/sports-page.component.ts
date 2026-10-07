import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { SportTemplateDto, SportTemplateRequest } from '@/app/core/api/api.models'

const empty = (): SportTemplateRequest => ({
  name: '', sport: 'run', durationMinutes: 30, distanceKm: null, intensity: 'moderate', calories: 0,
})

@Component({
  selector: 'app-sports-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page-stack">
      <header class="page-hero"><div><h1>Séances sportives</h1><p>Vos modèles, réutilisables sans modifier les séances déjà planifiées.</p></div></header>
      @if (error()) { <p class="sport-error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      <div class="sport-workspace">
        <section class="surface-card">
          <h2>Votre bibliothèque</h2>
          <label>Rechercher <input type="search" class="text-input" [(ngModel)]="search" /></label>
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
        <form class="surface-card sport-form" (ngSubmit)="save()">
          <h2>{{ editingId ? 'Modifier le modèle' : 'Nouveau modèle' }}</h2>
          <label>Nom <input class="text-input" name="name" required maxlength="200" [(ngModel)]="draft.name" /></label>
          <label>Sport <input class="text-input" name="sport" required maxlength="50" list="sport-types" [(ngModel)]="draft.sport" /></label>
          <datalist id="sport-types"><option value="run">Course</option><option value="bike">Vélo</option><option value="strength">Musculation</option><option value="walk">Marche</option></datalist>
          <label>Durée habituelle (min) <input class="text-input" name="duration" type="number" min="1" max="1440" step="1" required [(ngModel)]="draft.durationMinutes" /></label>
          <label>Distance (km, facultative) <input class="text-input" name="distance" type="number" min="0" step="any" [(ngModel)]="draft.distanceKm" /></label>
          <label>Intensité <select class="text-input" name="intensity" [(ngModel)]="draft.intensity"><option value="low">Faible</option><option value="moderate">Modérée</option><option value="high">Élevée</option></select></label>
          <label>Calories totales estimées manuellement <input class="text-input" name="calories" type="number" min="0" step="any" required [(ngModel)]="draft.calories" /></label>
          <p>Les calories sont un total saisi. Modifier durée ou distance ne les recalcule pas.</p>
          <div class="sport-actions"><button class="lifeos-button" type="submit" [disabled]="busy()">{{ busy() ? 'Enregistrement…' : 'Enregistrer' }}</button>
            <button class="lifeos-button lifeos-button-secondary" type="button" [disabled]="busy()" (click)="reset()">Nouveau / annuler</button></div>
        </form>
      </div>
    </section>
  `,
  styles: [`
    .sport-workspace { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; align-items: start; }
    .sport-form, .sport-form label { display: grid; gap: 10px; }
    .sport-form { gap: 16px; } h2 { margin-top: 0; } p { line-height: 1.5; }
    .sport-row { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; padding: 18px 0; border-bottom: 1px solid var(--lifeos-line); }
    .sport-row div { flex: 1; min-width: 150px; } .sport-row p { margin: 6px 0; }
    .sport-actions { display: flex; flex-wrap: wrap; gap: 12px; }
    .sport-error { color: #8b2c20; } button { min-height: 44px; } input, select { width: 100%; }
    @media (max-width: 850px) { .sport-workspace { grid-template-columns: 1fr; } }
  `],
})
export class SportsPageComponent {
  private readonly api = inject(LifeosApiService)
  readonly items = signal<SportTemplateDto[]>([])
  readonly error = signal('')
  readonly notice = signal('')
  readonly loading = signal(false)
  readonly busy = signal(false)
  search = ''
  editingId = ''
  draft = empty()
  constructor() { void this.load() }
  visible(): SportTemplateDto[] {
    return this.items().filter(i => !i.isArchived && `${i.name} ${i.sport}`.toLocaleLowerCase('fr').includes(this.search.toLocaleLowerCase('fr')))
  }
  reset(): void { this.editingId = ''; this.draft = empty() }
  edit(item: SportTemplateDto): void { this.editingId = item.id; this.draft = { ...item } }
  async load(): Promise<void> {
    this.loading.set(true)
    try { this.items.set(await firstValueFrom(this.api.get<SportTemplateDto[]>('/sport-templates'))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }
  async save(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set(''); this.notice.set('')
    try {
      const item = await firstValueFrom(this.editingId
        ? this.api.put<SportTemplateDto>(`/sport-templates/${this.editingId}`, this.draft)
        : this.api.post<SportTemplateDto>('/sport-templates', this.draft))
      this.items.update(items => [...items.filter(i => i.id !== item.id), item])
      this.reset(); this.notice.set('Modèle enregistré. Les séances existantes ne changent pas.')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }
  async archive(item: SportTemplateDto): Promise<void> {
    if (!window.confirm(`Archiver « ${item.name} » ? Les séances existantes sont conservées.`)) return
    this.busy.set(true); this.error.set('')
    try {
      await firstValueFrom(this.api.delete(`/sport-templates/${item.id}`))
      this.items.update(items => items.map(i => i.id === item.id ? { ...i, isArchived: true } : i))
      if (this.editingId === item.id) this.reset()
      this.notice.set('Modèle archivé.')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
