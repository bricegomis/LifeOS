# LifeOS API (backend)

ASP.NET Core minimal API in four layers (Domain / Application / Infrastructure / Api).
The target module layout and boundary rules are in
[ADR 0009](../../docs/architecture/decisions/0009-modular-monolith-and-planning-core.md).

Data endpoints require an access token issued by the API itself (see Authentication) and are scoped to the caller's
household. The public `GET /api/version` endpoint exposes only the API component
name and build provenance.

## Endpoints and model

| Area | API | Notes |
| --- | --- | --- |
| Foods | GET/POST /api/foods, GET/PUT/DELETE /api/foods/{id} | Generic culinary references; manual nullable base/macros/FODMAP/NOVA/notes, partial saves, archival. |
| Products | GET/POST /api/products, GET/PUT/DELETE /api/products/{id}, POST /price-entries | Commercial food link required on creation; purchase unit/package confirmations and observed prices. |
| Comparable prices | GET /api/foods/prices | Last price per product/store then minimum per safe base, with date; no recipe product selection. |
| Receipts | GET /api/receipts, POST /photo, GET /{id}/photo, PUT /{id} | A receipt is a stored photo with optional store and purchase date and a pending/processed status; exact duplicate detection per household. Nothing is read from the image. |
| Stores | GET/POST /api/stores, PUT/DELETE /{id} | Household stores; optional on a receipt. |
| Food images | GET/PUT/DELETE /api/foods/{id}/image | Optional image stored on a durable volume. |
| Recipes | /api/recipes, /api/recipes/{id}/ingredients | Food ingredients, yield, steps, duration, categories. |
| Reviews | /api/reviews/foods/{id}, /api/reviews/recipes/{id} | Optional 0..10 score and/or comment, per member profile. |
| Household | /api/households/current, /members, /profiles | Household auto-provisioned on first request; member profiles. |
| Sport templates | /api/sport-templates | Session templates (name, sport, duration, distance, intensity, manual calories). |
| Version | /api/version | Public, no-store build provenance. |

All data is household-scoped. Recipes refer to generic foods; commercial products
only carry prices. Tables from earlier models (stock, shopping list, legacy
configuration) may still exist in the database but are not mapped.

### Receipt photos

`POST /api/receipts/photo` accepts multipart field `photo` (JPEG, PNG or WebP, 10 MiB
by default, signature checked) plus optional `storeId` and `purchaseDate`
(`yyyy-MM-dd`). The API stores the file under a server-generated name, keeps its SHA-256
to refuse an exact duplicate in the same household, and creates a `pending` receipt.
Nothing else is created: no product, no price, no OCR (ADR 0010). `PUT /api/receipts/{id}`
sets or clears the store and date and switches between `pending` and `processed`.
Images are only served by the authenticated, household-checked
`GET /api/receipts/{id}/photo`.

Configure `Receipts:StoragePath` (`Receipts__StoragePath` in the container) to a durable
directory. The Docker image creates `/data/receipts` and declares it as a volume;
`docker-compose.yaml` mounts the named volume `lifeos_receipts` there. Back up this
volume alongside PostgreSQL: database backups contain file names and metadata, not
image bytes.

Targeted check (needs Docker):

```bash
dotnet test tests/LifeOS.Api.IntegrationTests/LifeOS.Api.IntegrationTests.csproj \
  --filter 'FullyQualifiedName~ReceiptPhotoTests'
```

Layers remain Domain / Application / Infrastructure / API.
See [ADR 0010](../../docs/architecture/decisions/0010-simplified-food-module.md).

## Households and isolation

Every business table carries a `household_id` foreign key to `households`, and every
query/command on these tables is filtered by that id — see
`docs/architecture/decisions/0003-household-isolation.md`. This isolation is enforced
**at the application layer**, not via PostgreSQL Row-Level Security: the backend's
database has no RLS policies to rely on.

