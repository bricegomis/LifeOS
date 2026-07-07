import type {
  Activity,
  AssembledMeal,
  ComponentType,
  CompositeDish,
  DayPlan,
  FrequencyRule,
  FrequencyRuleTarget,
  MealComponent,
  MealDefinition,
  MealSlot,
  MealType,
  PlanningRule,
  WeekPlan,
  Weekday,
} from '@/types'

interface MealLibrary {
  mealComponents: MealComponent[]
  compositeDishes: CompositeDish[]
  activities: Activity[]
}

interface GenerateWeekPlanOptions {
  library: MealLibrary
  planningRules: PlanningRule[]
  frequencyRules: FrequencyRule[]
  startDate: string
}

type MealSequenceItem = {
  dayIndex: number
  mealType: MealType
}

type DraftDayPlan = Omit<DayPlan, 'breakfast' | 'lunch' | 'dinner' | 'activity'> & {
  breakfast: MealSlot | null
  lunch: MealSlot | null
  dinner: MealSlot | null
  activity: Activity | null
}

const weekdays: Weekday[] = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday',
]

const mealTypes: MealType[] = ['breakfast', 'lunch', 'dinner']
const activityPlan = ['running', 'strength', 'mobility', 'running', 'strength', 'long-running', 'rest']
const pleasureDishIds = new Set(['homemade-pizza', 'lasagna', 'buckwheat-cheese-pasta'])

export function createGeneratedWeekPlan({
  library,
  planningRules,
  frequencyRules,
  startDate,
}: GenerateWeekPlanOptions): WeekPlan {
  const state = createGeneratorState(library)
  const days = createEmptyWeek(startDate)
  const fixedSlots = new Set<string>()

  applyFixedRules(days, planningRules, state, fixedSlots)
  applyFrequencyRules(days, frequencyRules, state, fixedSlots)
  completeMeals(days, state, frequencyRules)
  generateActivities(days, state.activities)

  return {
    id: `week-${startDate}`,
    startDate,
    status: 'Generated',
    days: days.map((day) => ({
      ...day,
      breakfast: requireSlot(day.breakfast, day.id, 'breakfast'),
      lunch: requireSlot(day.lunch, day.id, 'lunch'),
      dinner: requireSlot(day.dinner, day.id, 'dinner'),
      activity: day.activity ?? requireArrayValue(state.activities, 0, 'activity'),
    })),
  }
}

function createGeneratorState(library: MealLibrary) {
  const activeComponents = library.mealComponents.filter((component) => component.active)
  const activeDishes = library.compositeDishes.filter((dish) => dish.active)

  return {
    componentsByType: {
      protein: activeComponents.filter((component) => component.componentType === 'protein'),
      starch: activeComponents.filter((component) => component.componentType === 'starch'),
      vegetable: activeComponents.filter((component) => component.componentType === 'vegetable'),
      optional: activeComponents.filter((component) => component.componentType === 'optional'),
    },
    dishes: activeDishes,
    activities: library.activities,
    rotation: {
      breakfast: 0,
      protein: 0,
      starch: 0,
      vegetable: 0,
      optional: 0,
      dish: 0,
      pleasureDish: 0,
    },
  }
}

function createEmptyWeek(startDate: string): DraftDayPlan[] {
  const weekdayFormatter = new Intl.DateTimeFormat('fr-FR', { weekday: 'long' })
  const shortFormatter = new Intl.DateTimeFormat('fr-FR', {
    day: 'numeric',
    month: 'short',
  })
  const start = new Date(`${startDate}T00:00:00`)

  return Array.from({ length: 7 }, (_, index) => {
    const date = new Date(start)
    date.setDate(date.getDate() + index)
    const dateLabel = weekdayFormatter.format(date)

    return {
      id: `day-${index + 1}`,
      dateLabel: dateLabel.charAt(0).toUpperCase() + dateLabel.slice(1),
      shortDateLabel: shortFormatter.format(date),
      breakfast: null,
      lunch: null,
      dinner: null,
      activity: null,
    }
  })
}

function applyFixedRules(
  days: DraftDayPlan[],
  planningRules: PlanningRule[],
  state: ReturnType<typeof createGeneratorState>,
  fixedSlots: Set<string>,
): void {
  for (const rule of planningRules) {
    const dayIndex = weekdays.indexOf(rule.weekday)

    if (dayIndex < 0) {
      continue
    }

    const slot = createMealFromPlanningRule(rule, dayIndex, state)
    const day = days[dayIndex]

    if (slot && day) {
      day[rule.mealType] = slot
      fixedSlots.add(slotKey(dayIndex, rule.mealType))
    }
  }
}

