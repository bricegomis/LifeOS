# LifeOS Copilot Instructions

## What LifeOS is
LifeOS is a personal planning app focused on nutrition and weekly meal planning. The current product scope is a V0 weekly planner: decide what to eat this week, keep meals simple, and reduce daily decision fatigue.

## Current product focus
Do not expand the project into a generic life dashboard unless explicitly requested. The active scope is:
- meal planning
- meal tracking / daily “today” view
- food library and reusable components
- weekly scheduling logic
- planning rules and week context configuration

## Stack and architecture
The app is currently a Vue 3 + TypeScript single-page app using Vite.
- UI: Vue 3, PrimeVue
- State: Pinia
- Routing: Vue Router
- Persistence: localStorage for the main app state; optional Supabase sync for authenticated users
- Backend: Supabase only for auth and optional remote persistence

App code lives under `src/frontend`.
- `src/frontend/src/views`: pages (`TodayView`, `WeeklyPlannerView`, `SettingsView`, `LibraryView`, `LoginView`)
- `src/frontend/src/stores`: Pinia stores (`auth`, `weekContext`, `weekPlanner`, `planningRules`)
- `src/frontend/src/data`: local meal library and generated week logic
- `src/frontend/src/services/supabase`: auth and repository sync layer
- `src/frontend/src/types.ts`: shared domain types

## Run locally
From `src/frontend`:
- `npm install`
- `npm run dev`

Required environment variables for Supabase auth/sync:
- `VITE_SUPABASE_URL`
- `VITE_SUPABASE_ANON_KEY`

Use `src/frontend/.env.example` as the template.

## Validation commands
From `src/frontend`:
- `npm run build`
- `npm run lint`

There is no dedicated automated test suite in the current repository; build and lint are the main validation checks.

## Important conventions
- Keep the scope narrow and aligned with the meal-planning product.
- Prefer simple, maintainable solutions over generic abstractions.
- Preserve the current architecture before introducing new patterns.
- Respect the existing domain model: meal components, composite dishes, week context, planning rules, and generated week plans.
- Do not rewrite working code without a clear reason.
- When persisting data, keep compatibility with existing localStorage structures and validate unknown data carefully.

## Planning domain rules
The working model is intentionally pragmatic:
- `MealComponent` = reusable ingredient / building block
- `CompositeDish` = complete dish treated as one unit
- `WeekPlan` = the weekly schedule for 7 days
- `DayPlan` = one day with breakfast, lunch, dinner, and activity
- `WeekContext` = work context, alternating week mode, and per-day settings
- `PlanningRule` and `FrequencyRule` = the generation logic that drives the week

## Documentation expectations
Use the repo docs as the source of truth for product intent and current architecture.
- `docs/PROJECT.md` is the current product memory.
- `docs/DECISIONS.md` records important architecture and product decisions.
- Update these documents when a significant change affects product direction, architecture, or persistence.

## Keeping the docs current
After a substantial change, update the persistent documentation rather than relying on chat history. The documentation should reflect the current state of the codebase, not a historical log of every prior conversation.
