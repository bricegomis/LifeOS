import { activities, compositeDishes, mealComponents } from '@/data/mockLibrary'
import type {
  Activity,
  AssembledMeal,
  CompositeDish,
  DayPlan,
  MealComponent,
  MealDefinition,
  MealSlot,
  MealType,
  WeekPlan,
} from '@/types'

const dayLabels = [
  ['Lundi', '6 juil.'],
  ['Mardi', '7 juil.'],
  ['Mercredi', '8 juil.'],
  ['Jeudi', '9 juil.'],
  ['Vendredi', '10 juil.'],
  ['Samedi', '11 juil.'],
  ['Dimanche', '12 juil.'],
] as const

const componentById = new Map(mealComponents.map((component) => [component.id, component]))
const dishById = new Map(compositeDishes.map((dish) => [dish.id, dish]))
const activityById = new Map(activities.map((activity) => [activity.id, activity]))

function requireMapValue<T>(map: Map<string, T>, id: string, label: string): T {
  const item = map.get(id)

  if (!item) {
    throw new Error(`Missing ${label}: ${id}`)
  }

  return item
}

function requireArrayValue<T>(items: readonly T[], index: number, label: string): T {
  const item = items[index]

  if (!item) {
    throw new Error(`Missing ${label} at index ${index}`)
  }

  return item
}

function component(id: string): MealComponent {
  return requireMapValue(componentById, id, 'meal component')
}

function dish(id: string): CompositeDish {
  return requireMapValue(dishById, id, 'composite dish')
}

function activity(id: string): Activity {
  return requireMapValue(activityById, id, 'activity')
}

export function totalsForMeal(
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
      (total, item) => total + item.estimatedCalories,
      0,
    ),
    estimatedProteinGrams: mealDefinition.components.reduce(
      (total, item) => total + item.estimatedProteinGrams,
      0,
    ),
    preparationTimeMinutes: mealDefinition.preparationTimeMinutes,
  }
}

export function createAssembledMeal(
  id: string,
  componentIds: string[],
  preparationTimeMinutes: number,
): AssembledMeal {
  return {
    id,
    kind: 'assembled',
    components: componentIds.map(component),
    preparationTimeMinutes,
  }
}

export function createMealSlot(
  id: string,
  mealType: MealType,
  mealDefinition: MealDefinition,
): MealSlot {
  return {
    id,
    mealType,
    mealDefinition,
    ...totalsForMeal(mealDefinition),
  }
}

function day(
  index: number,
  meals: [MealDefinition, MealDefinition, MealDefinition],
  activityId: string,
): DayPlan {
  const [dateLabel, shortDateLabel] = requireArrayValue(dayLabels, index, 'day label')

  return {
    id: `day-${index + 1}`,
    dateLabel,
    shortDateLabel,
    breakfast: createMealSlot(`day-${index + 1}-breakfast`, 'breakfast', meals[0]),
    lunch: createMealSlot(`day-${index + 1}-lunch`, 'lunch', meals[1]),
    dinner: createMealSlot(`day-${index + 1}-dinner`, 'dinner', meals[2]),
    activity: activity(activityId),
  }
}

const assembledMeals = [
  createAssembledMeal('breakfast-eggs-potatoes-zucchini', ['eggs', 'potatoes', 'zucchini'], 12),
  createAssembledMeal('lunch-steak-potatoes-broccoli', ['steak', 'potatoes', 'broccoli'], 18),
  createAssembledMeal('breakfast-whey-millet-carrots', ['whey', 'millet', 'carrots'], 10),
  createAssembledMeal('lunch-sardines-rice-mixed', ['sardines', 'rice', 'mixed-vegetables'], 12),
  createAssembledMeal('breakfast-eggs-quinoa-broccoli', ['eggs', 'quinoa', 'broccoli'], 14),
  createAssembledMeal(
    'dinner-tempeh-buckwheat-zucchini',
    ['tempeh', 'buckwheat-couscous', 'zucchini'],
    20,
  ),
  createAssembledMeal('breakfast-whey-quinoa-carrots', ['whey', 'quinoa', 'carrots'], 8),
  createAssembledMeal('lunch-steak-millet-mixed', ['steak', 'millet', 'mixed-vegetables'], 16),
  createAssembledMeal('breakfast-eggs-rice-broccoli', ['eggs', 'rice', 'broccoli'], 15),
  createAssembledMeal('lunch-sardines-potatoes-zucchini', ['sardines', 'potatoes', 'zucchini'], 14),
  createAssembledMeal('dinner-tempeh-quinoa-broccoli', ['tempeh', 'quinoa', 'broccoli'], 22),
  createAssembledMeal(
    'breakfast-whey-buckwheat-carrots',
    ['whey', 'buckwheat-couscous', 'carrots'],
    9,
  ),
  createAssembledMeal(
    'lunch-steak-buckwheat-pasta-mixed',
    ['steak', 'buckwheat-pasta', 'mixed-vegetables'],
    18,
  ),
  createAssembledMeal('breakfast-eggs-millet-zucchini', ['eggs', 'millet', 'zucchini'], 13),
  createAssembledMeal('lunch-sardines-quinoa-carrots', ['sardines', 'quinoa', 'carrots'], 15),
]

