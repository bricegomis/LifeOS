# LifeOS API (backend)

A minimal ASP.NET Core Web API for LifeOS, structured to move progressively towards a
Clean Architecture / DDD layout as recommended by Microsoft's current guidance
(minimal APIs, layered separation of concerns, dependencies pointing inwards).

All endpoints are protected by Supabase-issued JWT authentication — the same identity
provider already used by the frontend — and, except for the shared meal library, are
scoped to the authenticated user (resolved from the JWT `sub` claim).

## Endpoints

| Area | Endpoints | Notes |
| --- | --- | --- |
| Stores | `GET /api/stores` | Read-only, scoped to the caller's household. PostgreSQL-backed (EF Core). |
| Articles | `GET/POST /api/articles`, `PUT/DELETE /api/articles/{id}`, `POST /api/articles/{id}/price-entries`, `DELETE /api/articles/{id}/price-entries/{priceEntryId}` | Full CRUD, scoped to the caller's household. PostgreSQL-backed (EF Core); mirrors the frontend's `GroceryItem` (name, description, unit, price history). |
| Library | `GET /api/meal-components`, `GET /api/composite-dishes`, `GET /api/activities` | Read-only, shared across users; mirrors `src/frontend/src/data/localLibrary.ts`. |
| Planning rules | `GET/POST /api/planning-rules`, `PUT/DELETE /api/planning-rules/{id}` | Full CRUD, per-owner (in-memory, not yet migrated); pins a meal component or dish to a weekday/meal slot. |
| Frequency rules | `GET/POST /api/frequency-rules`, `PUT/DELETE /api/frequency-rules/{id}` | Full CRUD, per-owner (in-memory, not yet migrated); constrains how many times per week a component/dish/category should appear. |
| Week context | `GET/PUT /api/week-context` | Per-owner singleton (in-memory, not yet migrated). |

`WeekPlan` generation (the weekly planner itself, `src/frontend/src/data/weekGenerator.ts`)
is not yet ported to the backend; it still runs entirely in the frontend.

## Layers

```
src/
  LifeOS.Domain          Entities and business rules. No dependency on any other layer.
  LifeOS.Application     Use cases (queries/commands) and ports (interfaces) consumed
                          by the API. Depends only on Domain.
  LifeOS.Infrastructure   Concrete implementations of Application ports (repositories,
                          external services). Depends on Application.
  LifeOS.Api              ASP.NET Core minimal API host: authentication, endpoint
                          mapping, composition root. Depends on Application and
                          Infrastructure.
```

Each bounded context (`Stores`, `Articles`, `Library`, `Planning`, `WeekContexts`,
`Households`) follows the same shape in every layer: a `Domain` aggregate/entity
mirroring the equivalent frontend model in `src/frontend/src/types.ts` (where
applicable), an `Application` query/command backed by a repository port
(`Application/Common/Interfaces`), and an `Infrastructure` implementation. `Households`,
`Stores`, and `Articles` are persisted in PostgreSQL via EF Core; `Library`, `Planning`,
and `WeekContexts` are still served by temporary in-memory repositories, expected to be
migrated in later milestones. The `Library` context is the only one that is not scoped
per household: it is shared, read-only seed data equivalent to
`src/frontend/src/data/localLibrary.ts`.
`LifeOS.Api/Endpoints/*.cs` maps each context's HTTP routes, requiring authorization and,
for household-scoped contexts, resolving the caller's household id (see "Households and
isolation" below).

## Households and isolation

Every household-scoped table (`stores`, `articles`, `article_price_entries`,
`household_members`, `member_profiles`) carries a `household_id` foreign key to
`households`, and every query/command on these tables is filtered by that id — see
`docs/architecture/decisions/0003-household-isolation.md`. This isolation is enforced
**at the application layer**, not via PostgreSQL Row-Level Security: the backend's
database is a separate PostgreSQL instance from Supabase (which is used purely for
authentication, see `docs/architecture/decisions/0002-supabase-auth-only.md`), so there
is no Supabase-managed RLS to rely on here.

A Supabase user is resolved to a household on every authenticated request via
`ResolveHouseholdForUserQuery` (`LifeOS.Application/Households`):
1. Look up an existing `household_members` row for the caller's `sub` claim.
2. If none exists (e.g. a pre-migration Supabase user's first request against the new
   backend), auto-provision a new household with that user as `owner` — on-demand
   provisioning rather than a bulk data migration, since the current user/data volume is
   low (see `docs/architecture/00-technical-audit.md`).
3. A unique index on `household_members.supabase_user_id` makes step 2 safe under
   concurrent requests: a race is caught as a unique-constraint violation and resolved by
   re-reading the now-existing row instead of failing.

`MemberProfile` (a per-household profile with a portion coefficient) is created
automatically for the owner when a household is provisioned; the model already supports
adding further `member` role members and profiles for later milestones (shared
households), even though the MVP only exercises the `owner` role.

## Persistence (PostgreSQL via EF Core)

