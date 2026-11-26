# Angular frontend notes

- Angular 22 standalone components, signals (`input()`, `output()`, `signal`,
  `inject()`), new control flow (`@if`, `@for`). Templates are mostly inline.
- Structure: `src/app/core` (api client, auth, native/Capacitor bridge), `shared`
  (dialog, household reviews, build version, picture reduction before upload, and
  `shared/ui`: Material building blocks of ADR 0016 — icon registry, list page,
  editor window frame and `openEditor`, stepper, breakpoints), `shell` (Material side menu with one
  foldable group per domain; on the phone a panel opened from the top bar, no tab
  bar), `login`, and one folder per module: `food` (recipes, foods, products and prices,
  stores, tickets), `sports` (today, history, progress, one page per element —
  routines, exercises, challenges, goals, templates — with their windows, and the
  routine page `sports/routine/:id` with its passes; rules in `sports/routine-format.ts`),
  `tasks` (today, all tasks with search, routines, projects, one page per todo `tasks/todo/:id`, routine
  `tasks/routine/:id` and project `tasks/project/:id`, their windows; rules in `tasks/task-format.ts` and `tasks/routine-rules.ts`),
  `health` (private health: today, supplements, symptoms, analyses with markers and panels, body
  measures, documents with encrypted files, appointments, one page per supplement, marker, analysis and document; images through
  `health/health-image.component.ts`; rules in `health/supplement-rules.ts`,
  `health/symptom-rules.ts`, `health/marker-rules.ts`, `health/document-rules.ts`),
  `budget` (private budget, ADR 0020: month, operations with OFX import and classification, planned lines, one page per line
  `budget/line/:id`, rules, evolution; rules in `budget/budget-rules.ts`),
  `household` (settings: profiles, versions, account), and `today` (the day at a glance, `/today`, the home page, ADR 0021:
  what is left to tick today, then the night and energy; it reads and ticks through the Tasks, Sport and Health endpoints
  with the API client, without importing their modules; rules in `today/today-items.ts`). Garmin (ADR 0019):
  `sports/session/:id`, `sports/vo2max`, `sports/garmin` (connection, MFA code, import token) and `health/sleep`;
  charts through `shared/ui/line-chart.component.ts` (geometry in `shared/ui/chart-geometry.ts`).
- A module folder holds its models, its API service (all HTTP calls), pure rules in
  plain `.ts` files with a `.spec.ts` (e.g. `food/foods/food-form.ts`,
  `food/recipes/recipe-nutrition.ts`), pages with their `.html`/`.css`, and lazy routes
  (`food/food.routes.ts`). `module-boundaries.spec.ts` fails when a module imports
  another module; sub-features of one module (`food/recipes`, `food/foods`) may share
  `food/shared`.
- Screens follow the validated mockups: photos everywhere (each module's image component, e.g.
  `food/shared/food-image.component.ts`, keeps its own placeholder and loads the authenticated
  image as a blob through `authenticatedImageUrl()` of `shared/ui/authenticated-image.ts`), a filter bar on top of every list page,
  prices secondary and read-only on everyday screens.
- UI kit (ADR 0016), the norm for every screen, new or changed: Angular Material 3 with the
  LifeOS theme (`src/theme.scss`) and the LifeOS building blocks.
  - Icons: Material Symbols as SVG (`<mat-icon svgIcon="name" />`); add the icon to
    `shared/ui/icons.ts` first (check it exists in `@material-symbols/svg-400/rounded`).
  - Fields: `mat-form-field appearance="outline"` with `matInput` or `mat-select`,
    `mat-checkbox`, `mat-button-toggle-group`; buttons `mat-flat-button` (main),
    `mat-stroked-button`, `mat-button`, `mat-icon-button`. A file picker is a hidden native
    `type="file"` input opened by a Material button.
  - Layout classes `lo-*` of `src/lifeos-ui.css`: page (`lo-page`, `lo-page-head`), list rows
    with a picture or icon on the left (`lo-list`, `lo-row`, `lo-row-main`), `lo-panel` for the
    sections of a detail page, `lo-table` for grids, `lo-stat`, `lo-tag` (`is-ok`, `is-warn`,
    `is-off`, `is-done`), `lo-chip` / `lo-filter-chip`, `lo-notice`, `lo-error`, `lo-status`,
    `lo-desktop-only` / `lo-phone-only`, `lo-bar-title` on a page `h1` the phone top bar already shows. Library pages use `app-list-page`; on the phone, adding is always the floating `lo-fab`.
  - Colours only from theme variables (`var(--mat-sys-*)`): no hex value, no `--lifeos-*`
    token (they are gone). The theme follows the system light/dark setting (each variable is
    a `light-dark()` value), so a screen must read well in both schemes.
  - Windows: `openEditor(dialog, Component, data)` + `app-editor-frame` (full screen on the
    phone, dangerous action bottom left with a second-click confirmation; without `formId`
    the window only has "Fermer"). Any other question before an action (delete, archive,
    leave a modified page) goes through `ConfirmService.ask()` of `shared/ui/confirm.ts`, a
    small Material window; never `window.confirm`/`alert`. Feedback after an action: `MatSnackBar`.
  - `src/app/ui-kit.spec.ts` fails on native selects/inputs/text areas, PrimeIcons, old
    tokens, fixed colours or the former shared classes. Don't add another UI library.
- Dialogs keep the dirty-draft guard of `dialog-guard.ts` (through `app-editor-frame`):
  closing a modified draft asks first, a pending request blocks closing, errors stay visible.
- Accessibility matters: whole-card links must be keyboard-reachable, keep
  `aria-*` attributes and visible labels. UI text is French.
- Data comes only from the LifeOS API (`core/api/lifeos-api.service.ts`) with the
  API's own access token (`core/auth`: OAuth code + PKCE against the API, ADR 0011;
  no Supabase). No demo/fallback data:
  an empty library stays empty.
- Hash-based routing; post-login redirects accept known internal routes only. The site is an
  installable web app (`public/manifest.webmanifest`, icons rendered from `favicon.svg`);
  `core/navigation/last-route*.ts` reopens the installed app (or the iPhone app) on the page left
  last, under 12 hours old, when it starts on its start address.

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
