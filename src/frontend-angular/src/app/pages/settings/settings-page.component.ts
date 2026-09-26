import { HttpErrorResponse } from '@angular/common/http'
import { Component, inject, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom } from 'rxjs'
import type {
  FrequencyRuleDto,
  FrequencyRuleRequest,
  LibraryCompositeDishDto,
  LibraryMealComponentDto,
  PlanningRuleDto,
  PlanningRuleRequest,
  UserConfigurationDto,
  UserConfigurationRequest,
} from '@/app/core/api/api.models'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'

type WeekMode = 'solo' | 'kids'
type WorkLocation = 'home' | 'office' | 'off'
type TargetKind = 'component' | 'dish'
type FrequencyKind = TargetKind | 'category'
type DayContext = { workLocation: WorkLocation; bikeCommute: boolean }
type WeekContextPayload = {
  alternatingWeekConfig: { referenceWeekStartDate: string; referenceWeekMode: WeekMode }
  weekModeOverrides: { weekStartDate: string; mode: WeekMode }[]
  days: Record<string, DayContext>
}
type NutritionDraft = { [K in keyof UserConfigurationRequest]: number | null }
type PlanningDraft = {
  weekday: string
  mealType: string
  kind: TargetKind
  targetId: string
}
type FrequencyDraft = {
  kind: FrequencyKind
  targetId: string
  label: string
  count: number | null
}

const weekdays = [
  { value: 'monday', label: 'Lundi' },
  { value: 'tuesday', label: 'Mardi' },
  { value: 'wednesday', label: 'Mercredi' },
  { value: 'thursday', label: 'Jeudi' },
  { value: 'friday', label: 'Vendredi' },
  { value: 'saturday', label: 'Samedi' },
  { value: 'sunday', label: 'Dimanche' },
]
const meals = [
  { value: 'breakfast', label: 'Petit-déjeuner' },
  { value: 'lunch', label: 'Déjeuner' },
  { value: 'dinner', label: 'Dîner' },
]

const emptyNutrition = (): NutritionDraft => ({
  dailyBaseEnergyKcal: null,
  targetNetDeficitKcal: null,
  targetProteinG: null,
  targetCarbsG: null,
  targetFatsG: null,
})
const emptyPlanning = (): PlanningDraft => ({
  weekday: '',
  mealType: '',
  kind: 'component',
  targetId: '',
})
const emptyFrequency = (): FrequencyDraft => ({
  kind: 'component',
  targetId: '',
  label: '',
  count: null,
})