function createMealFromPlanningRule(
  rule: PlanningRule,
  dayIndex: number,
  state: ReturnType<typeof createGeneratorState>,
): MealSlot | null {
  const target = rule.target

  if (target.kind === 'dish') {
    const dish = state.dishes.find((item) => item.id === target.dishId)

    return dish ? createMealSlot(dayIndex, rule.mealType, cloneDish(dish)) : null
  }

  const component = state.componentsByType[target.componentType].find(
    (item) => item.id === target.componentId,
  )

  if (!component) {
    return null
  }

  return createMealSlot(
    dayIndex,
    rule.mealType,
    createAssembledMeal(
      `generated-${dayIndex + 1}-${rule.mealType}-${component.id}`,
      [component, nextComponent(state, 'starch'), nextComponent(state, 'vegetable')],
      15,
    ),
  )
}

function applyFrequencyRules(
  days: DraftDayPlan[],
  frequencyRules: FrequencyRule[],
  state: ReturnType<typeof createGeneratorState>,
  fixedSlots: Set<string>,
): void {
  for (const rule of frequencyRules) {
    let currentCount = countFrequencyTarget(days, rule.target)
    const targetCount = Math.max(0, Math.round(rule.targetCountPerWeek))

    while (currentCount < targetCount) {
      const placement = findFrequencyPlacement(days, rule.target, fixedSlots)

      if (!placement) {
        break
      }

      const meal = createMealForFrequencyTarget(rule.target, placement, state)

      if (!meal) {
        break
      }

      const day = days[placement.dayIndex]

      if (!day) {
        break
      }

      day[placement.mealType] = meal
      currentCount += 1
    }
  }
}

function findFrequencyPlacement(
  days: DraftDayPlan[],
  target: FrequencyRuleTarget,
  fixedSlots: Set<string>,
): MealSequenceItem | null {
  const candidates = mealSequence().filter(({ dayIndex, mealType }) => {
    const day = days[dayIndex]

    if (!day || fixedSlots.has(slotKey(dayIndex, mealType)) || day[mealType]) {
      return false
    }

    if (target.kind === 'component') {
      return mealType !== 'breakfast'
    }

    return target.kind === 'dish' ? true : mealType !== 'breakfast'
  })

  return candidates.find((candidate) => !wouldRepeatTarget(days, candidate, target)) ?? candidates[0] ?? null
}

function createMealForFrequencyTarget(
  target: FrequencyRuleTarget,
  placement: MealSequenceItem,
  state: ReturnType<typeof createGeneratorState>,
): MealSlot | null {
  if (target.kind === 'component') {
    const component =
      state.componentsByType.protein.find((item) => item.id === target.componentId) ??
      state.componentsByType.starch.find((item) => item.id === target.componentId) ??
      state.componentsByType.vegetable.find((item) => item.id === target.componentId) ??
      state.componentsByType.optional.find((item) => item.id === target.componentId)

    if (!component) {
      return null
    }

    return createMealSlot(
      placement.dayIndex,
      placement.mealType,
      createAssembledMeal(
        `generated-${placement.dayIndex + 1}-${placement.mealType}-${component.id}`,
        componentsForForcedComponent(component, state),
        15,
      ),
    )
  }

  if (target.kind === 'dish') {
    const dish = state.dishes.find((item) => item.id === target.dishId)

    return dish ? createMealSlot(placement.dayIndex, placement.mealType, cloneDish(dish)) : null
  }

  const dish = nextPleasureDish(state, placement.mealType)

  return dish ? createMealSlot(placement.dayIndex, placement.mealType, cloneDish(dish)) : null
}

function completeMeals(
  days: DraftDayPlan[],
  state: ReturnType<typeof createGeneratorState>,
  frequencyRules: FrequencyRule[],
): void {
  for (const { dayIndex, mealType } of mealSequence()) {
    const day = days[dayIndex]

    if (!day || day[mealType]) {
      continue
    }

    const previousMeal = previousFilledMeal(days, dayIndex, mealType)
    day[mealType] =
      mealType === 'breakfast'
        ? nextBreakfast(dayIndex, state, previousMeal)
        : nextLunchOrDinner(dayIndex, mealType, state, previousMeal, days, frequencyRules)
  }
}

