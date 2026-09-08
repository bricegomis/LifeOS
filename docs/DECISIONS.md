# LifeOS — Architectural and Product Decisions

This document captures the most important product and technical decisions that shape the current project. It is intentionally not a log of every implementation choice; it focuses on durable decisions that explain why the app currently looks and behaves the way it does.

## 2026 — Keep LifeOS focused on food and weekly planning
Context:
The project started as a general personal planning app idea and historically evolved from meal tracking toward a broader life dashboard. The current codebase deliberately narrows back to the nutrition-focused workflow.

Decision:
LifeOS remains a V0 weekly planner centered on meals, activity planning, and personal food organization.

Reasons:
- the application is more valuable when it solves one concrete problem well
- the meal-planning problem is clear and recurring
- the existing codebase already centers on weekly meal generation and context-aware planning

Consequences:
- the app does not expand into a generic life management system by default
- the architecture stays small and easy to evolve
- future features can be added later only if they directly support the food-planning workflow

## 2026 — Vue 3 + TypeScript + Pinia + PrimeVue
Context:
The project needs a fast, small UI with a clear state model and low operational overhead.

Decision:
The frontend uses Vue 3, TypeScript, Pinia, Vue Router, and PrimeVue.

Reasons:
- Vue provides a straightforward SPA model
- TypeScript helps keep the domain model explicit
- Pinia aligns well with a small but structured planner state model
- PrimeVue gives a quick, polished component system without adding a large bespoke design system

Consequences:
- the app stays maintainable and easy to evolve
- the domain model remains explicit in `src/types.ts`
- UI work stays close to the business domain rather than a large abstraction layer

## 2026 — Local-first persistence before backend dependency
Context:
The project originally had no backend and was designed as a practical personal app. The current architecture still reflects that mindset.

Decision:
The app stores key planner data in localStorage first, with optional Supabase sync for authenticated users.

Reasons:
- the main workflow should work without a backend
- a personal planner benefits from a simple local-first experience
- Supabase can be used later for synchronization across devices without forcing it on the core workflow

Consequences:
- the core app remains usable without external configuration
- Supabase is optional rather than mandatory
- local state and remote state must be normalized carefully to keep compatibility

## 2026 — Week-based planning as the primary unit
Context:
The project is built around the idea of reducing daily decision fatigue by planning once for the week.

Decision:
A `WeekPlan` is the primary object, and each day contains breakfast, lunch, dinner, and activity.

Reasons:
- the weekly rhythm is the user’s mental model
- repeated decisions are easier to manage when the week is generated as a whole
- the settings and rules naturally operate at the week / day level

Consequences:
- day-level and week-level contexts are treated as first-class concerns
- the generator flow focuses on planning a full 7-day cycle rather than isolated meals
- the UI is centered on “what is happening this week?” rather than event-by-event tracking

## 2026 — Rule-driven generation instead of rigid recipes
Context:
The app should help generate meal plans without forcing the user into a rigid, prescriptive system.

Decision:
The scheduler uses fixed rules and frequency rules to build a week rather than hard-coding a single meal matrix.

Reasons:
- the user has recurring preferences and exceptions
- the app should be semi-automatic, not a black box
- a rule model allows both structure and manual override

Consequences:
- `PlanningRule` handles “must happen this weekday / meal slot” logic
- `FrequencyRule` handles “this food should appear X times per week” logic
- the app can remain flexible while still reducing decision fatigue

## 2026 — Keep the data model narrow and practical
Context:
The repo historically considered broader life management and future features, but the current code remains a compact food-planning model.

Decision:
The core domain stays narrow: components, dishes, weekly plans, and week context.

Reasons:
- the system should remain easy to understand and maintain
- broad abstractions would not add value at this stage
- the project is personal and should evolve incrementally

Consequences:
- the domain model is explicit and readable rather than over-engineered
- future features can be added only when they clearly support the nutrition-planning use case

## 2026 — Optional Supabase auth and synchronization layer
Context:
The project already anticipates cross-device access but does not require a cloud backend for local use.

Decision:
Supabase is used for magic-link authentication and optional remote persistence of user settings and week data.

Reasons:
- it supports a simple personal multi-device workflow
- it remains optional and non-blocking for offline local use
- it fits the current app’s size and architecture

Consequences:
- localStorage remains the default storage model
- remote persistence is configured only when env vars are present
- future cross-device sync can evolve without rewriting the base app architecture

## 2026 — Grocery stores and articles as local, price-tracking building blocks
Context:
Meal planning benefits from knowing where groceries are bought and how their prices evolve, without turning the app into a full shopping or budgeting tool.