function hasNutritionValues(draft: NutritionDraft): draft is UserConfigurationRequest {
  return Object.values(draft).every(
    (value) => typeof value === 'number' && Number.isFinite(value) && value >= 0,
  )
}

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './settings-page.component.html',
  styles: [
    `
      :host {
        display: block;
      }
      .settings-page {
        max-width: 1180px;
      }
      .settings-nav {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
      }
      .settings-nav a {
        padding: 9px 13px;
        border: 1px solid var(--lifeos-line);
        border-radius: 999px;
        background: var(--lifeos-surface);
        color: var(--lifeos-accent-strong);
        font-weight: 750;
      }
      .settings-nav a:hover {
        background: var(--lifeos-accent-soft);
      }
      .settings-section {
        display: grid;
        gap: 16px;
        scroll-margin-top: 24px;
      }
      .settings-section h2 {
        margin: 0;
        font-size: clamp(1.4rem, 2.4vw, 1.9rem);
      }
      .settings-section > p {
        margin: 0;
        color: var(--lifeos-text-soft);
        line-height: 1.5;
      }
      .settings-panel {
        display: grid;
        gap: 18px;
        padding: 22px;
        border-radius: 22px;
        background: var(--lifeos-surface);
        box-shadow: var(--lifeos-shadow);
      }
      .settings-fields {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 16px;
      }
      .settings-field {
        display: grid;
        gap: 7px;
        min-width: 0;
        color: var(--lifeos-text-soft);
        font-size: 0.88rem;
        font-weight: 750;
      }
      .settings-field input,
      .settings-field select {
        width: 100%;
        min-height: 44px;
        padding: 9px 11px;
        border: 1px solid var(--lifeos-line);
        border-radius: 10px;
        background: var(--lifeos-surface);
        color: var(--lifeos-text);
        font: inherit;
        font-weight: 400;
      }
      .settings-field input:focus-visible,
      .settings-field select:focus-visible,
      .settings-button:focus-visible,
      .settings-nav a:focus-visible,
      .settings-check input:focus-visible {
        outline: 3px solid var(--lifeos-accent);
        outline-offset: 2px;
      }
      .settings-button {
        min-height: 42px;
        padding: 9px 13px;
        border: 1px solid var(--lifeos-line);
        border-radius: 10px;
        background: var(--lifeos-surface-soft);
        color: var(--lifeos-text);
        font: inherit;
        font-weight: 750;
        cursor: pointer;
      }
      .settings-button:hover:not(:disabled) {
        background: var(--lifeos-accent-soft);
      }
      .settings-button:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
      .settings-button.is-primary {
        background: var(--lifeos-accent);
        border-color: var(--lifeos-accent);
        color: white;
      }
      .settings-button.is-primary:hover:not(:disabled) {
        background: var(--lifeos-accent-strong);
      }
      .settings-button.is-danger {
        color: #8b2c20;
      }
      .settings-actions {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 9px;
      }
      .settings-message {
        margin: 0;
        padding: 12px 14px;
        border-radius: 12px;
        line-height: 1.5;
        overflow-wrap: anywhere;
      }
      .settings-message.is-error {
        background: #fff0ed;
        color: #8b2c20;
      }
      .settings-message.is-success {
        background: var(--lifeos-accent-soft);
        color: var(--lifeos-accent-strong);
      }
      .settings-muted {
        margin: 0;
        color: var(--lifeos-text-soft);
        line-height: 1.5;
      }
      .settings-list {
        display: grid;
        gap: 0;
        margin: 0;
        padding: 0;
        list-style: none;
        border: 1px solid var(--lifeos-line);
        border-radius: 14px;
        overflow: hidden;
      }
      .settings-list li {
        display: flex;
        flex-wrap: wrap;
        justify-content: space-between;
        align-items: center;
        gap: 12px;
        padding: 12px 14px;
      }
      .settings-list li + li {
        border-top: 1px solid var(--lifeos-line);
      }
      .settings-list li > span {
        overflow-wrap: anywhere;
      }
      .settings-subheading {
        margin: 0;
        font-size: 1.05rem;
      }
      .settings-check {
        display: inline-flex;
        align-items: center;
        gap: 9px;
        min-height: 44px;
        color: var(--lifeos-text);
      }
      .settings-check input {
        width: 20px;
        height: 20px;
        accent-color: var(--lifeos-accent);
      }
      .settings-day {
        display: grid;
        grid-template-columns: 110px minmax(0, 1fr) auto;
        align-items: end;
        gap: 12px;
      }
      .settings-day strong {
        align-self: center;
      }
      .settings-overrides {
        display: grid;
        gap: 12px;
      }
      .settings-workspace {
        display: grid;
        grid-template-columns: minmax(260px, 0.9fr) minmax(0, 1.2fr);
        align-items: start;
        gap: 18px;
      }
      @media (max-width: 800px) {
        .settings-workspace {
          grid-template-columns: 1fr;
        }
      }
      @media (max-width: 620px) {
        .settings-fields {
          grid-template-columns: 1fr;
        }
        .settings-day {
          grid-template-columns: 1fr;
        }
        .settings-panel {
          padding: 18px;
        }
      }
    `,
  ],
})
export class SettingsPageComponent {
  private readonly api = inject(LifeosApiService)
  readonly weekdays = weekdays
  readonly meals = meals
  readonly workLocations: { value: WorkLocation; label: string }[] = [
    { value: 'home', label: 'Télétravail' },
    { value: 'office', label: 'Bureau' },
    { value: 'off', label: 'Repos' },
  ]
  readonly weekModes: { value: WeekMode; label: string }[] = [
    { value: 'solo', label: 'Solo' },
    { value: 'kids', label: 'Avec enfants' },
  ]

  readonly nutritionLoading = signal(true)
  readonly nutritionReady = signal(false)
  readonly nutritionExists = signal(false)
  readonly nutritionBusy = signal(false)
  readonly nutritionError = signal('')
  readonly nutritionNotice = signal('')
  nutrition = emptyNutrition()

