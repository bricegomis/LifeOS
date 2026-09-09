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
| Stores | `GET /api/stores` | Read-only, per-owner. |
| Articles | `GET/POST /api/articles`, `PUT/DELETE /api/articles/{id}`, `POST /api/articles/{id}/price-entries`, `DELETE /api/articles/{id}/price-entries/{priceEntryId}` | Full CRUD, per-owner, mirrors the frontend's `GroceryItem` (name, description, unit, price history). |
| Library | `GET /api/meal-components`, `GET /api/composite-dishes`, `GET /api/activities` | Read-only, shared across users; mirrors `src/frontend/src/data/localLibrary.ts`. |
| Planning rules | `GET/POST /api/planning-rules`, `PUT/DELETE /api/planning-rules/{id}` | Full CRUD, per-owner; pins a meal component or dish to a weekday/meal slot. |
| Frequency rules | `GET/POST /api/frequency-rules`, `PUT/DELETE /api/frequency-rules/{id}` | Full CRUD, per-owner; constrains how many times per week a component/dish/category should appear. |
| Week context | `GET/PUT /api/week-context` | Per-owner singleton (alternating week config, overrides, per-day settings). |

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

Each bounded context (`Stores`, `Articles`, `Library`, `Planning`, `WeekContexts`) follows
the same shape in every layer: a `Domain` aggregate/entity mirroring the equivalent
frontend model in `src/frontend/src/types.ts`, an `Application` query/command backed by a
repository port (`Application/Common/Interfaces`), and a temporary in-memory
`Infrastructure` repository — the first, simplest persistence port, expected to be
replaced by a real database (e.g. PostgreSQL via EF Core) as each feature grows. The
`Library` context is the only one that is not per-owner: it is shared, read-only seed
data equivalent to `src/frontend/src/data/localLibrary.ts`.
`LifeOS.Api/Endpoints/*.cs` maps each context's HTTP routes, requiring authorization and
resolving the current user from the JWT `sub` claim.

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