Decision:
Grocery stores (`GroceryStore`) and grocery articles (`GroceryItem`) are managed through dedicated CRUD views (`StoresView`, `ArticlesView`), each backed by its own local-first Pinia store. Each article keeps a reference unit (per kilogram, per liter, or per unit) and a price history (`GroceryPriceEntry`) of store + price + date observations.

Reasons:
- knowing "where" and "for how much" an article was bought is directly useful for the food-planning workflow, without requiring a shopping-list or budgeting feature
- keeping stores and articles as narrow, independent entities matches the existing CRUD pattern already used for stores
- price history as an append-only list per article is simple to reason about and does not require a backend

Consequences:
- `lifeos.groceryStores.v1` and `lifeos.groceryItems.v1` are added as new local-first `localStorage` keys, following the same schema-versioned persistence pattern as other stores
- these entities are not yet synced to Supabase; they remain local-only until a clear need for cross-device sync emerges
- shopping lists, budgeting, and automatic price analytics remain out of scope for now
- deleting a store does not cascade into existing price history entries; the UI tolerates orphaned `storeId` references and falls back to a "Magasin supprimé" label

## 2026 — Introduce a .NET backend API alongside the local-first frontend
Context:
The frontend has so far relied on localStorage plus optional Supabase sync, with no dedicated backend domain model. Some features (starting with grocery stores) call for server-side logic and a structured backend as the project grows.

Decision:
A new ASP.NET Core Web API (`src/backend`, `LifeOS.Api`) is introduced, organized into Domain, Application, Infrastructure, and Api layers pointing inward, as a foundation to grow towards Clean Architecture / DDD. The first implemented slice is a read-only `GET /api/stores` endpoint. The API authenticates requests using the same Supabase-issued JWTs already used by the frontend, instead of implementing a separate login flow.

Reasons:
- reusing Supabase Auth avoids duplicating identity management and keeps a single sign-in experience for the user
- a layered backend structure keeps business rules (Domain/Application) independent from framework and persistence details (Infrastructure/Api), making it easier to evolve incrementally
- starting with the smallest possible vertical slice (list stores) validates the wiring (auth, layering, DI) before adding write operations or a real database

Consequences:
- the backend does not replace the local-first frontend architecture; localStorage remains the default persistence for the core planning workflow
- stores are currently served from an in-memory, per-user seeded repository (`InMemoryStoreRepository`) rather than a real database; this is expected to change once the backend needs to persist writes
- future backend features should follow the same layering (Domain entities, Application use cases/ports, Infrastructure implementations, Api endpoint mapping) and reuse the existing Supabase JWT authentication

## 2026 — Expand the backend API to articles, meal library, planning rules, and week context; add Scalar for OpenAPI testing
Context:
The backend API started with a single read-only `GET /api/stores` slice. The frontend's domain model covers more entities (grocery articles, the shared meal library, planning/frequency rules, week context) that needed the same server-side treatment, and there was no interactive way to exercise the API without a separate tool.

Decision:
The backend now exposes, for each remaining bounded context, the same layering already used for stores (Domain aggregate mirroring the frontend model, Application query/command backed by a repository port, in-memory Infrastructure repository, Api endpoint mapping): full CRUD for grocery articles (`/api/articles`, including price history), read-only endpoints for the shared meal library (`/api/meal-components`, `/api/composite-dishes`, `/api/activities`), full CRUD for planning rules and frequency rules, and get/replace for the per-user week context (`/api/week-context`). `Scalar.AspNetCore` is added on top of the existing `Microsoft.AspNetCore.OpenApi` document to provide an interactive API reference/tester at `/scalar/v1` in development.

Reasons:
- following the same layering per bounded context keeps the backend consistent and easy to extend as more of the frontend domain is ported
- the meal library (components, dishes, activities) is shared, read-only seed data in the frontend, so it is exposed the same way rather than as per-owner CRUD
- Scalar gives a lightweight, native-OpenAPI-compatible UI to manually test authenticated endpoints without adding a heavier tool like Swashbuckle

Consequences:
- articles, planning rules, frequency rules, and week context are currently served from in-memory, per-owner repositories, not a real database; this is expected to change as the backend matures
- `WeekPlan` generation itself (the weekly planner, `src/frontend/src/data/weekGenerator.ts`) is not yet ported to the backend and still runs entirely in the frontend
- future backend work can keep using `/scalar/v1` to manually verify new endpoints against a real Supabase-issued JWT
