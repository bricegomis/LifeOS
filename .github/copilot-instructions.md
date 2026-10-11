# LifeOS Copilot Instructions

LifeOS is a personal meal and sport planner, not a generic life dashboard.
The active stack is Angular 22 / TypeScript (`src/frontend-angular`) and
ASP.NET Core / EF Core / PostgreSQL (`src/backend`). Supabase handles auth/JWT
only. Native HTML controls, shared LifeOS dialogues and CSS tokens preserve
the visual identity; PrimeIcons supplies navigation icons.

## Current model
- **Food** (`FoodItem`, `foods`) is a generic culinary reference with manual,
  nullable nutrition, base, FODMAP and NOVA. Partial saves and zero macros are valid.
- **Product** (`Product`, `products`) is a commercial price reference with an
  explicit food link. Multiple products may belong to one food.
- Recipes and new meal lines select foods; meal snapshots remain unchanged.
- Receipts preserve raw JSON and ordered pending lines. Only individual manual
  validation creates an optional food/product, purchase and price in one transaction.
- No imported health fields, automatic name matching, external enrichment,
  stock or shopping-list functionality.
- Compare observed prices only on safe kg/L/confirmed comparable-piece bases.
  A package needs manually confirmed contents. Never choose a recipe product implicitly.
- Historical non-food products and stock tables remain preserved, not active catalogues.

## Development and verification
From `src/frontend-angular`: `npm ci`, `npm run dev`; configure
`VITE_LIFEOS_API_URL`, `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY` using
`.env.example`. Run `npm test`, `npm run type-check`, `npm run lint`, `npm run build`.
From `src/backend`: `dotnet build`, `dotnet test`; PostgreSQL tests use Testcontainers/Docker.

Respect API household scope, existing planning/sport/auth behavior and precise
quantities. Do not rewrite applied migrations or historical snapshots.
Use `docs/PROJECT.md`, `docs/02-core-model.md` and
`docs/architecture/decisions/0006-foods-products-receipts.md` as current product truth.
Keep affected docs current, keep changes surgical and avoid unnecessary dependencies.
Back up PostgreSQL before upgrading. Never deploy or reset user data without authorization.
