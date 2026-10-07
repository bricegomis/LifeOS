import { Component, ElementRef, inject, Input, OnInit, signal, ViewChild } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { firstValueFrom, forkJoin } from 'rxjs'
import { apiErrorMessage, LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type { FoodItemDto, RecipeDto, SportTemplateDto } from '@/app/core/api/api.models'
import type {
  ManualDay, ManualMeal, ManualSport, ManualWeek, ManualWeekSummary, MealLinePayload,
  MealPayload, SportPayload,
} from '@/app/core/api/manual-planner.models'
import { addDays, dayEvents, isoDate, layoutEvents, monday, outsideDisplay, parseTime, timeLabel } from './calendar'
import type { CalendarEvent } from './calendar'

interface EventDraft {
  kind: 'meal' | 'sport'
  id: string
  dayId: string
  start: string
  end: string
  personalPortion: number
  children: number
  source: 'recipe' | 'foods' | 'legacy'
  recipeId: string
  lines: MealLinePayload[]
  templateId: string
  name: string
  sport: string
  intensity: string
  distance: number | null
  calories: number
  replaceContent: boolean
  hasSnapshot: boolean
}

@Component({
  selector: 'app-planning-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './planning-page.component.html',
  styleUrl: './planning-page.component.css',
})
export class PlanningPageComponent implements OnInit {
  private readonly api = inject(LifeosApiService)
  @Input() todayOnly = false
  @ViewChild('editorHeading') editorHeading?: ElementRef<HTMLElement>
  readonly week = signal<ManualWeek | null>(null)
  readonly summaries = signal<ManualWeekSummary[]>([])
  readonly foods = signal<FoodItemDto[]>([])
  readonly recipes = signal<RecipeDto[]>([])
  readonly templates = signal<SportTemplateDto[]>([])
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly catalogError = signal('')
  readonly destinationDays = signal<ManualDay[]>([])
  readonly hours = Array.from({ length: 14 }, (_, index) => index + 6)
  readonly events = dayEvents
  readonly layout = layoutEvents
  readonly outside = outsideDisplay
  readonly time = timeLabel
  startDate = monday()
  selectedDate = isoDate()
  timeZone = 'Europe/Paris'
  catalogSearch = ''
  destinationWeekId = ''
  draft: EventDraft | null = null

  ngOnInit(): void { void this.loadWeek(); void this.loadCatalogs() }

  dateLabel(date: string, short = false): string {
    return new Intl.DateTimeFormat('fr-FR', { weekday: short ? 'short' : 'long', day: 'numeric', month: short ? undefined : 'long' })
      .format(new Date(`${date}T12:00:00`))
  }

  number(value: number | null): string {
    return value === null ? 'inconnu' : new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 1 }).format(value)
  }
  selectedDay(): ManualDay | undefined { return this.week()?.days.find(d => d.date === this.selectedDate) }
  activeFoods(): FoodItemDto[] {
    return this.foods().filter(f => !f.isArchived && f.name.toLocaleLowerCase('fr').includes(this.catalogSearch.toLocaleLowerCase('fr')))
  }
  activeRecipes(): RecipeDto[] {
    return this.recipes().filter(r => !r.isArchived && r.name.toLocaleLowerCase('fr').includes(this.catalogSearch.toLocaleLowerCase('fr')))
  }
  activeTemplates(): SportTemplateDto[] {
    return this.templates().filter(t => !t.isArchived && t.name.toLocaleLowerCase('fr').includes(this.catalogSearch.toLocaleLowerCase('fr')))
  }
  unplaced(day: ManualDay): CalendarEvent[] { return this.events(day).filter(e => e.value.startMinute === null) }
  outOfRange(day: ManualDay): CalendarEvent[] { return this.events(day).filter(this.outside) }
  sportTotal(day: ManualDay): number { return day.sports.reduce((sum, s) => sum + s.calories, 0) }

  async loadCatalogs(): Promise<void> {
    this.catalogError.set('')
    try {
      const result = await firstValueFrom(forkJoin({
        foods: this.api.get<FoodItemDto[]>('/food-items'),
        recipes: this.api.get<RecipeDto[]>('/recipes'),
        sports: this.api.get<SportTemplateDto[]>('/sport-templates'),
      }))
      this.foods.set(result.foods); this.recipes.set(result.recipes); this.templates.set(result.sports)
    } catch (error) { this.catalogError.set(apiErrorMessage(error)) }
  }

  async loadWeek(): Promise<void> {
    this.loading.set(true); this.error.set('')
    try {
      const summaries = await firstValueFrom(this.api.get<ManualWeekSummary[]>('/manual-planner/weeks'))
      this.summaries.set(summaries)
      const summary = summaries.find(w => w.startsOn === this.startDate)
      this.week.set(summary
        ? await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${summary.id}`)) : null)
      if (!this.week()?.days.some(d => d.date === this.selectedDate))
        this.selectedDate = this.startDate
    } catch (error) { this.error.set(apiErrorMessage(error)); this.week.set(null) }
    finally { this.loading.set(false) }
  }

  navigateWeek(offset: number): void {
    if (this.loading() || this.busy()) return
    this.startDate = addDays(this.startDate, offset * 7)
    this.selectedDate = this.startDate
    this.draft = null; this.notice.set('')
    void this.loadWeek()
  }

  chooseWeek(date: string): void {
    this.startDate = monday(date); this.selectedDate = date
    this.draft = null; this.notice.set(''); void this.loadWeek()
  }

  today(): void { this.chooseWeek(isoDate()) }

  async createWeek(): Promise<void> {
    if (this.busy() || this.loading()) return
    this.busy.set(true); this.error.set('')
    try {
      const week = await firstValueFrom(this.api.post<ManualWeek>('/manual-planner/weeks', { startsOn: this.startDate, timeZoneId: this.timeZone }))
      this.week.set(week); this.notice.set('Semaine créée, sans génération ni réglages automatiques.')
      this.summaries.update(items => [week, ...items])
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  add(day: ManualDay, minute = 480, kind: 'meal' | 'sport' = 'meal'): void {
    if (this.busy()) return
    this.catalogSearch = ''; this.error.set(''); this.notice.set('')
    this.draft = {
      kind, id: '', dayId: day.id, start: timeLabel(minute), end: timeLabel(minute + 30),
      personalPortion: 1, children: 0, source: 'recipe', recipeId: '', lines: [],
      templateId: '', name: '', sport: 'run', intensity: 'moderate', distance: null, calories: 0,
      replaceContent: false, hasSnapshot: true,
    }
    this.destinationWeekId = this.week()!.id
    this.destinationDays.set(this.week()!.days)
    this.focusEditor()
  }

  edit(event: CalendarEvent): void {
    if (this.busy()) return
    const value = event.value
    this.catalogSearch = ''; this.error.set(''); this.notice.set('')
    this.draft = {
      kind: event.kind, id: value.id, dayId: value.dayPlanId,
      start: value.startMinute === null ? '' : timeLabel(value.startMinute),
      end: value.endMinute === null ? '' : timeLabel(value.endMinute),
      personalPortion: 1, children: 0, source: 'foods', recipeId: '', lines: [],
      templateId: '', name: value.name, sport: 'run', intensity: 'moderate', distance: null, calories: 0,
      replaceContent: false, hasSnapshot: true,
    }
    this.destinationWeekId = this.week()!.id
    this.destinationDays.set(this.week()!.days)
    if (event.kind === 'meal') {
      const meal = value as ManualMeal
      Object.assign(this.draft, {
        personalPortion: meal.personalPortion, children: meal.childrenCount,
        source: meal.composedMealId ? 'legacy' : meal.recipeId ? 'recipe' : 'foods',
        recipeId: meal.recipeId ?? '', hasSnapshot: meal.hasSnapshot,
        lines: meal.lines.map(l => ({ foodItemId: l.foodItemId, quantity: l.quantity, unit: l.unit, snapshotLineId: l.id })),
      })
    } else {
      const sport = value as ManualSport
      Object.assign(this.draft, { templateId: sport.sportTemplateId ?? '', sport: sport.sport, intensity: sport.intensity, distance: sport.distanceKm, calories: sport.calories })
    }
    this.focusEditor()
  }

  private focusEditor(): void {
    setTimeout(() => this.editorHeading?.nativeElement.focus(), 0)
  }

  async moveToWeek(id: string): Promise<void> {
    if (!this.draft || this.busy()) return
    this.busy.set(true); this.error.set('')
    try {
      const week = await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${id}`))
      this.destinationWeekId = id
      this.destinationDays.set(week.days)
      this.draft.dayId = week.days[0]!.id
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  changeKind(kind: 'meal' | 'sport'): void {
    if (!this.draft || this.draft.id) return
    this.draft.kind = kind
  }

  selectTemplate(id: string): void {
    const template = this.templates().find(t => t.id === id)
    if (!template || !this.draft) return
    Object.assign(this.draft, { templateId: id, name: template.name, sport: template.sport,
      intensity: template.intensity, distance: template.distanceKm, calories: template.calories })
    const start = parseTime(this.draft.start)
    if (start !== null && start + template.durationMinutes < 1440)
      this.draft.end = timeLabel(start + template.durationMinutes)
  }

  addLine(): void { this.draft?.lines.push({ foodItemId: '', quantity: 1, unit: 'g' }) }

  lineName(line: MealLinePayload): string {
    const day = this.week()?.days.flatMap(d => d.meals).find(m => m.id === this.draft?.id)
    return day?.lines.find(l => l.id === line.snapshotLineId)?.name
      ?? this.foods().find(f => f.id === line.foodItemId)?.name ?? 'Produit indisponible'
  }

  contentEditable(): boolean { return Boolean(this.draft && (!this.draft.id || this.draft.replaceContent)) }

  replace(): void {
    if (!this.draft) return
    this.draft.replaceContent = true
    this.draft.source = 'recipe'; this.draft.recipeId = ''; this.draft.lines = []; this.draft.templateId = ''
    this.draft.hasSnapshot = true
  }

  async save(): Promise<void> {
    const draft = this.draft
    if (!draft || this.busy()) return
    const start = parseTime(draft.start)
    const end = draft.end === '24:00' ? 1440 : parseTime(draft.end)
    if (start === null || end === null || end <= start) {
      this.error.set('Choisissez un début et une fin valides dans la même journée, fin après début.')
      return
    }
    if (draft.kind === 'meal' && (!Number.isFinite(draft.personalPortion) || draft.personalPortion <= 0
      || !Number.isInteger(draft.children) || draft.children < 0)) {
      this.error.set('La portion doit être positive et le nombre d’enfants un entier non négatif.')
      return
    }
    if (draft.kind === 'meal' && this.contentEditable()
      && (draft.source === 'recipe' ? !draft.recipeId : !draft.lines.length || draft.lines.some(l => !l.foodItemId || l.quantity <= 0 || !l.unit))) {
      this.error.set('Sélectionnez une recette, ou au moins un produit avec quantité et unité.')
      return
    }
    const meal: MealPayload = {
      dayPlanId: draft.dayId, startMinute: start, endMinute: end,
      personalPortion: draft.personalPortion, childrenCount: draft.children,
      recipeId: draft.source === 'recipe' ? draft.recipeId || null : null,
      lines: draft.source === 'foods' ? draft.lines : null, replaceContent: draft.replaceContent,
    }
    const sport: SportPayload = {
      dayPlanId: draft.dayId, startMinute: start, endMinute: end,
      sportTemplateId: draft.templateId || null, name: draft.name, sport: draft.sport,
      intensity: draft.intensity, durationMinutes: end - start, distanceKm: draft.distance,
      calories: draft.calories, replaceContent: draft.replaceContent,
    }
    this.busy.set(true); this.error.set(''); this.notice.set('')
    let persisted = false
    try {
      const path = `/manual-planner/${draft.kind === 'meal' ? 'meals' : 'sports'}`
      await firstValueFrom(draft.id ? this.api.put(`${path}/${draft.id}`, draft.kind === 'meal' ? meal : sport)
        : this.api.post(path, draft.kind === 'meal' ? meal : sport))
      persisted = true
      this.draft = null
      await this.refresh()
      this.notice.set('Événement enregistré.')
    } catch (error) { this.error.set(`${persisted ? 'Événement sauvegardé, mais actualisation impossible. Réessayez la lecture. ' : ''}${apiErrorMessage(error)}`) }
    finally { this.busy.set(false) }
  }

  async remove(): Promise<void> {
    const draft = this.draft
    if (!draft?.id || this.busy() || !window.confirm(`Supprimer cet événement « ${draft.name} » ?`)) return
    this.busy.set(true); this.error.set(''); this.notice.set('')
    try {
      await firstValueFrom(this.api.delete(`/manual-planner/${draft.kind === 'meal' ? 'meals' : 'sports'}/${draft.id}`))
      this.draft = null; await this.refresh(); this.notice.set('Événement supprimé.')
    } catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  private async refresh(): Promise<void> {
    const week = this.week()
    if (week) this.week.set(await firstValueFrom(this.api.get<ManualWeek>(`/manual-planner/weeks/${week.id}`)))
  }
}
