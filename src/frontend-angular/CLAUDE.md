# Angular frontend notes

- Angular 22 standalone components, signals (`input()`, `output()`, `signal`,
  `inject()`), new control flow (`@if`, `@for`). Templates are mostly inline.
- Structure: `src/app/core` (api client, auth, native/Capacitor bridge), `shared`
  (dialog, household reviews, build version), `shell` (left burger menu with one block
  per domain, phone tab bar), `login`, and one folder per module: `food` (recipes,
  foods, products and prices, stores, tickets), `sports`, `household` (settings:
  profiles, versions, account).
- A module folder holds its models, its API service (all HTTP calls), pure rules in
  plain `.ts` files with a `.spec.ts` (e.g. `food/foods/food-form.ts`,
  `food/recipes/recipe-nutrition.ts`), pages with their `.html`/`.css`, and lazy routes
  (`food/food.routes.ts`). `module-boundaries.spec.ts` fails when a module imports
  another module; sub-features of one module (`food/recipes`, `food/foods`) may share
  `food/shared`.
- Screens follow the validated step 1 mockup: photos everywhere (`food/shared/food-image.component.ts`
  loads authenticated images as blobs), a filter bar on top of every list page,
  prices secondary and read-only on everyday screens. Products, prices and stores
  are not in the phone tab bar.
- UI: native HTML controls; tokens and shared blocks (buttons, fields, cards, filter bar,
  chips, grid tables) in `src/styles.css`, page rules next to each page. Icons are
  PrimeIcons classes (`pi pi-...`). No PrimeNG/PrimeUI or other UI libraries.
- Dialogs: reuse `shared/dialog/lifeos-dialog.component.ts` (native modal
  `<dialog>`, focus return, dirty-draft confirmation via `dialog-guard.ts`). Detail
  and review editors open as centered popups; closing a modified draft asks first,
  a pending request blocks closing, errors stay visible.
- Accessibility matters: whole-card links must be keyboard-reachable, keep
  `aria-*` attributes and visible labels. UI text is French.
- Data comes only from the LifeOS API (`core/api/lifeos-api.service.ts`) with the
  Supabase access token; Supabase is used for auth only. No demo/fallback data:
  an empty library stays empty.
- Hash-based routing; post-login redirects accept known internal routes only.

## Tests
- `npm test` runs `node --test "src/**/*.spec.ts"` (Node's runner with native TS
  type stripping, no Angular TestBed). Put testable logic in plain `.ts` modules
  and import them with the `.ts` extension (also between pure modules), using `node:test` and `node:assert/strict`.
- `npm run type-check` / `npm run build` generate `public/config.js` from
  `.env.local` (copy `.env.example`). `VITE_LIFEOS_API_URL` has no `/api` suffix.
  Never put a Supabase service-role/secret key in frontend config.

## iOS (Capacitor)
- `ios/` holds the Xcode project and widget extension; see `docs/ios-v1.md`. The
  cooking session was removed from the web app (ADR 0010); its Swift code is
  unreachable and stays until roadmap step 6. The web only clears legacy state.
- No JWT is shared with the widget extension; don't disable ATS.
