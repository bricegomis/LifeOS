# LifeOS API (backend)

A minimal ASP.NET Core Web API for LifeOS, structured to move progressively towards a
Clean Architecture / DDD layout as recommended by Microsoft's current guidance
(minimal APIs, layered separation of concerns, dependencies pointing inwards).

This first slice implements a single, read-only endpoint: **list the current user's
grocery stores** (`GET /api/stores`), protected by Supabase-issued JWT authentication —
the same identity provider already used by the frontend.

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

- `LifeOS.Domain/Stores/Store.cs`: the `Store` aggregate, mirroring the frontend's
  `GroceryStore` model (`name`, `address`, `isOrganic`, `isLocal`), scoped to an owner.
- `LifeOS.Application/Stores/GetStoresQuery.cs`: the "list stores" use case, backed by
  the `IStoreRepository` port.
- `LifeOS.Infrastructure/Stores/InMemoryStoreRepository.cs`: a temporary in-memory,
  per-user seeded implementation. It is the first, simplest persistence port and is
  expected to be replaced by a real database once the stores feature grows beyond a
  read-only list.
- `LifeOS.Api/Endpoints/StoresEndpoints.cs`: maps `GET /api/stores`, requiring
  authorization and resolving the current user from the JWT `sub` claim.

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