A signed-in user account is resolved to a household on every authenticated request via
`ResolveHouseholdForUserQuery` (`LifeOS.Application/Households`):
1. Look up an existing `household_members` row for the caller's `sub` claim.
2. If none exists (the account's first request), auto-provision a new household with
   that user as `owner`.
3. A unique index on `household_members.UserId` makes step 2 safe under
   concurrent requests: a race is caught as a unique-constraint violation and resolved by
   re-reading the now-existing row instead of failing.

`MemberProfile` (a per-household profile with a portion coefficient) is created
automatically for the owner when a household is provisioned; the model already supports
adding further `member` role members and profiles for later milestones (shared
households), even though the MVP only exercises the `owner` role.

## Persistence (PostgreSQL via EF Core)

Everything is persisted through `LifeOSDbContext` (`LifeOS.Infrastructure/Persistence`),
targeting PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.

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

- `tests/LifeOS.Domain.Tests` — unit tests for domain entities (invariants, no I/O),
  covering households, stores, foods/products/prices and receipts.
- `tests/LifeOS.Api.IntegrationTests` — API-level integration tests using
  `Microsoft.AspNetCore.Mvc.Testing` against a real PostgreSQL instance spun up with
  [Testcontainers](https://dotnet.testcontainers.org/) (`Testcontainers.PostgreSql`);
  requires a working Docker daemon. They exercise the app's real startup path,
  including automatic migrations, and a `TestAuthHandler` (active only when
  `ASPNETCORE_ENVIRONMENT=Testing`) that simulates distinct users via an
  `X-Test-Sub` header instead of real tokens (and creates their account on first sight). These tests prove:
  - persistence survives a logical API restart (data written by one
    `WebApplicationFactory` instance is read back by an independent instance against the
    same database),
  - strict cross-household isolation (stores, foods, products, receipts, recipes,
    reviews, sport templates), and
  - schema-level integrity constraints that don't depend on application-layer checks
    (e.g. `FoodItemSchemaConstraintsTests` proves the `food_items` → `households` foreign
    key cascades on delete and the self-referencing `is_correction_of` foreign key is
    enforced/`SET NULL`ed at the database level).

## Authentication

The API is its own OAuth 2.1 server ([ADR 0011](../../docs/architecture/decisions/0011-conversational-assistant-mcp.md)),
built on OpenIddict (`LifeOS.Api/Authentication`):

- `/connect/authorize` signs the user in with Google (`/signin-google`), keeps a
  one-day `lifeos.session` cookie and returns an authorization code (PKCE S256 only).
  Only verified addresses listed in `Auth:AllowedEmails` get in; the first sign-in
  creates the `households.users` row (`SignInWithGoogle`).
- `/connect/token` exchanges codes and rotating refresh tokens; the account is checked
  again at each refresh (`GetActiveAccount`). `/connect/revoke` revokes a token.
- API routes validate the access token locally (`sub` = LifeOS user id).
- Clients are registered at startup, never dynamically (`OAuthClients`): `lifeos-web`,
  the Angular app, with `Auth:WebClient:RedirectUris`.
- OpenIddict tables live in the `auth` schema.

Configuration (`Auth` section; see [docs/google-oauth.md](../../docs/google-oauth.md)):
`Issuer`, `AllowedEmails`, `Google:ClientId`, `Google:ClientSecret`, `SigningKey` (RSA
PEM), `EncryptionKey` (32 bytes, base64), `WebClient:RedirectUris`. In Development and
Testing the token keys are ephemeral when not set.

Allowed CORS origins for the frontend dev server / deployed app are configured under
`Cors:AllowedOrigins`. For the Angular GitHub Pages deployment, set `GITHUB_PAGES_URL` in the
deployment environment and set the GitHub repository variable `LIFEOS_API_URL` to the
API origin so its sidebar can read `/api/version`.

## Run locally

From `src/backend`:

```bash
dotnet restore
dotnet build
dotnet run --project src/LifeOS.Api
```

Signing in needs a Google client (`Auth__Google__ClientId`, `Auth__Google__ClientSecret`)
and `Auth__AllowedEmails__0`; see [docs/google-oauth.md](../../docs/google-oauth.md).

## Testing the API with OpenAPI / Scalar

In the `Development` environment the API exposes its OpenAPI document (via
`Microsoft.AspNetCore.OpenApi`) at `/openapi/v1.json`, and an interactive
[Scalar](https://scalar.com/) API reference/tester at `/scalar/v1` (e.g.
`http://localhost:5292/scalar/v1` when running locally). Use it to browse every endpoint
and send authenticated requests (paste a LifeOS access token as a `Bearer` token) without
needing a separate tool such as Postman.

In production, both routes are disabled by default. To enable them on the VPS, set
`ENABLE_OPENAPI_UI=true` in the deployment `.env` file used by `docker-compose.yaml`, then
recreate the API container so Compose passes it as `Api__EnableOpenApiUi=true`:

```bash
docker compose up -d --force-recreate api
```

The document and UI are then available at
`/openapi/v1.json` and `/scalar/v1` on the API host. Set it back to `false` (or omit it)
and run the same command to disable them again. A plain `docker compose restart` does not
reload environment changes. These routes are publicly reachable unless access is
restricted separately at the reverse proxy; API operations still require authentication.

## Docker image

`src/backend/Dockerfile` builds a multi-stage, self-contained image for `LifeOS.Api`
(SDK image to restore/publish, then the smaller ASP.NET runtime image). To build and run
it locally from `src/backend`:

```bash
docker build -t lifeos-api --build-arg LIFEOS_BUILD_ID=local -f Dockerfile .
docker run --rm -p 8080:8080 \
  -e Auth__Google__ClientId="<google-client-id>" \
  -e Auth__Google__ClientSecret="<google-client-secret>" \
  -e Auth__SigningKey="$(openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048)" \
  -e Auth__EncryptionKey="$(openssl rand -base64 32)" \
  -e Auth__AllowedEmails__0="you@example.com" \
  -e Cors__AllowedOrigins__0="http://localhost:4200" \
  lifeos-api
```

The container listens on port `8080` (`ASPNETCORE_HTTP_PORTS=8080`) and runs as the
image's built-in non-root `app` user.

### Continuous delivery

The `.github/workflows/build-push-docker-images.yml` workflow builds this image (and
the frontend web image) and pushes it to the GitHub Container Registry
(`ghcr.io/bricegomis/lifeos-api`, tagged `latest`) on every push to `main`. No extra
secrets are needed: it authenticates with the automatically provided `GITHUB_TOKEN`.
Both images receive the producing workflow's `github.run_number`, `github.run_id`,
`github.run_attempt`, repository, server URL and workflow name, alongside the full
commit SHA. Docker build arguments are `LIFEOS_BUILD_ID` (commit),
`LIFEOS_BUILD_NUMBER`, `LIFEOS_RUN_ID`, `LIFEOS_RUN_ATTEMPT`,
`LIFEOS_BUILD_REPOSITORY`, `LIFEOS_BUILD_SERVER_URL`, and `LIFEOS_BUILD_WORKFLOW`.
The API image retains these under the corresponding `LifeOS__BuildId`,
`LifeOS__BuildNumber`, `LifeOS__RunId`, `LifeOS__RunAttempt`, `LifeOS__Repository`,
`LifeOS__ServerUrl`, and `LifeOS__Workflow` configuration keys. Do not replace
them with deployment-time values or the latest run: they identify the built image.

The UI displays `UI Build #123` and `API Build #123` separately, linking each to
its exact run and attempt. A rerun displays `Build #123 · tentative 2`.
Workflow name, run ID, attempt and full commit remain available in expandable
details. Run numbers are scoped to a workflow, not globally unique. API and Angular
are built together in this Docker workflow, but independently updated `latest`
containers may temporarily differ. Angular Pages is built in a separate workflow;
its number need not match the API even when the commits match.

`GET /api/version` keeps the existing `component` and `buildId` fields and adds
nullable string fields `buildNumber`, `runId`, `runAttempt`, `repository`,
`serverUrl`, and `workflow`. Local builds default to `local`, with no CI number.
An older API returning only a commit is displayed as `Build non identifié`, with
its commit in details. Missing API configuration and request failures remain explicit.