function nextBreakfast(
  dayIndex: number,
  state: ReturnType<typeof createGeneratorState>,
  previousMeal: MealSlot | null,
): MealSlot {
  const breakfastDishes = dishesForMealType(state.dishes, 'breakfast')
  const options: MealDefinition[] = [
    ...breakfastDishes.map(cloneDish),
    createAssembledMeal(
      `breakfast-${dayIndex + 1}-eggs`,
      [componentById(state, 'eggs'), nextComponent(state, 'starch'), nextComponent(state, 'vegetable')],
      12,
    ),
    createAssembledMeal(
      `breakfast-${dayIndex + 1}-whey`,
      [componentById(state, 'whey'), nextComponent(state, 'starch'), componentById(state, 'fruit')],
      8,
    ),
  ].filter(Boolean)

  const mealDefinition = chooseDifferentMeal(options, previousMeal, state.rotation.breakfast)
  state.rotation.breakfast += 1

  return createMealSlot(dayIndex, 'breakfast', mealDefinition)
}

function nextLunchOrDinner(
  dayIndex: number,
  mealType: MealType,
  state: ReturnType<typeof createGeneratorState>,
  previousMeal: MealSlot | null,
  days: DraftDayPlan[],
  frequencyRules: FrequencyRule[],
): MealSlot {
  const shouldUseDish = state.rotation.dish % 4 === 0
  state.rotation.dish += 1

  if (shouldUseDish) {
    const dish = nextCompatibleDish(state, mealType, previousMeal, days, frequencyRules)

    if (dish) {
      return createMealSlot(dayIndex, mealType, cloneDish(dish))
    }
  }

  const mealDefinition = createVariedAssembledMeal(
    dayIndex,
    mealType,
    state,
    previousMeal,
    days,
    frequencyRules,
  )

  return createMealSlot(dayIndex, mealType, mealDefinition)
}

function createVariedAssembledMeal(
  dayIndex: number,
  mealType: MealType,
  state: ReturnType<typeof createGeneratorState>,
  previousMeal: MealSlot | null,
  days: DraftDayPlan[],
  frequencyRules: FrequencyRule[],
): AssembledMeal {
  let protein = nextProtein(days, state, frequencyRules)
  const previousProteinId = previousMeal ? mainProteinId(previousMeal.mealDefinition) : null

  if (previousProteinId && protein.id === previousProteinId) {
    protein = nextProtein(days, state, frequencyRules)
  }

  return createAssembledMeal(
    `generated-${dayIndex + 1}-${mealType}-${protein.id}`,
    [protein, nextComponent(state, 'starch'), nextComponent(state, 'vegetable')],
    16,
  )
}

function generateActivities(days: DraftDayPlan[], activities: Activity[]): void {
  for (const [index, day] of days.entries()) {
    day.activity =
      activities.find((activity) => activity.id === activityPlan[index]) ??
      requireArrayValue(activities, index % activities.length, 'activity')
  }
}

function mealSequence(): MealSequenceItem[] {
  return weekdays.flatMap((_, dayIndex) =>
    mealTypes.map((mealType) => ({
      dayIndex,
      mealType,
    })),
  )
}

function wouldRepeatTarget(
  days: DraftDayPlan[],
  placement: MealSequenceItem,
  target: FrequencyRuleTarget,
): boolean {
  return adjacentMeals(days, placement).some((meal) =>
    mealMatchesFrequencyTarget(meal.mealDefinition, target),
  )
}

function adjacentMeals(days: DraftDayPlan[], placement: MealSequenceItem): MealSlot[] {
  const sequence = mealSequence()
  const currentIndex = sequence.findIndex(
    (item) => item.dayIndex === placement.dayIndex && item.mealType === placement.mealType,
  )
  const adjacentIndexes = [currentIndex - 1, currentIndex + 1]

  return adjacentIndexes
    .map((index) => sequence[index])
    .map((item) => {
      if (!item) {
        return null
      }

      const day = days[item.dayIndex]

      return day ? day[item.mealType] : null
    })
    .filter((meal): meal is MealSlot => Boolean(meal))
}

