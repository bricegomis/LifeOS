import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { FoodApiService } from '../food-api.service'
import type { FoodDto } from '../food.models'
import { FoodImageComponent } from '../shared/food-image.component'
import { fodmapLabel, formatNumber } from '../shared/format'
import { type FoodFilter, visibleFoods } from './food-form'

@Component({
  selector: 'app-foods-page',
  standalone: true,
  imports: [FormsModule, RouterLink, LifeosDialogComponent, FoodImageComponent],
  templateUrl: './foods-page.component.html',
  styleUrl: './foods.css',
})
export class FoodsPageComponent implements OnInit {
  private readonly api = inject(FoodApiService)
  private readonly router = inject(Router)
  readonly foods = signal<FoodDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  readonly search = signal('')
  readonly filter = signal<FoodFilter>('all')
  readonly visible = computed(() => visibleFoods(this.foods(), this.search(), this.filter()))
  readonly activeCount = computed(() => visibleFoods(this.foods(), '', 'all').length)
  readonly todoCount = computed(() => visibleFoods(this.foods(), '', 'todo').length)
  readonly creating = signal(false)
  readonly busy = signal(false)
  readonly createError = signal('')
  newName = ''
  readonly fodmapLabel = fodmapLabel

  async ngOnInit(): Promise<void> {
    try {
      this.foods.set(await firstValueFrom(this.api.foods()))
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  calories(food: FoodDto): string {
    const value = food.nutrition?.caloriesPerUnit
    return value === null || value === undefined ? '—' : `${formatNumber(value, 0)} kcal`
  }

  openCreate(): void {
    this.newName = ''
    this.createError.set('')
    this.creating.set(true)
  }

  /** A food starts with its name; the sheet then shows what is left to fill in. */
  async create(): Promise<void> {
    const name = this.newName.trim()
    if (!name) { this.createError.set('Indiquez le nom de l’aliment.'); return }
    this.busy.set(true)
    try {
      const food = await firstValueFrom(this.api.createFood({
        name, referenceUnit: '', nutrition: null, fodmap: null, nova: null, digestiveNotes: null,
      }))
      this.creating.set(false)
      await this.router.navigate(['/foods', food.id])
    } catch (error) {
      this.createError.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }
}