  readonly contextLoading = signal(true)
  readonly contextBusy = signal(false)
  readonly contextError = signal('')
  readonly contextNotice = signal('')
  context: WeekContextPayload | null = null
  overrideDate = ''
  overrideMode: WeekMode = 'solo'

  readonly catalogLoading = signal(true)
  readonly catalogError = signal('')
  readonly components = signal<LibraryMealComponentDto[]>([])
  readonly dishes = signal<LibraryCompositeDishDto[]>([])

  readonly planningLoading = signal(true)
  readonly planningLoaded = signal(false)
  readonly planningBusy = signal(false)
  readonly planningError = signal('')
  readonly planningNotice = signal('')
  readonly planningRules = signal<PlanningRuleDto[]>([])
  readonly editingPlanningId = signal('')
  readonly deletingPlanningId = signal('')
  planning = emptyPlanning()

  readonly frequencyLoading = signal(true)
  readonly frequencyLoaded = signal(false)
  readonly frequencyBusy = signal(false)
  readonly frequencyError = signal('')
  readonly frequencyNotice = signal('')
  readonly frequencyRules = signal<FrequencyRuleDto[]>([])
  readonly editingFrequencyId = signal('')
  readonly deletingFrequencyId = signal('')
  frequency = emptyFrequency()

  constructor() {
    void Promise.all([
      this.loadNutrition(),
      this.loadContext(),
      this.loadCatalog(),
      this.loadPlanningRules(),
      this.loadFrequencyRules(),
    ])
  }

