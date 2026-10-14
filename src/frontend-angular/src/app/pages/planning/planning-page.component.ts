import { Component, inject, Input, OnInit, signal } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { firstValueFrom, forkJoin } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type {
  FoodItemDto, HouseholdGroupDto, MemberProfileDto, RecipeDto, SportTemplateDto,
} from '@/app/core/api/api.models'
import type {
  ManualDay, ManualMeal, ManualSport, ManualWeek, ManualWeekSummary, MealPayload, SportPayload,
} from '@/app/core/api/manual-planner.models'
import { LifeosDialogComponent } from '@/app/shared/dialog/lifeos-dialog.component'
import { HouseholdReviewsComponent } from '@/app/shared/reviews/household-reviews.component'
import { formSnapshot, hasChanges } from '@/app/shared/dialog/dialog-guard'
import { addDays, dayEvents, isoDate, layoutEvents, monday, outsideDisplay, parseTime, timeLabel } from './calendar'
import type { CalendarEvent } from './calendar'
import {
  emptyPreparation, eventDraftFrom, eventDraftHasSecondaryValues, eventDraftProblem, newEventDraft, recipeLineRequest,
} from './event-draft'
import type { EventDraft, MealLineDraft } from './event-draft'

import { CookingSessionBannerComponent } from '@/app/shared/cooking/cooking-session-banner.component'
import { CookingSessionDialogComponent } from '@/app/shared/cooking/cooking-session-dialog.component'
import { NativeSyncService } from '@/app/core/native/native-sync.service'