export function createDemoWeekPlan(): WeekPlan {
  return {
    id: 'week-2026-07-06',
    startDate: '2026-07-06',
    status: 'Generated',
    days: [
      day(
        0,
        [
          requireArrayValue(assembledMeals, 0, 'assembled meal'),
          requireArrayValue(assembledMeals, 1, 'assembled meal'),
          dish('tempeh-curry'),
        ],
        'walk',
      ),
      day(
        1,
        [
          dish('protein-pancakes'),
          requireArrayValue(assembledMeals, 3, 'assembled meal'),
          dish('lentil-rice-dhal'),
        ],
        'running',
      ),
      day(
        2,
        [
          requireArrayValue(assembledMeals, 4, 'assembled meal'),
          dish('homemade-chili'),
          requireArrayValue(assembledMeals, 5, 'assembled meal'),
        ],
        'mobility',
      ),
      day(
        3,
        [
          requireArrayValue(assembledMeals, 6, 'assembled meal'),
          requireArrayValue(assembledMeals, 7, 'assembled meal'),
          dish('buckwheat-cheese-pasta'),
        ],
        'strength',
      ),
      day(
        4,
        [
          requireArrayValue(assembledMeals, 8, 'assembled meal'),
          requireArrayValue(assembledMeals, 9, 'assembled meal'),
          dish('roast-chicken-vegetables'),
        ],
        'bike',
      ),
      day(
        5,
        [
          dish('protein-pancakes'),
          requireArrayValue(assembledMeals, 10, 'assembled meal'),
          dish('homemade-chili'),
        ],
        'long-running',
      ),
      day(
        6,
        [
          requireArrayValue(assembledMeals, 12, 'assembled meal'),
          requireArrayValue(assembledMeals, 13, 'assembled meal'),
          dish('lentil-rice-dhal'),
        ],
        'family',
      ),
    ],
  }
}

export function generateDemoWeekPlan(rotation: number): WeekPlan {
  const dishRotation = rotation % compositeDishes.length
  const activityRotation = rotation % activities.length
  const rotatedDishes = [...compositeDishes.slice(dishRotation), ...compositeDishes.slice(0, dishRotation)]
  const rotatedActivities = [
    ...activities.slice(activityRotation),
    ...activities.slice(0, activityRotation),
  ]
  const dinnerDishes = rotatedDishes.filter((item) => item.suitableForDinner)
  const breakfastDish = rotatedDishes.find((item) => item.suitableForBreakfast)
  const lunchDish = rotatedDishes.find((item) => item.suitableForLunch)

  return {
    ...createDemoWeekPlan(),
    id: `week-2026-07-06-generated-${rotation}`,
    days: createDemoWeekPlan().days.map((existingDay, index) => {
      const breakfast =
        index % 3 === 0 && breakfastDish
          ? breakfastDish
          : requireArrayValue(assembledMeals, (index + rotation) % assembledMeals.length, 'breakfast')
      const lunch =
        index % 2 === 0 || !lunchDish
          ? requireArrayValue(assembledMeals, (index + rotation + 3) % assembledMeals.length, 'lunch')
          : lunchDish
      const dinner = requireArrayValue(
        dinnerDishes,
        index % dinnerDishes.length,
        'dinner composite dish',
      )
      const rotatedActivity = requireArrayValue(
        rotatedActivities,
        index % rotatedActivities.length,
        'activity',
      )

      return day(index, [breakfast, lunch, dinner], rotatedActivity.id)
    }),
  }
}