  async loadNutrition(): Promise<void> {
    this.nutritionLoading.set(true)
    this.nutritionError.set('')
    this.nutritionReady.set(false)
    try {
      const saved = await firstValueFrom(
        this.api.get<UserConfigurationDto>('/nutrition/configuration'),
      )
      this.setNutrition(saved)
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.nutrition = emptyNutrition()
        this.nutritionExists.set(false)
        this.nutritionReady.set(true)
      } else {
        this.nutritionError.set(apiErrorMessage(error))
      }
    } finally {
      this.nutritionLoading.set(false)
    }
  }

  private setNutrition(saved: UserConfigurationDto): void {
    this.nutrition = {
      dailyBaseEnergyKcal: saved.dailyBaseEnergyKcal,
      targetNetDeficitKcal: saved.targetNetDeficitKcal,
      targetProteinG: saved.targetProteinG,
      targetCarbsG: saved.targetCarbsG,
      targetFatsG: saved.targetFatsG,
    }
    this.nutritionExists.set(true)
    this.nutritionReady.set(true)
  }

  async saveNutrition(): Promise<void> {
    if (!this.nutritionReady() || this.nutritionBusy()) return
    const draft = this.nutrition
    if (!hasNutritionValues(draft)) {
      this.nutritionError.set('Renseignez les cinq objectifs avec des nombres positifs ou nuls.')
      return
    }
    this.nutritionBusy.set(true)
    this.nutritionError.set('')
    this.nutritionNotice.set('')
    try {
      this.setNutrition(
        await firstValueFrom(
          this.api.post<UserConfigurationDto>('/nutrition/configuration', draft),
        ),
      )
      this.nutritionNotice.set('Objectifs nutritionnels enregistrés.')
    } catch (error) {
      this.nutritionError.set(`Impossible d’enregistrer les objectifs : ${apiErrorMessage(error)}`)
    } finally {
      this.nutritionBusy.set(false)
    }
  }

  async loadContext(): Promise<void> {
    this.contextLoading.set(true)
    this.contextError.set('')
    try {
      const saved = await firstValueFrom(this.api.get<WeekContextPayload>('/week-context'))
      this.context = structuredClone(saved)
    } catch (error) {
      this.contextError.set(
        `Impossible de charger le contexte de semaine : ${apiErrorMessage(error)}`,
      )
    } finally {
      this.contextLoading.set(false)
    }
  }

  addOverride(): void {
    if (!this.context) return
    if (!this.isMonday(this.overrideDate)) {
      this.contextError.set('Choisissez un lundi pour la semaine à remplacer.')
      return
    }
    if (this.context.weekModeOverrides.some((item) => item.weekStartDate === this.overrideDate)) {
      this.contextError.set(
        'Cette semaine possède déjà une exception. Supprimez-la avant de la recréer.',
      )
      return
    }
    this.context.weekModeOverrides.push({
      weekStartDate: this.overrideDate,
      mode: this.overrideMode,
    })
    this.overrideDate = ''
    this.contextError.set('')
    this.contextNotice.set('')
  }

  removeOverride(date: string): void {
    if (!this.context) return
    this.context.weekModeOverrides = this.context.weekModeOverrides.filter(
      (item) => item.weekStartDate !== date,
    )
    this.contextNotice.set('')
  }

  private isMonday(date: string): boolean {
    return /^\d{4}-\d{2}-\d{2}$/.test(date) && new Date(`${date}T00:00:00Z`).getUTCDay() === 1
  }

  async saveContext(): Promise<void> {
    const draft = this.context
    if (!draft || this.contextBusy()) return
    if (
      !this.isMonday(draft.alternatingWeekConfig.referenceWeekStartDate) ||
      draft.weekModeOverrides.some((item) => !this.isMonday(item.weekStartDate))
    ) {
      this.contextError.set('La semaine de référence et les exceptions doivent commencer un lundi.')
      return
    }
    this.contextBusy.set(true)
    this.contextError.set('')
    this.contextNotice.set('')
    try {
      this.context = structuredClone(
        await firstValueFrom(this.api.put<WeekContextPayload>('/week-context', draft)),
      )
      this.contextNotice.set('Contexte de semaine enregistré.')
    } catch (error) {
      this.contextError.set(`Impossible d’enregistrer le contexte : ${apiErrorMessage(error)}`)
    } finally {
      this.contextBusy.set(false)
    }
  }

  async loadCatalog(): Promise<void> {
    this.catalogLoading.set(true)
    this.catalogError.set('')
    const [components, dishes] = await Promise.allSettled([
      firstValueFrom(this.api.get<LibraryMealComponentDto[]>('/meal-components')),
      firstValueFrom(this.api.get<LibraryCompositeDishDto[]>('/composite-dishes')),
    ])
    if (components.status === 'fulfilled') this.components.set(components.value)
    if (dishes.status === 'fulfilled') this.dishes.set(dishes.value)
    const errors = [
      components.status === 'rejected' ? `composants : ${apiErrorMessage(components.reason)}` : '',
      dishes.status === 'rejected' ? `plats : ${apiErrorMessage(dishes.reason)}` : '',
    ].filter(Boolean)
    if (errors.length)
      this.catalogError.set(`Impossible de charger le catalogue (${errors.join(' ; ')}).`)
    this.catalogLoading.set(false)
  }

  targetOptions(kind: TargetKind): { id: string; name: string }[] {
    return kind === 'component' ? this.components() : this.dishes()
  }

  hasTarget(kind: TargetKind, id: string): boolean {
    return this.targetOptions(kind).some((item) => item.id === id)
  }

  targetName(kind: string | undefined, id: string | undefined): string {
    if (!id) return 'Cible inconnue'
    return (
      this.targetOptions(kind === 'dish' ? 'dish' : 'component').find((item) => item.id === id)
        ?.name ?? id
    )
  }

  planningLabel(rule: PlanningRuleDto): string {
    const day = weekdays.find((item) => item.value === rule.weekday)?.label ?? rule.weekday
    const meal = meals.find((item) => item.value === rule.mealType)?.label ?? rule.mealType
    const id = rule.target.kind === 'dish' ? rule.target['dishId'] : rule.target['componentId']
    return `${day} · ${meal} · ${this.targetName(rule.target.kind, id)}`
  }

  frequencyLabel(rule: FrequencyRuleDto): string {
    const kind = rule.target['kind']
    if (kind === 'category') return rule.target['label'] || rule.target['categoryId'] || 'Catégorie'
    const id = kind === 'dish' ? rule.target['dishId'] : rule.target['componentId']
    return this.targetName(kind, id)
  }

  frequencyLabelForEdit(): string {
    const rule = this.frequencyRules().find((item) => item.id === this.editingFrequencyId())
    return rule ? this.frequencyLabel(rule) : ''
  }

  async loadPlanningRules(): Promise<void> {
    this.planningLoading.set(true)
    this.planningLoaded.set(false)
    this.planningError.set('')
    try {
      this.planningRules.set(
        await firstValueFrom(this.api.get<PlanningRuleDto[]>('/planning-rules')),
      )
      this.planningLoaded.set(true)
    } catch (error) {
      this.planningError.set(
        `Impossible de charger les règles de planification : ${apiErrorMessage(error)}`,
      )
    } finally {
      this.planningLoading.set(false)
    }
  }

  editPlanning(rule: PlanningRuleDto): void {
    const kind = rule.target.kind
    if (kind !== 'component' && kind !== 'dish') {
      this.planningError.set('Cette cible n’est pas modifiable depuis ce formulaire.')
      return
    }
    this.planning = {
      weekday: rule.weekday,
      mealType: rule.mealType,
      kind,
      targetId:
        kind === 'component' ? (rule.target['componentId'] ?? '') : (rule.target['dishId'] ?? ''),
    }
    this.editingPlanningId.set(rule.id)
    this.deletingPlanningId.set('')
    this.planningError.set('')
    this.planningNotice.set('')
  }

  resetPlanning(): void {
    this.editingPlanningId.set('')
    this.planning = emptyPlanning()
  }

  async savePlanning(): Promise<void> {
    if (this.planningBusy() || !this.planningLoaded()) return
    const draft = this.planning
    const component = this.components().find((item) => item.id === draft.targetId)
    const previous = this.planningRules().find((item) => item.id === this.editingPlanningId())
    const savedType =
      previous?.target['componentId'] === draft.targetId
        ? previous.target['componentType']
        : undefined
    const knownDish = this.dishes().some((item) => item.id === draft.targetId)
    const existingDish = previous?.target['dishId'] === draft.targetId
    if (
      !draft.targetId ||
      (draft.kind === 'component' && !component && !savedType) ||
      (draft.kind === 'dish' && !knownDish && !existingDish)
    ) {
      this.planningError.set('Choisissez un composant ou un plat du catalogue.')
      return
    }
    if (!draft.weekday || !draft.mealType) {
      this.planningError.set('Choisissez un jour et un repas.')
      return
    }
    let target: PlanningRuleRequest['target']
    if (draft.kind === 'component') {
      const componentType = component?.componentType ?? savedType
      if (!componentType) {
        this.planningError.set('Le type de ce composant n’est pas disponible.')
        return
      }
      target = { kind: 'component', componentId: draft.targetId, componentType }
    } else {
      target = { kind: 'dish', dishId: draft.targetId }
    }
    const request: PlanningRuleRequest = {
      weekday: draft.weekday,
      mealType: draft.mealType,
      target,
    }
    this.planningBusy.set(true)
    this.planningError.set('')
    this.planningNotice.set('')
    try {
      const id = this.editingPlanningId()
      const saved = id
        ? await firstValueFrom(this.api.put<PlanningRuleDto>(`/planning-rules/${id}`, request))
        : await firstValueFrom(this.api.post<PlanningRuleDto>('/planning-rules', request))
      this.planningRules.update((items) =>
        id ? items.map((item) => (item.id === id ? saved : item)) : [...items, saved],
      )
      this.resetPlanning()
      this.planningNotice.set(
        id ? 'Règle de planification modifiée.' : 'Règle de planification ajoutée.',
      )
    } catch (error) {
      this.planningError.set(`Impossible d’enregistrer la règle : ${apiErrorMessage(error)}`)
    } finally {
      this.planningBusy.set(false)
    }
  }

  async deletePlanning(rule: PlanningRuleDto): Promise<void> {
    if (this.planningBusy()) return
    this.planningBusy.set(true)
    this.planningError.set('')
    this.planningNotice.set('')
    try {
      await firstValueFrom(this.api.delete(`/planning-rules/${rule.id}`))
      this.planningRules.update((items) => items.filter((item) => item.id !== rule.id))
      if (this.editingPlanningId() === rule.id) this.resetPlanning()
      this.deletingPlanningId.set('')
      this.planningNotice.set('Règle de planification supprimée.')
    } catch (error) {
      this.planningError.set(`Impossible de supprimer la règle : ${apiErrorMessage(error)}`)
    } finally {
      this.planningBusy.set(false)
    }
  }

  async loadFrequencyRules(): Promise<void> {
    this.frequencyLoading.set(true)
    this.frequencyLoaded.set(false)
    this.frequencyError.set('')
    try {
      this.frequencyRules.set(
        await firstValueFrom(this.api.get<FrequencyRuleDto[]>('/frequency-rules')),
      )
      this.frequencyLoaded.set(true)
    } catch (error) {
      this.frequencyError.set(
        `Impossible de charger les règles de fréquence : ${apiErrorMessage(error)}`,
      )
    } finally {
      this.frequencyLoading.set(false)
    }
  }

  editFrequency(rule: FrequencyRuleDto): void {
    this.editingFrequencyId.set(rule.id)
    this.frequency = {
      kind: rule.target['kind'] as FrequencyKind,
      targetId:
        rule.target['componentId'] ?? rule.target['dishId'] ?? rule.target['categoryId'] ?? '',
      label: rule.target['label'] ?? '',
      count: rule.targetCountPerWeek,
    }
    this.deletingFrequencyId.set('')
    this.frequencyError.set('')
    this.frequencyNotice.set('')
  }

  resetFrequency(): void {
    this.editingFrequencyId.set('')
    this.frequency = emptyFrequency()
  }

  async saveFrequency(): Promise<void> {
    if (this.frequencyBusy() || !this.frequencyLoaded()) return
    const draft = this.frequency
    if (
      draft.count === null ||
      !Number.isInteger(draft.count) ||
      draft.count < 0 ||
      (!this.editingFrequencyId() &&
        (!draft.targetId.trim() || (draft.kind === 'category' && !draft.label.trim())))
    ) {
      this.frequencyError.set(
        'Choisissez une cible et indiquez un nombre entier de fois par semaine (zéro ou plus).',
      )
      return
    }
    if (
      !this.editingFrequencyId() &&
      draft.kind !== 'category' &&
      !this.hasTarget(draft.kind, draft.targetId)
    ) {
      this.frequencyError.set('Choisissez une cible présente dans le catalogue.')
      return
    }
    this.frequencyBusy.set(true)
    this.frequencyError.set('')
    this.frequencyNotice.set('')
    try {
      const id = this.editingFrequencyId()
      const saved = id
        ? await firstValueFrom(
            this.api.put<FrequencyRuleDto>(`/frequency-rules/${id}`, {
              targetCountPerWeek: draft.count,
            }),
          )
        : await firstValueFrom(
            this.api.post<FrequencyRuleDto>('/frequency-rules', {
              target:
                draft.kind === 'component'
                  ? { kind: 'component', componentId: draft.targetId }
                  : draft.kind === 'dish'
                    ? { kind: 'dish', dishId: draft.targetId }
                    : {
                        kind: 'category',
                        categoryId: draft.targetId.trim(),
                        label: draft.label.trim(),
                      },
              targetCountPerWeek: draft.count,
            } satisfies FrequencyRuleRequest),
          )
      this.frequencyRules.update((items) =>
        id ? items.map((item) => (item.id === id ? saved : item)) : [...items, saved],
      )
      this.resetFrequency()
      this.frequencyNotice.set(id ? 'Fréquence modifiée.' : 'Règle de fréquence ajoutée.')
    } catch (error) {
      this.frequencyError.set(`Impossible d’enregistrer la fréquence : ${apiErrorMessage(error)}`)
    } finally {
      this.frequencyBusy.set(false)
    }
  }

  async deleteFrequency(rule: FrequencyRuleDto): Promise<void> {
    if (this.frequencyBusy()) return
    this.frequencyBusy.set(true)
    this.frequencyError.set('')
    this.frequencyNotice.set('')
    try {
      await firstValueFrom(this.api.delete(`/frequency-rules/${rule.id}`))
      this.frequencyRules.update((items) => items.filter((item) => item.id !== rule.id))
      if (this.editingFrequencyId() === rule.id) this.resetFrequency()
      this.deletingFrequencyId.set('')
      this.frequencyNotice.set('Règle de fréquence supprimée.')
    } catch (error) {
      this.frequencyError.set(`Impossible de supprimer la fréquence : ${apiErrorMessage(error)}`)
    } finally {
      this.frequencyBusy.set(false)
    }
  }
}