@Component({
  selector: 'app-planning-page',
  standalone: true,
  imports: [FormsModule, LifeosDialogComponent, HouseholdReviewsComponent, CookingSessionBannerComponent, CookingSessionDialogComponent],
  templateUrl: './planning-page.component.html',
  styleUrl: './planning-page.component.css',
})
export class PlanningPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)
  private readonly nativeSync = inject(NativeSyncService)
  @Input() todayOnly = false
  readonly week = signal<ManualWeek | null>(null)
  readonly summaries = signal<ManualWeekSummary[]>([])
  readonly foods = signal<FoodItemDto[]>([])
  readonly recipes = signal<RecipeDto[]>([])
  readonly templates = signal<SportTemplateDto[]>([])
  readonly profiles = signal<MemberProfileDto[]>([])
  readonly groups = signal<HouseholdGroupDto[]>([])
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly catalogError = signal('')
  readonly dialogError = signal('')
  readonly destinationDays = signal<ManualDay[]>([])
  readonly hours = Array.from({ length: 14 }, (_, index) => index + 6)
  readonly events = dayEvents
  readonly layout = layoutEvents
  readonly outside = outsideDisplay
  readonly time = timeLabel
  startDate = monday()
  selectedDate = isoDate()
  cookingDialogOpen = signal(false)
  selectedCookingLine = signal<{ title: string; recipeId: string | null; instructions: string | null } | null>(null)

  startCookingLine(line: { name: string | null; recipeId: string | null; instructions: string | null }): void {
    this.selectedCookingLine.set({
      title: line.name || 'Recette',
      recipeId: line.recipeId,
      instructions: line.instructions,
    })
    this.cookingDialogOpen.set(true)
  }
  timeZone = 'Europe/Paris'
  catalogSearch = ''
  destinationWeekId = ''
  draft: EventDraft | null = null
  showMoreOptions = false
  private initialDraft = ''

  ngOnInit(): void {
    void this.loadWeek()
    void this.loadCatalogs()
  }

  dateLabel(date: string, short = false): string {
    return new Intl.DateTimeFormat('fr-FR', {
      weekday: short ? 'short' : 'long',
      day: 'numeric',
      month: short ? undefined : 'long',
    }).format(new Date(`${date}T12:00:00`))
  }

  number(value: number | null): string {
    return value === null ? 'inconnu' : new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 2 }).format(value)
  }

  selectedDay(): ManualDay | undefined { return this.week()?.days.find(day => day.date === this.selectedDate) }
  activeFoods(): FoodItemDto[] {
    const search = this.catalogSearch.toLocaleLowerCase('fr')
    return this.foods().filter(food => !food.isArchived && food.name.toLocaleLowerCase('fr').includes(search))
  }
  activeRecipes(): RecipeDto[] {
    const search = this.catalogSearch.toLocaleLowerCase('fr')
    return this.recipes().filter(recipe => !recipe.isArchived && recipe.name.toLocaleLowerCase('fr').includes(search))
  }
  activeTemplates(): SportTemplateDto[] {
    const search = this.catalogSearch.toLocaleLowerCase('fr')
    return this.templates().filter(template => !template.isArchived && template.name.toLocaleLowerCase('fr').includes(search))
  }
  unplaced(day: ManualDay): CalendarEvent[] { return this.events(day).filter(event => event.value.startMinute === null) }
  outOfRange(day: ManualDay): CalendarEvent[] { return this.events(day).filter(this.outside) }
  sportTotal(day: ManualDay): number { return day.sports.reduce((sum, sport) => sum + sport.calories, 0) }

  lineName(line: MealLineDraft): string {
    if (line.recipeId) return this.recipes().find(recipe => recipe.id === line.recipeId)?.name ?? 'Recette indisponible'
    return line.name.trim() || 'Préparation ponctuelle'
  }

  portionTotal(line: MealLineDraft): number {
    return line.portions.reduce((sum, portion) => sum + Number(portion.servings), 0)
  }

  async loadCatalogs(): Promise<void> {
    this.catalogError.set('')
    try {
      const result = await firstValueFrom(forkJoin({
        foods: this.api.get<FoodItemDto[]>('/foods'),
        recipes: this.api.get<RecipeDto[]>('/recipes'),
        sports: this.api.get<SportTemplateDto[]>('/sport-templates'),
        household: this.api.get<{ memberProfiles: MemberProfileDto[] }>('/households/members'),
        groups: this.api.get<HouseholdGroupDto[]>('/household-groups'),
      }))
      this.foods.set(result.foods)
      this.recipes.set(result.recipes)
      this.templates.set(result.sports)
      this.profiles.set(result.household.memberProfiles)
      this.groups.set(result.groups)
    } catch (error) {
      this.catalogError.set(apiErrorMessage(error))
    }
  }

  async loadWeek(): Promise<void> {
    this.loading.set(true)
    this.error.set('')
    try {
      const summaries = await firstValueFrom(this.api.get<ManualWeekSummary[]>('/manual-planner/weeks'))
      this.summaries.set(summaries)
      const summary = summaries.find(item => item.startsOn === this.startDate)
      this.week.set(summary
        ? await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${summary.id}`))
        : null)
      if (!this.week()?.days.some(day => day.date === this.selectedDate)) this.selectedDate = this.startDate
    } catch (error) {
      this.error.set(apiErrorMessage(error))
      this.week.set(null)
    } finally {
      this.loading.set(false)
    }
  }

  navigateWeek(offset: number): void {
    if (this.loading() || this.busy()) return
    this.startDate = addDays(this.startDate, offset * 7)
    this.selectedDate = this.startDate
    this.draft = null
    this.notice.set('')
    void this.loadWeek()
  }

  chooseWeek(date: string): void {
    this.startDate = monday(date)
    this.selectedDate = date
    this.draft = null
    this.notice.set('')
    void this.loadWeek()
  }

  today(): void { this.chooseWeek(isoDate()) }

  async createWeek(): Promise<void> {
    if (this.busy() || this.loading()) return
    this.busy.set(true)
    this.error.set('')
    try {
      const week = await firstValueFrom(this.api.post<ManualWeek>('/manual-planner/weeks', {
        startsOn: this.startDate, timeZoneId: this.timeZone,
      }))
      this.setWeek(week)
      this.notice.set('Semaine créée, sans génération ni réglages automatiques.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async duplicateWeek(): Promise<void> {
    const source = this.week()
    if (!source || this.busy()) return
    this.busy.set(true)
    this.error.set('')
    try {
      const week = await firstValueFrom(this.api.post<ManualWeek>(`/manual-planner/weeks/${source.id}/duplicate`, {
        startsOn: this.startDate, timeZoneId: source.timeZoneId,
      }))
      this.setWeek(week)
      this.notice.set('Semaine copiée. Repas, préparations et avis ponctuels sont indépendants de la semaine d’origine.')
    } catch (error) {
      this.error.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  add(day: ManualDay | undefined = this.selectedDay() ?? this.week()?.days[0], minute = 480, kind: 'meal' | 'sport' = 'meal'): void {
    if (this.busy() || !day) return
    this.openDraft(newEventDraft(day.id, minute, kind))
  }

  edit(event: CalendarEvent): void {
    if (this.busy()) return
    this.openDraft(eventDraftFrom(event))
  }

  isDirty(): boolean { return Boolean(this.draft && hasChanges(this.initialDraft, this.draft)) }

  setEndOfDay(draft: EventDraft, event: Event): void {
    draft.end = (event.target as HTMLInputElement).checked ? '24:00' : ''
  }

  closeDialog(): void { this.draft = null; this.dialogError.set('') }

  private openDraft(draft: EventDraft): void {
    this.catalogSearch = ''
    this.dialogError.set('')
    this.notice.set('')
    this.draft = draft
    this.initialDraft = formSnapshot(draft)
    this.showMoreOptions = eventDraftHasSecondaryValues(draft)
    this.destinationWeekId = this.week()!.id
    this.destinationDays.set(this.week()!.days)
  }

  async moveToWeek(id: string): Promise<void> {
    if (!this.draft || this.busy()) return
    this.busy.set(true)
    this.dialogError.set('')
    try {
      const week = await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${id}`))
      this.destinationWeekId = id
      this.destinationDays.set(week.days)
      this.draft.dayId = week.days[0]!.id
    } catch (error) {
      this.dialogError.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  changeKind(kind: 'meal' | 'sport'): void {
    if (!this.draft || this.draft.id) return
    this.draft.kind = kind
  }

  addRecipeLine(): void { this.draft?.recipeLines.push(emptyPreparation()) }

  removeRecipeLine(index: number): void { this.draft?.recipeLines.splice(index, 1) }

  selectRecipe(line: MealLineDraft, id: string): void {
    line.recipeId = id
    line.name = ''
    line.instructions = ''
    const recipe = this.recipes().find(item => item.id === id)
    if (recipe) line.yieldServings = recipe.servings
  }

  addIngredient(line: MealLineDraft): void {
    const food = this.activeFoods()[0]
    line.ingredients.push({ foodItemId: food?.id ?? '', quantity: 1, unit: food?.referenceUnit ?? 'g' })
  }

  selectIngredient(ingredient: MealLineDraft['ingredients'][number], id: string): void {
    ingredient.foodItemId = id
    ingredient.unit = this.foods().find(food => food.id === id)?.referenceUnit ?? ingredient.unit
  }

  addPortion(line: MealLineDraft): void {
    const next = this.profiles().find(profile =>
      !line.portions.some(portion => portion.memberProfileId === profile.id))
    if (!next) {
      this.dialogError.set('Aucune autre personne du foyer à répartir. Ajoutez ou modifiez les profils dans Réglages.')
      return
    }
    line.plannedServings = null
    line.portions.push({ memberProfileId: next.id, servings: next.portionCoefficient })
  }

  useProfileDefaults(line: MealLineDraft): void {
    line.plannedServings = null
    line.portions = this.profiles().map(profile => ({
      memberProfileId: profile.id, servings: profile.portionCoefficient,
    }))
    this.dialogError.set('')
  }

  removePortion(line: MealLineDraft, index: number): void {
    line.portions.splice(index, 1)
    if (line.portions.length === 0) line.plannedServings = 1
  }

  selectTemplate(id: string): void {
    const template = this.templates().find(item => item.id === id)
    if (!template || !this.draft) return
    Object.assign(this.draft, {
      templateId: id, name: template.name, sport: template.sport,
      intensity: template.intensity, distance: template.distanceKm, calories: template.calories,
    })
    const start = parseTime(this.draft.start)
    if (start !== null && start + template.durationMinutes < 1440)
      this.draft.end = timeLabel(start + template.durationMinutes)
  }

  contentEditable(): boolean {
    return Boolean(this.draft && (this.draft.kind === 'sport' || !this.draft.id || this.draft.replaceContent))
  }

  replaceMealContent(): void {
    if (!this.draft || this.draft.kind !== 'meal') return
    this.draft.replaceContent = true
    this.draft.recipeLines = []
    this.dialogError.set('')
  }

  async save(): Promise<void> {
    const draft = this.draft
    if (!draft || this.busy()) return
    const problem = eventDraftProblem(draft, this.contentEditable())
    if (problem) { this.dialogError.set(problem); return }
    const startMinute = parseTime(draft.start)
    const endMinute = draft.end === '24:00' ? 1440 : parseTime(draft.end)
    const meal: MealPayload = {
      dayPlanId: draft.dayId, startMinute, endMinute,
      recipeLines: this.contentEditable() ? draft.recipeLines.map(recipeLineRequest) : [],
      replaceContent: draft.replaceContent,
    }
    const sport: SportPayload = {
      dayPlanId: draft.dayId, startMinute, endMinute,
      sportTemplateId: draft.templateId || null, name: draft.name, sport: draft.sport,
      intensity: draft.intensity, durationMinutes: endMinute! - startMinute!,
      distanceKm: draft.distance,       calories: draft.calories, replaceContent: draft.templateId !== draft.initialTemplateId,
    }
    this.busy.set(true)
    this.dialogError.set('')
    this.notice.set('')
    let persisted = false
    try {
      const path = `/manual-planner/${draft.kind === 'meal' ? 'meals' : 'sports'}`
      await firstValueFrom(draft.id
        ? this.api.put(`${path}/${draft.id}`, draft.kind === 'meal' ? meal : sport)
        : this.api.post(path, draft.kind === 'meal' ? meal : sport))
      persisted = true
      this.closeDialog()
      await this.refresh()
      this.notice.set(draft.id ? 'Événement enregistré.' : 'Événement créé.')
    } catch (error) {
      if (persisted) this.error.set(`Événement sauvegardé, mais actualisation impossible. Réessayez la lecture. ${apiErrorMessage(error)}`)
      else this.dialogError.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  async remove(): Promise<void> {
    const draft = this.draft
    if (!draft?.id || this.busy() || !window.confirm(`Supprimer cet événement « ${draft.name} » ?`)) return
    this.busy.set(true)
    this.dialogError.set('')
    this.notice.set('')
    let removed = false
    try {
      await firstValueFrom(this.api.delete(`/manual-planner/${draft.kind === 'meal' ? 'meals' : 'sports'}/${draft.id}`))
      removed = true
      this.closeDialog()
      await this.refresh()
      this.notice.set('Événement supprimé.')
    } catch (error) {
      if (removed) this.error.set(`Événement supprimé, mais actualisation impossible. Réessayez la lecture. ${apiErrorMessage(error)}`)
      else this.dialogError.set(apiErrorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }

  private setWeek(week: ManualWeek): void {
    this.week.set(week)
    this.summaries.update(items => [week, ...items.filter(item => item.id !== week.id)])
    void this.nativeSync.syncNativeState()
  }

  private async refresh(): Promise<void> {
    const week = this.week()
    if (week) {
      this.setWeek(await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${week.id}`)))
    } else {
      await this.loadWeek()
      void this.nativeSync.syncNativeState()
    }
  }
}
