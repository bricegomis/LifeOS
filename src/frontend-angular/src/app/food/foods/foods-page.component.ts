import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { FoodApiService } from '../food-api.service'
import type { Conservation, FoodDto, FoodFamily, FoodRole } from '../food.models'
import {
  conservationOptions, currentMonth, familyLabel, familyOptions, filterFoods, type FoodListFilter, householdTags,
  MONTH_NAMES, roleOptions, type SeasonKind, seasonStatus,
} from '../shared/classification'
import { FoodImageComponent } from '../shared/food-image.component'
import { fodmapLabel, formatNumber } from '../shared/format'

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
  readonly todo = signal(false)
  readonly family = signal<FoodFamily | ''>('')
  readonly role = signal<FoodRole | ''>('')
  readonly conservation = signal<Conservation | ''>('')
  readonly season = signal<SeasonKind | ''>('')
  readonly tag = signal('')
  readonly month = currentMonth()
  readonly monthName = MONTH_NAMES[this.month - 1]
  private readonly filter = computed<FoodListFilter>(() => ({
    search: this.search(), todo: this.todo(), family: this.family(), role: this.role(), conservation: this.conservation(),
    season: this.season(), tag: this.tag(), month: this.month,
  }))
  readonly visible = computed(() => filterFoods(this.foods(), this.filter()))
  readonly activeCount = computed(() => this.foods().filter(food => !food.isArchived).length)
  readonly todoCount = computed(() => this.foods().filter(food => !food.isArchived && food.missingFields.length > 0).length)
  readonly tags = computed(() => householdTags(this.foods().filter(food => !food.isArchived)))
  readonly creating = signal(false)
  readonly busy = signal(false)
  readonly createError = signal('')
  newName = ''
  newFamily: FoodFamily | '' = ''
  readonly fodmapLabel = fodmapLabel
  readonly familyLabel = familyLabel
  readonly familyOptions = familyOptions
  readonly roleOptions = roleOptions
  readonly conservationOptions = conservationOptions

  season$(food: FoodDto) { return seasonStatus(food.seasonMonths, this.month) }

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
    this.newFamily = ''
    this.createError.set('')
    this.creating.set(true)
  }

  /** A food starts with its name and family; the sheet then shows what is left to fill in. */
  async create(): Promise<void> {
    const name = this.newName.trim()
    const family = this.newFamily
    if (!name) { this.createError.set('Indiquez le nom de l’aliment.'); return }
    if (!family) { this.createError.set('Choisissez la famille de l’aliment.'); return }
    this.busy.set(true)
    try {
      const food = await firstValueFrom(this.api.createFood({
        name, referenceUnit: '', nutrition: null, fodmap: null, nova: null, digestiveNotes: null,
        family, roles: [], conservation: null, seasonMonths: [], tags: [],
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
