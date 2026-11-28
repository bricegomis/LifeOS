import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { LineChartComponent } from '@/app/shared/ui/line-chart.component'
import { decimal } from './sport-format'
import { monthlyVo2, vo2Summary } from './vo2max-rules'
import { SportsApiService } from './sports-api.service'
import type { Vo2MaxDto } from './sports.models'
import { SkeletonComponent } from '@/app/shared/ui/skeleton.component'

/** VO2max estimated by the watch (ADR 0019): the last value of each month, per sport. */
@Component({
  selector: 'app-sport-vo2max-page',
  standalone: true,
  imports: [SkeletonComponent, LineChartComponent],
  template: `
    <div class="lo-page is-narrow">
      <header class="lo-page-head"><h1 class="lo-bar-title">VO2max</h1></header>
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      <div class="lo-chips" role="group" aria-label="Sport">
        @for (option of sports; track option.code) {
          <button type="button" class="lo-filter-chip" [class.is-on]="sport() === option.code" [attr.aria-pressed]="sport() === option.code"
            (click)="sport.set(option.code)">{{ option.label }}</button>
        }
      </div>
      @if (loading()) { <app-skeleton variant="page" /> } @else {
        @if (summary(); as s) {
          <div class="lo-stats">
            <div class="lo-stat"><div class="lo-stat-label">Dernière valeur</div><div class="lo-stat-value">{{ decimal(s.last) }}</div><div class="lo-help">{{ s.lastDay }}</div></div>
            @if (s.change !== null) {
              <div class="lo-stat"><div class="lo-stat-label">Sur 12 mois</div><div class="lo-stat-value">{{ s.change > 0 ? '+' : '' }}{{ decimal(s.change) }}</div></div>
            }
            <div class="lo-stat"><div class="lo-stat-label">Meilleure</div><div class="lo-stat-value">{{ decimal(s.best) }}</div></div>
          </div>
          <app-line-chart [name]="'VO2max ' + sportLabel() + ', valeur du mois'" unit="ml/kg/min" [points]="points()" [format]="oneDecimal"
            [label]="'VO2max ' + sportLabel() + ' par mois sur deux ans'"
            note="Garmin calcule une valeur après une séance en extérieur avec GPS et fréquence cardiaque. Une valeur par mois : la dernière du mois." />
        } @else {
          <p class="lo-empty">{{ sport() === 'cycling'
            ? 'Pas de VO2max vélo : la montre la calcule seulement avec un capteur de puissance.'
            : 'Pas encore de VO2max. Elle arrive avec la synchro Garmin.' }}</p>
        }
      }
    </div>
  `,
})
export class SportVo2MaxPageComponent implements OnInit {
  private readonly api = inject(SportsApiService)
  readonly sports: { code: 'running' | 'cycling', label: string }[] = [{ code: 'running', label: 'Course' }, { code: 'cycling', label: 'Vélo' }]
  readonly sport = signal<'running' | 'cycling'>('running')
  readonly measures = signal<Vo2MaxDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  private today = new Date().toISOString().slice(0, 10)
  readonly sportLabel = computed(() => this.sport() === 'running' ? 'course' : 'vélo')
  readonly points = computed(() => monthlyVo2(this.measures(), this.sport(), this.today, 24))
  readonly summary = computed(() => vo2Summary(this.measures(), this.sport(), this.today))
  readonly decimal = decimal
  readonly oneDecimal = (value: number) => decimal(value)

  async ngOnInit(): Promise<void> {
    try {
      const catalog = await firstValueFrom(this.api.catalog())
      this.today = catalog.today
      const from = `${Number(this.today.slice(0, 4)) - 2}${this.today.slice(4)}`
      this.measures.set(await firstValueFrom(this.api.vo2max(from, this.today)))
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.loading.set(false) }
  }
}
