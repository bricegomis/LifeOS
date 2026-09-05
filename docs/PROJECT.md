# LifeOS — Project Memory

LifeOS is a personal planning app built to reduce daily decision fatigue around food and weekly activity. The current implementation is intentionally not a broad “all-of-life” dashboard. It is a narrow, practical tool focused on meals, planned eating, and the weekly rhythm that supports them.

## Why this project exists
The project addresses a recurring personal problem: deciding what to eat, what to prepare, and what physical activity to schedule each day is repetitive and cognitively expensive. The app aims to give the user a clear weekly view so that meals and activity can be planned once and then followed with less friction.

## Current scope
The active product scope is the V0 Weekly Planner:
- daily meal overview (`Today`)
- weekly planner view
- food library for components and dishes
- settings for context and planning rules
- generation of a full week from rules and constraints

This is not a generic personal life operating system. The focus remains on nutrition and planning, with room to evolve only when the core food-planning workflow is stable.

## Implemented features
The current codebase already includes the core of the weekly meal planner:

### Daily view
The `TodayView` resolves the current date against the current `WeekPlan` and shows:
- breakfast, lunch, and dinner cards
- estimated calories and protein totals for the day
- the scheduled activity
- work-location context (home / office / off) and bike commute status

### Weekly planner
The `WeeklyPlannerView` handles a 7-day week grid with:
- one row per day
- breakfast, lunch, dinner, and activity columns
- generation or regeneration of the week from configured rules
- week navigation by offset
- manual meal editing via a side drawer
- activity selection per day

### Library
The `LibraryView` exposes the local meal catalog:
- active meal components grouped by type (protein, starch, vegetable, optional)
- composite dishes
- physical activities
- search by name

### Settings and generation rules
The `SettingsView` lets the user configure:
- the reference week and alternation mode
- per-week override exceptions
- day-by-day work context and bike commute data
- fixed meal rules (`PlanningRule`)
- weekly frequency rules (`FrequencyRule`)

### Rule-driven generation
The generator in `src/frontend/src/data/weekGenerator.ts` builds a full `WeekPlan` from:
- active meal components and dishes
- fixed rules
- frequency rules
- week context and week mode

The default rule set already encodes a practical weekly rhythm for the current user, including recurring lunch / dinner preferences and enjoyment-focused dishes.

## Architecture
The frontend is centered on a Vue 3 / TypeScript app in `src/frontend`.

### Main folders
- `src/frontend/src/views` contains the UI pages.
- `src/frontend/src/stores` contains the Pinia state management layer.
- `src/frontend/src/data` contains the static library and generator logic.
- `src/frontend/src/services/supabase` contains the optional remote sync layer.
- `src/frontend/src/types.ts` defines the domain model.
- `supabase/migrations` contains the database schema used for authenticated user data.

### Representative domain model
Key types in `src/types.ts`:
- `MealComponent`: reusable ingredient with nutrition data and category
- `CompositeDish`: complete dish treated as a single unit
- `MealSlot`: a planned meal with type and definition
- `DayPlan`: breakfast, lunch, dinner, and activity for a day
- `WeekPlan`: a full week with 7 days and status
- `WeekContext`: alternating week config, overrides, and per-day context
- `PlanningRule`: fixed meal assignment by weekday + meal type
- `FrequencyRule`: target count per week for a component, dish, or category

## Data flow and planning logic
The planning engine follows a simple pattern:
1. resolve the current week mode (`kids` vs `solo`) from the reference configuration and overrides
2. create a 7-day empty week
3. apply fixed rules
4. apply frequency rules to fill remaining slots
5. complete remaining meals from available library entries
6. generate the activity plan for each day
7. persist the resulting `WeekPlan`

The `weekContext` store carries the user’s real-world context such as work location and alternating week mode. It influences how the week is interpreted and what the planner should prefer from the available meal and activity library.

## Persistence model
The app currently uses a dual persistence strategy:

### Local-first persistence
The stores persist their state in `window.localStorage`:
- `lifeos.weekPlan.v1`
- `lifeos.planningRules.v1`
- `lifeos.context.v1`

This keeps the app usable offline and prevents the project from depending on a backend for the core workflow.

### Optional Supabase sync
The app also includes a Supabase integration for authentication and remote persistence when environment variables are configured:
- `VITE_SUPABASE_URL`
- `VITE_SUPABASE_ANON_KEY`

The repository layer (`lifeosRepository.ts`) syncs:
- user settings
- planning rules
- week context config
- week mode overrides
- week plans

Authentication is implemented through magic-link email sign-in and an auth session stored in browser storage.

## Current state of the project
At the code level, the project is already working as a pragmatic V0 product:
- the app is structured around the weekly meal planning domain
- library, settings, and week generation are implemented
- a daily “today” overview is present
- local persistence is in place
- Supabase support exists but is optional and configured by environment
- no broad generic LifeOS features are implemented yet

The current state is intentionally narrow and product-focused on nutrition and planning rather than a full personal operating system.

## Known limitations
- The app is still centered on a single user / personal workflow.
- There is no broad backend domain model beyond the current Supabase tables.
- The meal library is a curated local catalog, not a general recipe database.
- The project currently has no dedicated automated test suite; the main quality gate is the build and lint flow.
- The app is still at the V0 planning stage; advanced nutrition tracking, shopping lists, and broader life-management features are out of scope.

## Current State
The repository currently reflects a functioning V0 weekly planner for food-oriented planning. The core product is stable enough to be used as a focused nutrition planning app, with the main architecture centered on local-first state plus optional Supabase sync.

## Next Steps
The logical next step is not a broad product expansion. It is to keep the system focused and reliable by:
1. validating the current weekly generation flow against real-world usage
2. strengthening the meal library and rule quality over time
3. refining the planner UX around meal editing and week adjustments
4. adding only the missing nutrition-tracking features that are clearly part of the food-planning workflow
5. keeping the docs aligned with code as the product evolves