function previousFilledMeal(
  days: DraftDayPlan[],
  dayIndex: number,
  mealType: MealType,
): MealSlot | null {
  const sequence = mealSequence()
  const currentIndex = sequence.findIndex(
    (item) => item.dayIndex === dayIndex && item.mealType === mealType,
  )

  for (let index = currentIndex - 1; index >= 0; index -= 1) {
    const previous = sequence[index]

    if (!previous) {
      continue
    }

    const day = days[previous.dayIndex]
    const meal = day ? day[previous.mealType] : null

    if (meal) {
      return meal
    }
  }

  return null
}

function componentsForForcedComponent(
  component: MealComponent,
  state: ReturnType<typeof createGeneratorState>,
): MealComponent[] {
  if (component.componentType === 'protein') {
    return [component, nextComponent(state, 'starch'), nextComponent(state, 'vegetable')]
  }

  if (component.componentType === 'starch') {
    return [nextComponent(state, 'protein'), component, nextComponent(state, 'vegetable')]
  }

  if (component.componentType === 'vegetable') {
    return [nextComponent(state, 'protein'), nextComponent(state, 'starch'), component]
  }

  return [
    nextComponent(state, 'protein'),
    nextComponent(state, 'starch'),
    nextComponent(state, 'vegetable'),
    component,
  ]
}

function nextComponent(
  state: ReturnType<typeof createGeneratorState>,
  componentType: ComponentType,
): MealComponent {
  const items = state.componentsByType[componentType]
  const index = state.rotation[componentType] % items.length
  state.rotation[componentType] += 1

  return requireArrayValue(items, index, componentType)
}

function nextProtein(
  days: DraftDayPlan[],
  state: ReturnType<typeof createGeneratorState>,
  frequencyRules: FrequencyRule[],
): MealComponent {
  const proteins = state.componentsByType.protein

  for (let offset = 0; offset < proteins.length; offset += 1) {
    const index = (state.rotation.protein + offset) % proteins.length
    const protein = requireArrayValue(proteins, index, 'protein')

    if (!wouldExceedFrequency(days, frequencyRules, createAssembledMeal('candidate', [protein], 0))) {
      state.rotation.protein += offset + 1
      return protein
    }
  }

  return nextComponent(state, 'protein')
}

function componentById(state: ReturnType<typeof createGeneratorState>, componentId: string): MealComponent {
  const component = Object.values(state.componentsByType)
    .flat()
    .find((item) => item.id === componentId)

  return component ?? nextComponent(state, 'optional')
}

function nextCompatibleDish(
  state: ReturnType<typeof createGeneratorState>,
  mealType: MealType,
  previousMeal: MealSlot | null,
  days: DraftDayPlan[],
  frequencyRules: FrequencyRule[],
): CompositeDish | null {
  const dishes = dishesForMealType(state.dishes, mealType)
  const previousKey = previousMeal ? mealIdentity(previousMeal.mealDefinition) : null

  if (dishes.length === 0) {
    return null
  }

  for (let offset = 0; offset < dishes.length; offset += 1) {
    const dish = requireArrayValue(
      dishes,
      (state.rotation.dish + offset) % dishes.length,
      'compatible dish',
    )

    if (
      mealIdentity(dish) !== previousKey &&
      !wouldExceedFrequency(days, frequencyRules, dish)
    ) {
      state.rotation.dish += offset + 1
      return dish
    }
  }

  return requireArrayValue(dishes, 0, 'compatible dish')
}

function nextPleasureDish(
  state: ReturnType<typeof createGeneratorState>,
  mealType: MealType,
): CompositeDish | null {
  const dishes = dishesForMealType(state.dishes, mealType).filter((dish) => pleasureDishIds.has(dish.id))

  if (dishes.length === 0) {
    return null
  }

  const dish = requireArrayValue(
    dishes,
    state.rotation.pleasureDish % dishes.length,
    'pleasure dish',
  )
  state.rotation.pleasureDish += 1

  return dish
}

function dishesForMealType(dishes: CompositeDish[], mealType: MealType): CompositeDish[] {
  return dishes.filter((dish) => {
    if (mealType === 'breakfast') {
      return dish.suitableForBreakfast
    }

    if (mealType === 'lunch') {
      return dish.suitableForLunch
    }

    return dish.suitableForDinner
  })
}

function chooseDifferentMeal(
  options: MealDefinition[],
  previousMeal: MealSlot | null,
  startIndex: number,
): MealDefinition {
  const previousKey = previousMeal ? mealIdentity(previousMeal.mealDefinition) : null

  if (options.length === 0) {
    throw new Error('Missing meal options')
  }

  for (let offset = 0; offset < options.length; offset += 1) {
    const option = requireArrayValue(
      options,
      (startIndex + offset) % options.length,
      'meal option',
    )

    if (mealIdentity(option) !== previousKey) {
      return option
    }
  }

  return requireArrayValue(options, 0, 'meal option')
}