`Households`, `Stores`, and `Articles` are persisted through `LifeOSDbContext`
(`LifeOS.Infrastructure/Persistence`), targeting PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.

Connection string resolution (`PostgresConnectionStringResolver`) supports either:
- `ConnectionStrings:Postgres` (standard .NET convention, e.g. env var
  `ConnectionStrings__Postgres`), or
- `DATABASE_URL` as a `postgres://user:pass@host:port/db` URL (Coolify/Heroku-style),

and throws at startup if neither is configured — there is no default/committed
connection string (see `docs/architecture/decisions/0001-postgresql-on-coolify.md`).
Copy `src/backend/.env.example` to a local, non-committed `.env` (or equivalent secret
store) and adjust it.

Migrations live in `LifeOS.Infrastructure/Persistence/Migrations` and are applied
automatically at startup (`dbContext.Database.Migrate()`), unless the
`SkipDatabaseMigration` configuration value is `true` (e.g. if migrations are applied
out-of-band in a deployment pipeline). To generate a new migration after a model
change, from `src/backend`:

```bash
dotnet ef migrations add <Name> \
  --project src/LifeOS.Infrastructure \
  --startup-project src/LifeOS.Api
```

## Testing

From `src/backend`:

```bash
dotnet test
```

- `tests/LifeOS.Domain.Tests` — unit tests for the `Households`, `Stores`, and
  `Articles` domain entities (invariants, no I/O).
- `tests/LifeOS.Api.IntegrationTests` — API-level integration tests using
  `Microsoft.AspNetCore.Mvc.Testing` against a real PostgreSQL instance spun up with
  [Testcontainers](https://dotnet.testcontainers.org/) (`Testcontainers.PostgreSql`);
  requires a working Docker daemon. They exercise the app's real startup path,
  including automatic migrations, and a `TestAuthHandler` (active only when
  `ASPNETCORE_ENVIRONMENT=Testing`) that simulates distinct Supabase users via an
  `X-Test-Sub` header instead of real JWTs. These tests prove:
  - persistence survives a logical API restart (data written by one
    `WebApplicationFactory` instance is read back by an independent instance against the
    same database), and
  - strict cross-household isolation (a household can never read or mutate another
    household's stores or articles, even by guessing another household's resource ids).

## Authentication

The API validates the same JWT access tokens issued by Supabase Auth for the frontend
(`src/frontend/src/services/supabase`). No separate login flow is implemented in the
backend: the frontend keeps using Supabase for sign-in and simply forwards the access
token as a `Bearer` token to the API.

Configuration lives under the `Supabase` section (`appsettings.json` /
environment variables):

```json
{
  "Supabase": {
    "Url": "https://<project-ref>.supabase.co",
    "Audience": "authenticated"
  }
}
```

- `Supabase:Url` — the Supabase project URL (same value as the frontend's
  `VITE_SUPABASE_URL`).
- `Supabase:Audience` — the expected `aud` claim, `authenticated` by default.

The issuer (`{Supabase:Url}/auth/v1`) is used as the JWT bearer `Authority`; signing
keys are resolved automatically from Supabase's OIDC metadata, so no secret is stored
in this repository.

Allowed CORS origins for the frontend dev server / deployed app are configured under
`Cors:AllowedOrigins`.

## Run locally

From `src/backend`:

```bash
dotnet restore
dotnet build
dotnet run --project src/LifeOS.Api
```

Set `Supabase:Url` (e.g. via `dotnet user-secrets` or an environment variable
`Supabase__Url`) to point at your Supabase project before calling the API with a real
access token.

## Testing the API with OpenAPI / Scalar

In the `Development` environment the API exposes its OpenAPI document (via
`Microsoft.AspNetCore.OpenApi`) at `/openapi/v1.json`, and an interactive
[Scalar](https://scalar.com/) API reference/tester at `/scalar/v1` (e.g.
`http://localhost:5292/scalar/v1` when running locally). Use it to browse every endpoint
and send authenticated requests (paste a Supabase access token as a `Bearer` token) without
needing a separate tool such as Postman.

## Docker image

`src/backend/Dockerfile` builds a multi-stage, self-contained image for `LifeOS.Api`
(SDK image to restore/publish, then the smaller ASP.NET runtime image). To build and run
it locally from `src/backend`:

```bash
docker build -t lifeos-api -f Dockerfile .
docker run --rm -p 8080:8080 \
  -e Supabase__Url="https://<project-ref>.supabase.co" \
  -e Cors__AllowedOrigins__0="http://localhost:5173" \
  lifeos-api
```

The container listens on port `8080` (`ASPNETCORE_HTTP_PORTS=8080`) and runs as the
image's built-in non-root `app` user.

### Continuous delivery

The `.github/workflows/build-push-docker-images.yml` workflow builds this image (and
the frontend web image) and pushes it to the GitHub Container Registry
(`ghcr.io/bricegomis/lifeos-api`, tagged `latest`) on every push to `main`. No extra
secrets are needed: it authenticates with the automatically provided `GITHUB_TOKEN`.
