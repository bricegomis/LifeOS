import { Component, computed, HostListener, inject, input, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { Router, RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import type { MemberProfileDto } from '@/app/core/api/api.models'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { HouseholdReviewsComponent } from '@/app/shared/reviews/household-reviews.component'
import { ReviewDraftsService } from '@/app/shared/reviews/review-drafts.service'
import { FoodApiService } from '../food-api.service'
import type { FoodDto, FoodRole, ProductDto, StoreDto } from '../food.models'
import {
  addTag, CONSERVATION_LABELS, conservationOptions, currentMonth, familyLabel, familyOptions, householdTags, MAX_TAG_LENGTH,
  MONTH_NAMES, MONTH_SHORT, ROLE_LABELS, roleOptions, seasonRangeLabel, seasonStatus,
} from '../shared/classification'
import { FoodImageComponent } from '../shared/food-image.component'
import { FODMAP_LABELS, NOVA_LABELS } from '../shared/format'
import { buildFoodRequest, emptyFoodForm, foodFormFrom, foodPriceSummary, type FoodFormValues, NUTRITION_FIELDS } from './food-form'

@Component({
  selector: 'app-food-page',
  standalone: true,
  imports: [FormsModule, RouterLink, FoodImageComponent, HouseholdReviewsComponent],
  templateUrl: './food-page.component.html',
  styleUrl: './foods.css',
})
export class FoodPageComponent implements OnInit {
  private readonly api = inject(FoodApiService)
  private readonly router = inject(Router)
  private readonly reviewDrafts = inject(ReviewDraftsService)
  /** Route parameter. */
  readonly id = input.required<string>()
  /** Where the sheet was opened from: `recipe` (with `recipe`) or `products`. */
  readonly from = input<string>()
  readonly recipe = input<string>()

  readonly food = signal<FoodDto | null>(null)
  readonly products = signal<ProductDto[]>([])
  readonly stores = signal<StoreDto[]>([])
  readonly profiles = signal<MemberProfileDto[]>([])
  readonly loading = signal(true)
  readonly error = signal('')
  readonly saving = signal(false)
  readonly saveError = signal('')
  readonly saved = signal(false)
  readonly imageBusy = signal(false)
  form: FoodFormValues = emptyFoodForm()
  private initial = formSnapshot(this.form)
  /** Tags of the other foods of the household, offered as suggestions. */
  readonly knownTags = signal<string[]>([])
  tagDraft = ''

  readonly month = currentMonth()
  readonly monthName = MONTH_NAMES[this.month - 1]
  readonly months = MONTH_SHORT.map((label, index) => ({ value: index + 1, label, name: MONTH_NAMES[index] }))
  readonly familyOptions = familyOptions
  readonly roleOptions = roleOptions
  readonly conservationOptions = conservationOptions
  readonly maxTagLength = MAX_TAG_LENGTH

  readonly fields = NUTRITION_FIELDS
  readonly fodmapOptions = Object.entries(FODMAP_LABELS)
  readonly novaOptions = Object.entries(NOVA_LABELS)
  readonly prices = computed(() => foodPriceSummary(this.products(), this.stores()))
  readonly back = computed(() => {
    if (this.from() === 'recipe' && this.recipe()) return { link: ['/recipes', this.recipe()!], label: '← Recette' }
    if (this.from() === 'products') return { link: ['/products'], label: '← Produits et prix' }
    return { link: ['/foods'], label: '← Aliments' }
  })

  async ngOnInit(): Promise<void> {
    try {
      const [food, products, stores, profiles, foods] = await Promise.all([
        firstValueFrom(this.api.food(this.id())),
        firstValueFrom(this.api.products(this.id())),
        firstValueFrom(this.api.stores()),
        firstValueFrom(this.api.profiles()),
        firstValueFrom(this.api.foods()),
      ])
      this.knownTags.set(householdTags(foods.filter(other => !other.isArchived)))
      this.show(food)
      this.products.set(products)
      this.stores.set(stores)
      this.profiles.set(profiles)
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.loading.set(false)
    }
  }

  dirty(): boolean { return hasChanges(this.initial, this.form) }

  canLeave(): boolean {
    if (this.saving() || this.imageBusy()) return false
    return (!this.dirty() || window.confirm('Abandonner les modifications de l’aliment ?')) && this.reviewDrafts.canLeave()
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty() || this.saving()) { event.preventDefault(); event.returnValue = '' }
  }

  isMissing(text: string): boolean { return !text.trim() }

  hasRole(role: FoodRole): boolean { return this.form.roles.includes(role) }

  toggleRole(role: FoodRole): void {
    const roles = this.hasRole(role) ? this.form.roles.filter(r => r !== role) : [...this.form.roles, role]
    this.form.roles = roleOptions.map(([code]) => code).filter(code => roles.includes(code))
  }

  hasMonth(month: number): boolean { return this.form.seasonMonths.includes(month) }

  toggleMonth(month: number): void {
    this.form.seasonMonths = this.hasMonth(month)
      ? this.form.seasonMonths.filter(m => m !== month)
      : [...this.form.seasonMonths, month].sort((a, b) => a - b)
  }

  /** Season of the form being edited, for the current month. */
  seasonNow(): { kind: string; text: string } {
    const status = seasonStatus(this.form.seasonMonths, this.month)
    return { kind: status.kind, text: status.kind === 'all' ? 'Pas saisonnier : disponible toute l’année' : `${status.label} en ${this.monthName}` }
  }

  addTag(typed = this.tagDraft): void {
    this.form.tags = addTag(this.form.tags, typed, this.knownTags())
    this.tagDraft = ''
  }

  removeTag(tag: string): void { this.form.tags = this.form.tags.filter(t => t !== tag) }

  suggestions(): string[] {
    const used = new Set(this.form.tags.map(tag => tag.toLocaleLowerCase('fr')))
    return this.knownTags().filter(tag => !used.has(tag.toLocaleLowerCase('fr')))
  }

  /** Read-only summary for the phone, from the saved food. */
  classRows(food: FoodDto): { label: string; value: string }[] {
    return [
      { label: 'Famille', value: familyLabel(food.family) },
      { label: 'Rôles', value: food.roles.map(role => ROLE_LABELS[role]).join(', ') || '—' },
      { label: 'Conservation', value: food.conservation ? CONSERVATION_LABELS[food.conservation] : 'Non précisée' },
      { label: 'Saison', value: seasonRangeLabel(food.seasonMonths) },
      { label: 'Étiquettes', value: food.tags.join(', ') || '—' },
    ]
  }

  async save(): Promise<void> {
    const food = this.food()
    const request = buildFoodRequest(this.form)
    if (!food) return
    if (!request.ok) { this.saveError.set(request.error); return }
    this.saving.set(true)
    this.saveError.set('')
    this.saved.set(false)
    try {
      this.show(await firstValueFrom(this.api.updateFood(food.id, request.value)))
      this.saved.set(true)
    } catch (error) {
      this.saveError.set(apiErrorMessage(error))
    } finally {
      this.saving.set(false)
    }
  }

  async changeImage(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    const food = this.food()
    input.value = ''
    if (!file || !food) return
    await this.imageAction(async () => {
      const updated = await firstValueFrom(this.api.uploadImage<FoodDto>('foods', food.id, file))
      this.food.set(updated)
    })
  }

  async removeImage(): Promise<void> {
    const food = this.food()
    if (!food || !window.confirm('Retirer la photo de cet aliment ?')) return
    await this.imageAction(async () => {
      await firstValueFrom(this.api.removeImage('foods', food.id))
      this.food.set({ ...food, hasImage: false, updatedAt: new Date().toISOString() })
    })
  }

  async archive(): Promise<void> {
    const food = this.food()
    if (!food || !window.confirm(`Archiver « ${food.name} » ? Il disparaît des listes ; les recettes qui l’utilisent le gardent.`)) return
    try {
      await firstValueFrom(this.api.archiveFood(food.id))
      this.initial = formSnapshot(this.form)
      await this.router.navigate(['/foods'])
    } catch (error) {
      this.saveError.set(apiErrorMessage(error))
    }
  }

  private async imageAction(action: () => Promise<void>): Promise<void> {
    this.imageBusy.set(true)
    this.saveError.set('')
    try { await action() } catch (error) { this.saveError.set(apiErrorMessage(error)) } finally { this.imageBusy.set(false) }
  }

  private show(food: FoodDto): void {
    this.food.set(food)
    this.form = foodFormFrom(food)
    this.initial = formSnapshot(this.form)
  }
}
