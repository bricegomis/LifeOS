# Backend notes

- .NET 10, solution `LifeOS.slnx`. Layers point inward:
  `LifeOS.Domain` (entities, invariants, parsing; no EF or I/O) →
  `LifeOS.Application` (use cases, ports) → `LifeOS.Infrastructure` (EF Core
  `LifeOSDbContext`, Npgsql, repositories, file storage) → `LifeOS.Api`
  (minimal API endpoints, DTOs, validation, auth, error handling).
- Reference slice: recipes. `Domain/Food/Recipes` (entity and rules),
  `Infrastructure/Food/Recipes` (EF mapping), `Api/Food/Recipes` (endpoints and DTOs,
  DbContext used directly because it is plain management). Copy this layout when
  moving the rest of the existing code. Module names: `Households` (core), `Food`,
  `Sports`.
- Endpoints take a `HouseholdContext` parameter (`Api/Households`): it resolves the
  caller's household once. Do not resolve it by hand in new or moved endpoints, and
  do not wrap domain calls in `try/catch (ArgumentException)`: the global handler
  returns the 400.
- `tests/LifeOS.Architecture.Tests` fails the build when a module references another
  module or when Domain/Application reference EF or HTTP. Add a new module's name to
  its list when you create the folder.
- New modules follow ADR 0009: one folder per module in each project
  (`Domain/Sport`, `Application/Sport`, ...), own PostgreSQL schema. Plain CRUD
  endpoints may use the DbContext directly on their own module's tables; actions
  with real rules, or called by another module, are use cases in Application behind
  a module data port, testable without HTTP or a database. There is no access to another
  module's tables or entities (published read contract only). Existing Food and
  Household code is brought to this style in roadmap step 1 (`docs/01-roadmap.md`).
- Auth: Supabase-issued JWT bearer; the caller's household is resolved per request
  (`ResolveHouseholdForUserQuery`, auto-provisioned on first sight). Filter every
  query/command by that household and reject cross-household IDs.
- Files (receipt photos `/data/receipts`, food images `/data/foods`) live on durable
  volumes, only served through authenticated, household-checked routes.
- A receipt is only a stored photo (ADR 0010): no OCR, no lines, no purchases.

## Migrations
- In `src/LifeOS.Infrastructure/Persistence/Migrations`, applied at API startup unless
  `SkipDatabaseMigration=true`. Never edit an existing migration or the history of
  `UnifiedProducts`/`FoodsAndReceipts`; add a new one:
  `dotnet ef migrations add <Name> --project src/LifeOS.Infrastructure --startup-project src/LifeOS.Api`
- Migrations must be additive and preserve IDs, aliases, sources, prices and
  snapshots; rollback is by restoring a backup, not a destructive down-migration.
  Exceptions: `RemoveWeekPlanner` (ADR 0008), and roadmap step 1, where the user
  accepted losing existing data (ADR 0009) — confirm what is reset before writing it.

## Tests
- `dotnet test` from `src/backend`. `tests/LifeOS.Domain.Tests` are pure unit tests.
  `tests/LifeOS.Api.IntegrationTests` use `WebApplicationFactory` + Testcontainers
  PostgreSQL (Docker required) with `ASPNETCORE_ENVIRONMENT=Testing` and
  `TestAuthHandler`: simulate users with `client.AsUser(guid)` (`X-Test-Sub` header).
- New household-scoped data needs an isolation test (see
  `HouseholdIsolationTests.cs`); removed features stay covered by
  `RemovedFeaturesTests.cs`.
- Filter example:
  `dotnet test tests/LifeOS.Api.IntegrationTests --filter 'FullyQualifiedName~ReceiptPhotoTests'`
- Local run: `dotnet run --project src/LifeOS.Api` (http://localhost:5292,
  Scalar at `/scalar/v1` in Development); needs `Supabase__Url` and a Postgres
  connection string (`ConnectionStrings__Postgres` or `DATABASE_URL`).
