# LifeOS Angular frontend

The frontend is a standalone Angular 22 + TypeScript single-page application,
built with the Angular CLI and styled with PrimeNG and the existing LifeOS
design system. It is a separate, API-backed implementation alongside the
existing Vue frontend. It is the frontend published by the Docker delivery
workflow for VPS deployments.

## Local development

Use Node.js 22.22.3+ or 24.15.0+:

```sh
npm ci
npm run dev
```

Configure the API and authentication in `.env.local`:

```sh
cp .env.example .env.local
```

Set:

- `VITE_LIFEOS_API_URL` to the backend origin (for example `http://localhost:5000`, without `/api`)
- `VITE_SUPABASE_URL`
- `VITE_SUPABASE_ANON_KEY`

The backend must allow the Angular app's origin in `Cors:AllowedOrigins`.
Supabase is used for authentication only; household data is read from and
written to the LifeOS API using the Supabase access token.
Google OAuth is the primary sign-in method; email magic links remain available.
Follow [the Google/Supabase setup guide](../../docs/google-oauth.md) for provider
activation, callback/redirect allow-lists, existing-account linking limitations,
and the final private-Firefox check. No Google secret belongs in frontend config.
Open Food Facts search currently persists or caches matches through the backend,
so searching is a write-capable action rather than a side-effect-free preview.

The predev/prebuild scripts generate an ignored `public/config.js` from those
environment variables for local development and builds. The Supabase
anonymous key is public client configuration; it must never be replaced with a
service-role key. API calls fail visibly when the API URL is missing.

The sidebar shows the UI and API build IDs above the signed-in user's email.
GitHub Actions builds both Docker images from the same commit SHA; the UI reads
the API's public `GET /api/version` diagnostic endpoint at startup. Local builds
use `local`, and a missing API URL or failed request is shown as
`non configurée` or `indisponible`.

## Validation

```sh
npm run type-check
npm test
npm run lint
npm run build
```

Auth flow and callback tests use Node's built-in test runner, including a real
Supabase SDK PKCE exchange with a simulated transport. The Angular production
build performs strict template and TypeScript checks. Real Google sign-in needs
manual provider setup and is not covered by these tests.

## Docker image

`Dockerfile` builds a Supabase-independent Angular image and serves
`dist/browser` from nginx. The nginx entrypoint writes `config.js` at container
startup from `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY`, and
`VITE_LIFEOS_API_URL`, plus the build ID baked into the image, so the same image
can be deployed to different Supabase projects:

```sh
docker build -t lifeos-web .
docker run --rm -p 8080:80 \
  -e VITE_LIFEOS_API_URL="https://<api-host>" \
  -e VITE_SUPABASE_URL="https://<project-ref>.supabase.co" \
  -e VITE_SUPABASE_ANON_KEY="<public-anon-key>" \
  lifeos-web
```

The `.github/workflows/build-push-docker-images.yml` workflow publishes this
unconfigured image as `ghcr.io/bricegomis/lifeos-web:latest` on pushes to
`main`; no Supabase settings are required in GitHub Actions. Supply the three
runtime variables when starting the container (for example in the root
`docker-compose.yaml`). `VITE_SUPABASE_URL` should use the same project URL as
the API's `SUPABASE_URL`. The anonymous key is public and must never be
replaced with a service-role key. The API host must allow the deployed Angular
origin through its CORS configuration.

## Routes and hosting

The app uses hash-based routes so it can be served from nginx or a path-based
static host without server-side route rewrites. The default base href is `/`;
for a subpath host, build with `npm run build -- --base-href /<path>/`.
