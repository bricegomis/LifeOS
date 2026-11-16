# Angular frontend notes

- Angular 22 standalone components, signals (`input()`, `output()`, `signal`,
  `inject()`), new control flow (`@if`, `@for`). Templates are mostly inline.
- Structure: `src/app/core` (api client, auth, native/Capacitor bridge), `shared`
  (dialog, household reviews, build version, picture reduction before upload), `shell` (left burger menu with one block
  per domain, phone tab bar), `login`, and one folder per module: `food` (recipes,
  foods, products and prices, stores, tickets), `sports` (today, history, progress,
  settings, and the routine page `sports/routine/:id`; a sub-navigation on the phone,
  menu entries on the desktop; routine screens are their own components embedded in
  those pages, with rules in `sports/routine-format.ts`), `household` (settings:
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
- UI kit: Angular Material 3 with the LifeOS theme, Material Symbols icons and the
  screen conventions of ADR 0016 (list items, add button, dialogs full screen on the
  phone, dangerous action bottom left). New and migrated screens use Material
  components and the LifeOS components built on them; don't add another UI library.
  Screens not migrated yet keep native controls, the shared blocks of `src/styles.css`
  and PrimeIcons (`pi pi-...`) until their turn.
- Dialogs: migrated screens use `MatDialog` and keep the dirty-draft guard of
  `dialog-guard.ts`. Screens not migrated yet reuse `shared/dialog/lifeos-dialog.component.ts` (native modal
  `<dialog>`, focus return, dirty-draft confirmation via `dialog-guard.ts`). Detail
  and review editors open as centered popups; closing a modified draft asks first,
  a pending request blocks closing, errors stay visible.
- Accessibility matters: whole-card links must be keyboard-reachable, keep
  `aria-*` attributes and visible labels. UI text is French.
- Data comes only from the LifeOS API (`core/api/lifeos-api.service.ts`) with the
  API's own access token (`core/auth`: OAuth code + PKCE against the API, ADR 0011;
  no Supabase). No demo/fallback data:
  an empty library stays empty.
- Hash-based routing; post-login redirects accept known internal routes only.

## Tests
- `npm test` runs `node --test "src/**/*.spec.ts"` (Node's runner with native TS
  type stripping, no Angular TestBed). Put testable logic in plain `.ts` modules
  and import them with the `.ts` extension (also between pure modules), using `node:test` and `node:assert/strict`.
- `npm run type-check` / `npm run build` generate `public/config.js` from
  `.env.local` (copy `.env.example`). `LIFEOS_API_URL` has no `/api` suffix.
  Frontend config holds only `LIFEOS_API_URL`; never put a secret in it.

## iOS (Capacitor)
- `ios/` holds the Xcode project and widget extension; see `docs/ios-v1.md`. The
  cooking session was removed from the web app (ADR 0010); its Swift code is
  unreachable and stays until roadmap step 6. The web only clears legacy state.
- No JWT is shared with the widget extension; don't disable ATS.
