@AGENTS.md

# Claude Code notes for LifeOS

LifeOS is a personal, household-scoped **meal and sport planner**, not a generic
life dashboard. The user writes in French; product docs and UI strings are French,
code, identifiers and code comments are English.

## Sources of truth (read before changing behaviour)
- Current model: `docs/02-core-model.md`, ADR 0006 (foods/products/receipts, receipt
  photo OCR, food images) and ADR 0007 (recipes, meal lines, household reviews).
- `docs/PROJECT.md`: only the top section ("État actuel" + "Modèle actif") is current.
  Everything under "Mémoire historique" describes the removed
  Vue/local-first/generation era — never treat it as live.
- `docs/DECISIONS.md` and ADRs 0001–0005 are history; 0005 is superseded by 0006.
- Vue and Open Food Facts search are removed; if a stray mention remains anywhere,
  trust the ADRs and the code.

## Layout
- `src/frontend-angular` — Angular 22 SPA, also packaged for iPhone with Capacitor 7
  (`ios/`: SwiftUI widgets, App Intents, Live Activity). See its `CLAUDE.md`.
- `src/backend` — ASP.NET Core (.NET 10) Clean Architecture: Domain / Application /
  Infrastructure / Api, plus `tests/`. See its `CLAUDE.md`.
- `docs/` — product docs (French) and `docs/architecture/decisions/` ADRs.
- `supabase/` — legacy Supabase migrations; Supabase is auth only now, don't add tables.
- `docker-compose.yaml` — production compose (VPS/Coolify, images from GHCR, watchtower).

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
- Meal line quantities/nutrition: `ingredient quantity × planned portions / yield`;
  unknown nutrition stays unknown and makes totals incomplete.
- Reviews: optional integer 0..10 and/or comment, per food or recipe and per profile
  or named household group. 0 ≠ "non noté". Strong FKs, no polymorphic target.
- Planned meals keep their snapshots; editing a recipe never rewrites planned weeks.
- Out of scope, don't reintroduce: stock, shopping list, automatic week generation,
  rules/scenarios, OFF/external enrichment, name-based matching, Vue, PrimeNG runtime.

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
- Work on a branch and open a PR to `main`; pushes to `main` publish Docker images
  (`ghcr.io/bricegomis/lifeos-{api,web}:latest`) that watchtower picks up, and deploy
  GitHub Pages. So merging to `main` is effectively a deploy: ask first.
- Never run migrations against a shared/production database, reset user data, or
  touch the VPS. The user backs up PostgreSQL **and** the `lifeos_receipts` /
  `lifeos_food_images` volumes, then refreshes the containers themselves.
- When behaviour changes, update the directly affected docs (ADR status/extension,
  `docs/PROJECT.md` current section, `docs/02-core-model.md`, the relevant README).
