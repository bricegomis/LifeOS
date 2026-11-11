# LifeOS

## Stack and active scope
- Angular 22 + TypeScript in `src/frontend-angular`, native LifeOS controls and PrimeIcons.
- ASP.NET Core / EF Core / PostgreSQL in `src/backend`.
- Supabase authentication only; household data belongs to the API.
- Generic foods, recipes, sport session templates and manually validated JSON receipts.
  The week planner was removed (ADR 0008) and will be rebuilt later.
- Products are commercial price references subordinate to foods, not a competing library.
- Target (ADR 0009): modular monolith with Food, Sport, Tasks, Health and Planning
  modules; the planner is the core, filled by proposals from an external AI agent
  that the user validates.
- No stock, shopping list, automatic enrichment or Vue frontend.

## Working rules
- Read relevant documentation: `docs/PROJECT.md` (current state), `docs/01-roadmap.md`
  (step order), ADR 0006/0007 (current model), ADR 0010 (step 1 target food model),
  ADR 0009 (target architecture and module boundary rules).
- Prefer simple implementations; no unnecessary dependencies or speculative features.
- Preserve household isolation and historical snapshots, IDs, aliases, sources and prices.
- Never infer catalogue links by name or invent unit conversions.
- Angular checks: `npm test`, `npm run type-check`, `npm run lint`, `npm run build`.
- Backend checks: `dotnet test` from `src/backend`; integration tests require Docker.
- Update directly affected docs for significant changes; do not rewrite historical migrations.
- Back up PostgreSQL before production migration; deployment requires explicit authorization.