function countFrequencyTarget(days: DraftDayPlan[], target: FrequencyRuleTarget): number {
  return mealSequence().filter(({ dayIndex, mealType }) => {
    const day = days[dayIndex]
    const slot = day ? day[mealType] : null

    return slot ? mealMatchesFrequencyTarget(slot.mealDefinition, target) : false
  }).length
}

function mealMatchesFrequencyTarget(mealDefinition: MealDefinition, target: FrequencyRuleTarget): boolean {
  if (target.kind === 'component') {
    if (mealDefinition.kind === 'assembled') {
      return mealDefinition.components.some((component) => component.id === target.componentId)
    }

    return normalizedText(mealDefinition.id).includes(normalizedText(target.componentId))
  }

  if (target.kind === 'dish') {
    return mealDefinition.kind === 'composite' && mealDefinition.id === target.dishId
  }

  return mealDefinition.kind === 'composite' && pleasureDishIds.has(mealDefinition.id)
}

function wouldExceedFrequency(
  days: DraftDayPlan[],
  frequencyRules: FrequencyRule[],
  mealDefinition: MealDefinition,
): boolean {
  return frequencyRules.some((rule) => {
    const targetCount = Math.max(0, Math.round(rule.targetCountPerWeek))

    if (!mealMatchesFrequencyTarget(mealDefinition, rule.target)) {
      return false
    }

    return countFrequencyTarget(days, rule.target) >= targetCount
  })
}

function createAssembledMeal(
  id: string,
  components: MealComponent[],
  preparationTimeMinutes: number,
): AssembledMeal {
  return {
    id,
    kind: 'assembled',
    components: components.map((component) => ({ ...component })),
    preparationTimeMinutes,
  }
}

function createMealSlot(dayIndex: number, mealType: MealType, mealDefinition: MealDefinition): MealSlot {
  return {
    id: `day-${dayIndex + 1}-${mealType}`,
    mealType,
    mealDefinition,
    ...totalsForMeal(mealDefinition),
  }
}

function totalsForMeal(
  mealDefinition: MealDefinition,
): Pick<MealSlot, 'estimatedCalories' | 'estimatedProteinGrams' | 'preparationTimeMinutes'> {
  if (mealDefinition.kind === 'composite') {
    return {
      estimatedCalories: mealDefinition.estimatedCalories,
      estimatedProteinGrams: mealDefinition.estimatedProteinGrams,
      preparationTimeMinutes: mealDefinition.preparationTimeMinutes,
    }
  }

  return {
    estimatedCalories: mealDefinition.components.reduce(
      (total, component) => total + component.estimatedCalories,
      0,
    ),
    estimatedProteinGrams: mealDefinition.components.reduce(
      (total, component) => total + component.estimatedProteinGrams,
      0,
    ),
    preparationTimeMinutes: mealDefinition.preparationTimeMinutes,
  }
}

function cloneDish(dish: CompositeDish): CompositeDish {
  return { ...dish }
}

function mainProteinId(mealDefinition: MealDefinition): string | null {
  if (mealDefinition.kind === 'composite') {
    return mealDefinition.id
  }

  return mealDefinition.components.find((component) => component.componentType === 'protein')?.id ?? null
}

function mealIdentity(mealDefinition: MealDefinition): string {
  if (mealDefinition.kind === 'composite') {
    return `dish:${mealDefinition.id}`
  }

  return `assembled:${mealDefinition.components.map((component) => component.id).join('+')}`
}

function normalizedText(value: string): string {
  return value.toLocaleLowerCase('fr-FR')
}

function slotKey(dayIndex: number, mealType: MealType): string {
  return `${dayIndex}:${mealType}`
}

function requireSlot(slot: MealSlot | null, dayId: string, mealType: MealType): MealSlot {
  if (!slot) {
    throw new Error(`Missing generated meal slot: ${dayId} ${mealType}`)
  }

  return slot
}

function requireArrayValue<T>(items: readonly T[], index: number, label: string): T {
  const item = items[index]

  if (!item) {
    throw new Error(`Missing ${label} at index ${index}`)
  }

  return item
}
