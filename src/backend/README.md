# LifeOS API (backend)

A minimal ASP.NET Core Web API for LifeOS, structured to move progressively towards a
Clean Architecture / DDD layout as recommended by Microsoft's current guidance
(minimal APIs, layered separation of concerns, dependencies pointing inwards).

API data endpoints are protected by Supabase-issued JWT authentication — the same identity
provider already used by the frontend — and are
scoped to the authenticated user (resolved from the JWT `sub` claim). The public
`GET /api/version` endpoint exposes only the API component name and build provenance for deployment
diagnostics.

## Endpoints and model

| Area | API | Notes |
| --- | --- | --- |
| Foods | GET/POST /api/foods, GET/PUT/DELETE /api/foods/{id} | Generic culinary references; manual nullable base/macros/FODMAP/NOVA/notes, partial saves, archival. |
| Products | GET/POST /api/products, GET/PUT/DELETE /api/products/{id}, POST /price-entries | Commercial food link required on creation; purchase unit/package confirmations and observed prices. |
| Comparable prices | GET /api/foods/prices | Last price per product/store then minimum per safe base; provenance/date, no recipe product selection. |
| Receipts | GET/POST /api/receipts, POST /photo, GET /{id}, GET /{id}/photo, PUT /{id}/context, POST /{id}/finalize | JSON or photo drafts; original image, OCR candidates and raw text; exact duplicate detection per household. |
| Receipt lines | POST /{id}/lines, PUT /{id}/lines/{lineId}, POST /ignore, POST /validate | Pending corrections; JSON lines validate individually, photo lines finalize atomically as a batch. |
| Stores | GET/POST /api/stores, PUT/DELETE /{id} | Household stores; required explicit choice for receipts. |
| Recipes / composed meals | /api/recipes, /api/composed-meals | Food ingredients, portions/steps, archival and historical compositions. |
| Manual planner / sports | /api/manual-planner, /api/sport-templates | Civil times, independent weeks, personal portions, unchanged historical snapshots. |
| Historical configuration | /api/planning-rules, /api/frequency-rules, /api/week-context | Preserved but not used by the manual planner. |
| Nutrition / version | /api/nutrition/*, /api/version | Existing totals/configuration; public no-store build provenance. |

All new data is household-scoped. Stock/shopping APIs, article compatibility
catalogue, parallel food seed catalogues and OFF search are removed. Historical
stock/shopping tables and product legacy nutrition/source columns remain non-mapped.
Recipes and new meal lines refer to generic foods; commercial products only carry
prices. The historical balanced engine has no stock dependency, measures ingredient
reuse and returns costs as non-calculable without explicit commercial references.

Receipt JSON limits: 1 MiB UTF-8, 500 lines, depth 32. Unreadable structure is rejected
without partial import. Missing usable fields remain pending with diagnostics.
Raw health fields are archival only and never reach catalogue nutrition.
Total / quantity produces observed price; incompatible units never convert.

### Receipt photos and OCR

Photo tickets use the same household-scoped receipt and purchase model as JSON imports.
`POST /api/receipts/photo` accepts multipart field `photo` (JPEG, PNG or WebP, 10 MiB
by default; the configured limit is capped at 25 MiB). The server checks the image
signature, stores a generated filename outside PostgreSQL, calculates a SHA-256 image
fingerprint, and runs the local `tesseract` executable with `fra+eng` trained data.
There is no remote OCR provider or paid API. The French parser only suggests the
first plausible store line, a day-first date/time, and rows with a final cents amount;
it only suggests quantity when a leading multiplier such as `2 x` is explicit. Unit,
product association and uncertain/missing values require user correction. OCR text and
errors remain on the draft, and an OCR failure still retains the image for manual entry.

Every photo starts as `draft`. The original image is available only through the
authenticated `GET /api/receipts/{id}/photo` endpoint after household ownership is
checked. The client must confirm the store and receipt date/time, review or ignore all
candidate lines, and explicitly select/create products and foods. `POST
/api/receipts/{id}/finalize` requires the complete set of pending-line choices and
processes them under a receipt row lock and one PostgreSQL transaction. A failure
leaves the receipt in draft and rolls back products, foods, purchases, observations and
line statuses. Repeating a successful finalization is idempotent. An observed unit
price is `line total / quantity`; the purchase retains its original total and quantity.
Photo line-by-line validation is rejected to preserve this all-or-nothing boundary.

Configure `Receipts:StoragePath` (`Receipts__StoragePath` in the container) to a durable
directory. The supplied Docker image installs Tesseract and French data, creates
`/data/receipts`, and declares it as a volume. `docker-compose.yaml` mounts the named
volume `lifeos_receipts` there and sets the default size limit. Back up this volume
alongside PostgreSQL: database backups contain paths and receipt metadata, not image
bytes. Keep the same volume mounted when replacing the API container; deleting it
permanently deletes original photos. Local deployments can set `Receipts__StoragePath`
to any persistent directory writable by the API process. Tesseract executable and
language can be overridden with `Receipts__TesseractExecutable` and
`Receipts__TesseractLanguage`. OCR has a 45-second process timeout and 100 KiB text
limit. Receipts, images and OCR are never treated as authoritative: visual review is
required because thermal paper, glare, layout, locale and OCR quality vary.

Targeted checks:

```bash
dotnet test tests/LifeOS.Api.IntegrationTests/LifeOS.Api.IntegrationTests.csproj \
  --filter 'FullyQualifiedName~ReceiptOcrParserTests|FullyQualifiedName~ReceiptPhotoTests'
```

The integration tests use a deterministic local OCR stub and a real PostgreSQL
Testcontainer; they require Docker, but do not require Tesseract on the host. To check
the production OCR runtime and packaging, build the API image with `docker build -f
src/backend/Dockerfile src/backend` and verify `tesseract --list-langs` in the resulting
container includes both `eng` and `fra`.

Layers remain Domain / Application / Infrastructure / API. Endpoint orchestration
uses the scoped DbContext for transactionally consistent receipt writes, as the
existing manual planner does. Domain parsing/invariants remain independent of EF.
See [ADR 0006](../../docs/architecture/decisions/0006-foods-products-receipts.md).

## Households and isolation

Every household-scoped table (all tables listed above except the shared `Library`
seed data) carries a `household_id` foreign key to `households`, and every
query/command on these tables is filtered by that id — see
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

Every household-scoped bounded context (see the endpoints table above) is persisted
through `LifeOSDbContext` (`LifeOS.Infrastructure/Persistence`), targeting PostgreSQL via
`Npgsql.EntityFrameworkCore.PostgreSQL`. Only the shared `Library` seed data (meal
components, composite dishes, activities) remains served from in-memory repositories.

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
  covering households, stores, foods/products/prices, receipts and nutrition.
- `tests/LifeOS.Api.IntegrationTests` — API-level integration tests using
  `Microsoft.AspNetCore.Mvc.Testing` against a real PostgreSQL instance spun up with
  [Testcontainers](https://dotnet.testcontainers.org/) (`Testcontainers.PostgreSql`);
  requires a working Docker daemon. They exercise the app's real startup path,
  including automatic migrations, and a `TestAuthHandler` (active only when
  `ASPNETCORE_ENVIRONMENT=Testing`) that simulates distinct Supabase users via an
  `X-Test-Sub` header instead of real JWTs. These tests prove:
  - persistence survives a logical API restart (data written by one
    `WebApplicationFactory` instance is read back by an independent instance against the
    same database),
  - strict cross-household isolation across every persisted bounded context (stores,
    foods, products, receipts, recipes, composed meals, week planning,
    planning rules, week context), and
  - schema-level integrity constraints that don't depend on application-layer checks
    (e.g. `FoodItemSchemaConstraintsTests` proves the `food_items` → `households` foreign
    key cascades on delete and the self-referencing `is_correction_of` foreign key is
    enforced/`SET NULL`ed at the database level).

## Authentication

The API validates the same JWT access tokens issued by Supabase Auth for the frontend
(`src/frontend-angular/src/app/core/auth`). No separate login flow is implemented in the
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
`Cors:AllowedOrigins`. For the Angular GitHub Pages deployment, set `GITHUB_PAGES_URL` in the
deployment environment and set the GitHub repository variable `VITE_LIFEOS_API_URL` to the
API origin so its sidebar can read `/api/version`.

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
