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
  `SkipDatabaseMigration=true`. Each module owns a PostgreSQL schema (`households`,
  `food`, `sports`; OpenIddict uses `auth`); `public` only holds `__EFMigrationsHistory`.
- `FoodModuleReset` (roadmap step 1, ADR 0010) replaced the whole previous history:
  it drops every table of `public` with its data. Never edit it; add a new migration:
  `dotnet ef migrations add <Name> --project src/LifeOS.Infrastructure --startup-project src/LifeOS.Api --output-dir Persistence/Migrations`
- `AccountsAndOAuth` (ADR 0011) truncates every household and its data, once, when
  accounts move from Supabase to LifeOS.
- From now on migrations are additive and preserve IDs, prices and snapshots; rollback
  is by restoring a backup, not a destructive down-migration.

## Tests
- `dotnet test` from `src/backend`. `tests/LifeOS.Domain.Tests` are pure unit tests;
  `tests/LifeOS.Application.Tests` test use cases with in-memory fakes of their data ports.
  `tests/LifeOS.Api.IntegrationTests` use `WebApplicationFactory` + Testcontainers
  PostgreSQL (Docker required) with `ASPNETCORE_ENVIRONMENT=Testing` and
  `TestAuthHandler`: simulate users with `client.AsUser(guid)` (`X-Test-Sub` header).
- New household-scoped data needs an isolation test (see
  `HouseholdIsolationTests.cs`); removed features stay covered by
  `RemovedFeaturesTests.cs`.
- Filter example:
  `dotnet test tests/LifeOS.Api.IntegrationTests --filter 'FullyQualifiedName~ReceiptPhotoTests'`
- Local run: `dotnet run --project src/LifeOS.Api` (http://localhost:5292,
  Scalar at `/scalar/v1` in Development); needs a Postgres connection string
  (`ConnectionStrings__Postgres` or `DATABASE_URL`); signing in also needs the `Auth__Google__*`
  settings (docs/google-oauth.md).
- Auth (ADR 0011): the API is its own OAuth server (OpenIddict, `Api/Authentication`),
  Google sign-in, accounts in `households.users`, OAuth tables in schema `auth`. Clients
  are registered at startup in `OAuthClients` (`lifeos-web`) or just in time from a
  trusted Claude metadata document (`ClientMetadataDocuments`); never dynamic registration.
- MCP (`/mcp`, `Api/Assistant`): stateless HTTP transport, policy `Assistant` (tokens
  issued for the `/mcp` resource). Tools are `[McpServerToolType]` classes in their
  module's Api folder (e.g. `Households/HouseholdAssistantTools.cs`), resolve the
  household with `AssistantRequest.HouseholdAsync` and call the same use cases or
  queries as the endpoints. Tool descriptions are French documentation, not prompts.
