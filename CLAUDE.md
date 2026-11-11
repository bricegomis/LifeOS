@AGENTS.md

# Claude Code notes for LifeOS

LifeOS is a personal, household-scoped **life planner**: food, sport, tasks and
health, tied together by a week planner (the product's core, to be rebuilt). Today
only food and sport templates exist. The user writes in French; product docs and UI strings are French,
code, identifiers and code comments are English.

## Sources of truth (read before changing behaviour)
- Where we are going: `docs/00-vision.md`, ADR 0009 (modular monolith, planner core,
  proposals, external AI agent, per-person health privacy) and `docs/01-roadmap.md`
  (six steps; each starts with a scoping session, then a clickable mockup the user
  validates before any backend or real UI code).
- Step 1 target: ADR 0010 (simplified food module: receipts become a stored photo,
  JSON import/OCR/purchases/household groups/cooking session removed, fibres added,
  admin area for products and prices, recipe cost per store, full data reset) and
  `docs/02-core-model.md`.
- What exists today: `docs/PROJECT.md` (kept current at every delivery), ADR 0006
  (foods/products/receipts), ADR 0007 (recipes, household reviews) and ADR 0008
  (first week planner removed). The code still follows these until each step 1 lot lands.
- `docs/semainier-retour-experience.md`: lessons from the first planner and the
  sport needs already expressed.
- Historical docs were deleted on 2026-11-11 (git history keeps them). If the docs
  and the code disagree, say so instead of guessing.

## Layout
- `src/frontend-angular` — Angular 22 SPA, also packaged for iPhone with Capacitor 7
  (`ios/`: SwiftUI widgets, App Intents, Live Activity). See its `CLAUDE.md`.
- `src/backend` — ASP.NET Core (.NET 10) Clean Architecture: Domain / Application /
  Infrastructure / Api, plus `tests/`. See its `CLAUDE.md`.
- `docs/` — product docs (French) and `docs/architecture/decisions/` ADRs.
- `supabase/` — legacy Supabase migrations; Supabase is auth only now, don't add tables.
- `docker-compose.yaml` — production compose (VPS/Coolify, images from GHCR, watchtower).
- `docker-compose.integration.yaml` — integration stack (`:integration` images, own
  database and volumes); see `docs/integration-environment.md`.

## Domain invariants that are easy to break
- Every persisted business row is filtered by `household_id` at the application layer
  (no Postgres RLS). New queries/commands must resolve and filter by the caller's
  household; cross-household references must be rejected (composite FKs exist).
- Food = generic culinary reference with manual, nullable nutrition (base, 4 macros,
  FODMAP, NOVA). Zero is a valid value; "À compléter" when any of the 7 fields is null.
  Product = commercial price reference linked to one food. Recipes and meal lines
  pick foods, never a product/brand implicitly.
- Receipts: raw source kept, lines `pending`/`validated`/`ignored`; import creates
  nothing. JSON lines validate one by one; photo receipts finalize atomically. Both
  are transactional and idempotent. No health/nutrition data imported from receipts.
- Prices: observed price = line total / quantity. Compare only on truly comparable
  bases (kg, L, confirmed pieces); package contents must be confirmed manually.
  No invented density, piece weight or drained weight.
- Quantities/nutrition from a recipe: `ingredient quantity × portions / yield`;
  unknown nutrition stays unknown and makes totals incomplete.
- Reviews: optional integer 0..10 and/or comment, per food or recipe and per profile
  or named household group. 0 ≠ "non noté". Strong FKs, no polymorphic target.
- There is no week planner for now (ADR 0008). When it comes back, planned meals keep
  snapshots: editing a recipe never rewrites planned weeks.
- Module boundaries (ADR 0009): a module never reads another module's tables or
  entities, only its published read contract; domain modules know neither each other
  nor Planning; cross-domain rules live in Planning as signals computed on read.
- The planner changes only through a direct user action or a user-accepted proposal.
  Planning is computed by an external agent; the API never calls an AI provider.
- Health data is owned by a profile, not shared with the household, never sent to the agent.
- Out of scope, don't reintroduce: stock, shopping list, planning computed inside the
  API, OFF/external enrichment, name-based matching, Vue, PrimeNG runtime.

## Verification
- Frontend (from `src/frontend-angular`): `npm run type-check`, `npm test`,
  `npm run lint`, `npm run build` (CI runs all four).
- Backend (from `src/backend`): `dotnet build` and `dotnet test`. Integration tests
  need a running Docker daemon (Testcontainers PostgreSQL); if Docker is unavailable,
  say so instead of claiming the tests pass.
- iOS (`scripts/test-ios-contract.sh`, `npm run cap:sync`) needs macOS/Xcode; on
  Linux, report it as not run. Unsigned builds never prove an iPhone install.
- Local checks are not a production verification.

## Git and delivery
- Commit messages: Conventional Commits with a scope, in English,
  e.g. `feat(foods): add optional food images`, `fix(reviews): ...`.
- Work on a branch and open PRs to `integration`. `main` gets one merge per roadmap
  step, at the end of the step (`docs/01-roadmap.md`). Pushes to `integration` publish the
  `:integration` images for the integration stack. Pushes to `main` publish Docker images
  (`ghcr.io/bricegomis/lifeos-{api,web}:latest`) that watchtower picks up, and deploy
  GitHub Pages. So merging to `main` is effectively a deploy: ask first.
- Roadmap step 1 may drop existing data (user decision, 2026-11-11): a destructive
  migration is allowed there, once the user confirms what is reset.
- Never run migrations against a shared/production database, reset user data, or
  touch the VPS. The user backs up PostgreSQL **and** the `lifeos_receipts` /
  `lifeos_food_images` volumes, then refreshes the containers themselves.
- When behaviour changes, update the directly affected docs (ADR status/extension,
  `docs/PROJECT.md`, `docs/02-core-model.md`, the relevant README). Replace stale
  text instead of stacking "superseded" notes; don't keep history in the docs.
