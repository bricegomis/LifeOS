# Backend notes

- .NET 10, solution `LifeOS.slnx`. Layers point inward:
  `LifeOS.Domain` (entities, invariants, parsing; no EF or I/O) →
  `LifeOS.Application` (use cases, ports) → `LifeOS.Infrastructure` (EF Core
  `LifeOSDbContext`, Npgsql, repositories, file storage) → `LifeOS.Api`
  (minimal API endpoints, DTOs, validation, auth, error handling).
- Modules (ADR 0009): `Households` (core: households, members, profiles), `Food`
  (foods, recipes, reviews, stores, products and prices, receipts), `Sports` (session
  templates). Each has a folder in every project: `Domain/Food/Recipes` (entities and
  rules), `Infrastructure/Food/Recipes` (EF mapping), `Api/Food/Recipes` (endpoints and
  DTOs). Plain management endpoints use the DbContext directly on their own module's
  tables; actions with real rules, or called by another module, are use cases in
  Application, testable without HTTP or a database. A module never touches another
  module's tables or entities; the household core is the only shared reference.
- Endpoints take a `HouseholdContext` parameter (`Api/Households`): it resolves the
  caller's household once (auto-provisioned on first sight). Filter every query and
  command on `household.HouseholdId` and reject cross-household IDs. Do not wrap
  domain calls in `try/catch`: the global handler turns `ArgumentException` into 400
  and PostgreSQL unique / foreign key violations into 409.
- `tests/LifeOS.Architecture.Tests` fails the build when a module references another
  module, when a module endpoint resolves the household by hand, or when
  Domain/Application reference EF or HTTP. Add a new module's name to its list when
  you create the folder.
- Files (receipt photos `/data/receipts`, food and recipe images `/data/foods`) live on
  durable volumes (`Infrastructure/Food/Files`), only served through authenticated,
  household-checked routes.
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
