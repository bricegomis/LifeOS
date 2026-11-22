import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatIconModule } from '@angular/material/icon'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HealthApiService } from './health-api.service'
import type { SupplementDto, SupplementListDto } from './health.models'
import { SupplementActionsService } from './supplement-actions.service'
import { SupplementCardComponent } from './supplement-card.component'
import { todayGroups } from './supplement-rules'

/** Home of the Health module (ADR 0018): today's supplements by moment, one tick each. */
@Component({
  selector: 'app-health-today-page',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink, SupplementCardComponent],
  template: `
    <div class="lo-page">
      <header class="lo-page-head">
        <div class="lo-page-title"><h1 class="lo-bar-title">Aujourd’hui</h1><p class="lo-intro">Ta santé, visible par toi seul.</p></div>
      </header>
      @if (error()) { <p class="lo-empty" role="alert">{{ error() }}</p> }
      @if (list(); as l) {
        <section class="lo-section" aria-labelledby="supps">
          <div class="lo-section-head"><h2 id="supps">Compléments du jour</h2>
            <span class="lo-muted">{{ takenCount() }} / {{ activeCount() }} pris</span>
            <a mat-button routerLink="/health/supplements"><mat-icon svgIcon="tune" />Gérer</a></div>
          @for (group of groups(); track group.moment) {
            <h3 class="moment">{{ group.label }}</h3>
            <div class="cards">
              @for (s of group.items; track s.id) {
                <app-supplement-card [supplement]="s" [tick]="true" [busy]="pending() === s.id" (taken)="toggle($event)" />
              }
            </div>
          } @empty {
            <p class="lo-muted">{{ l.supplements.length ? 'Aucun complément à prendre aujourd’hui (cures en pause).' : 'Aucun complément. Ajoute-les dans Compléments.' }}</p>
          }
        </section>
      }
    </div>
  `,
  styles: [`
    .moment { margin: 4px 0 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-title-small); }
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 8px; }
    @media (max-width: 400px) { .cards { grid-template-columns: 1fr; } }
  `],
})
export class HealthTodayPageComponent implements OnInit {
  private readonly api = inject(HealthApiService)
  private readonly actions = inject(SupplementActionsService)
  readonly list = signal<SupplementListDto | null>(null)
  readonly error = signal('')
  readonly pending = signal('')
  readonly groups = computed(() => todayGroups(this.list()?.supplements ?? []))
  readonly activeCount = computed(() => this.groups().reduce((n, g) => n + g.items.length, 0))
  readonly takenCount = computed(() => this.groups().reduce((n, g) => n + g.items.filter(s => s.takenToday).length, 0))

  async ngOnInit(): Promise<void> { await this.load() }

  async toggle(supplement: SupplementDto): Promise<void> {
    this.pending.set(supplement.id)
    const updated = await this.actions.toggle(supplement)
    this.pending.set('')
    if (updated) this.list.update(l => l && { ...l, supplements: l.supplements.map(s => s.id === updated.id ? updated : s) })
  }

  private async load(): Promise<void> {
    try { this.list.set(await firstValueFrom(this.api.supplements())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
