# Angular frontend notes

- Angular 22 standalone components, signals (`input()`, `output()`, `signal`,
  `inject()`), new control flow (`@if`, `@for`). Templates are mostly inline.
- Structure: `src/app/core` (api client + models, auth, native/Capacitor bridge),
  `pages/<feature>` (foods, recipes, sports, stores, tickets, settings, login;
  the planning and dashboard pages were removed by ADR 0008), `shared` (dialog,
  reviews, cooking), `shell`.
- New modules follow ADR 0009: `src/app/<module>/` with its own pages, API service,
  models and lazy routes; a module imports only `core` and `shared`, never another
  module. Existing `pages/<feature>` move in roadmap step 1 (`docs/01-roadmap.md`).
- UI: native HTML controls and LifeOS CSS tokens from `src/styles.css`; icons are
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
  (e.g. `food-draft.ts`, `food-review-summary.ts`) and import them with the `.ts`
  extension, using `node:test` and `node:assert/strict`.
- `npm run type-check` / `npm run build` generate `public/config.js` from
  `.env.local` (copy `.env.example`). `VITE_LIFEOS_API_URL` has no `/api` suffix.
  Never put a Supabase service-role/secret key in frontend config.

## iOS (Capacitor)
- `ios/` holds the Xcode project and widget extension; see `docs/ios-v1.md`. The
  cooking session was removed from the web app (ADR 0010); its Swift code is
  unreachable and stays until roadmap step 6. The web only clears legacy state.
- No JWT is shared with the widget extension; don't disable ATS.
