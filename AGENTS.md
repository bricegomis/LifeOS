# LifeOS

## Stack and active scope
- Angular 22 + TypeScript in `src/frontend-angular`, native LifeOS controls and PrimeIcons.
- ASP.NET Core / EF Core / PostgreSQL in `src/backend`.
- Supabase authentication only; household data belongs to the API.
- Manual meal and sport planning, generic foods, recipes and manually validated JSON receipts.
- Products are commercial price references subordinate to foods, not a competing library.
- No stock, shopping list, automatic enrichment or Vue frontend.

## Working rules
- Read relevant documentation; `docs/PROJECT.md` and ADR 0006 describe the current model.
- Prefer simple implementations; no unnecessary dependencies or speculative features.
- Preserve household isolation and historical snapshots, IDs, aliases, sources and prices.
- Never infer catalogue links by name or invent unit conversions.
- Angular checks: `npm test`, `npm run type-check`, `npm run lint`, `npm run build`.
- Backend checks: `dotnet test` from `src/backend`; integration tests require Docker.
- Update directly affected docs for significant changes; do not rewrite historical migrations.
- Back up PostgreSQL before production migration; deployment requires explicit authorization.
