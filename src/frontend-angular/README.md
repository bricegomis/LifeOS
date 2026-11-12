# LifeOS Angular frontend

The frontend is a standalone Angular 22 + TypeScript single-page application,
built with the Angular CLI and styled with native HTML controls and the existing
LifeOS CSS tokens. PrimeIcons (MIT) supplies the icons; no PrimeNG/PrimeUI runtime. It is the only, API-backed
frontend, published by the Docker delivery workflow for VPS deployments and
packaged for iPhone with Capacitor (see [docs/ios-v1.md](../../docs/ios-v1.md)).

## Pages

`/` opens recipes. Foods, recipes, sport templates, stores, tickets and settings
are API-backed. The manual week planner and Today view (`/planning`, `/today`)
were removed by [ADR 0008](../../docs/architecture/decisions/0008-remove-week-planner.md);
old links redirect to recipes.

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

- `LIFEOS_API_URL` to the backend origin (for example `http://localhost:5000`, without `/api`).
  The former name `VITE_LIFEOS_API_URL` is still read when `LIFEOS_API_URL` is unset,
  in the build script, the Docker image, the compose files and the Pages workflow.
- `VITE_SUPABASE_URL`
- `VITE_SUPABASE_ANON_KEY`

The backend must allow the Angular app's origin in `Cors:AllowedOrigins`.
Supabase is used for authentication only; household data is read from and
written to the LifeOS API using the Supabase access token.
Google OAuth is the primary sign-in method; email magic links remain available.
Follow [the Google/Supabase setup guide](../../docs/google-oauth.md) for provider
activation, callback/redirect allow-lists, existing-account linking limitations,
and the final private-Firefox check. No Google secret belongs in frontend config.

The predev/prebuild scripts generate an ignored `public/config.js` from those
environment variables for local development and builds. The Supabase
anonymous key is public client configuration; it must never be replaced with a
service-role key. API calls fail visibly when the API URL is missing.

The sidebar shows separate `UI Build #123` and `API Build #123` links to the
producing GitHub Actions run/attempt. Retries show `· tentative 2`; expandable
details retain the workflow name, run ID, attempt and full commit. The settings page
uses the same UI version display. The UI reads the API's public, no-store
`GET /api/version` diagnostic endpoint at startup, never the latest GitHub run.
Docker builds both images together, but independently updated containers can differ.
Angular Pages uses a different workflow: compare commits, not just workflow-scoped numbers.
Local builds show `Local` without a CI number, and a missing API URL or failed request is shown as
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
`LIFEOS_API_URL`, so the same image
can be deployed to different Supabase projects:

```sh
docker build -t lifeos-web .
docker run --rm -p 8080:80 \
  -e LIFEOS_API_URL="https://<api-host>" \
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

Build provenance is generated into an ignored
`src/environments/build-info.generated.ts` module and compiled into the hashed
application bundle. An old cached bundle therefore still reports its own build,
not the identity of a newer server-side file.
The runtime entrypoint only replaces `config.js`; changing a deployment's environment
cannot change the UI's build identity. For CI builds, pass Docker build arguments
`LIFEOS_BUILD_ID` (commit), `LIFEOS_BUILD_NUMBER` (`github.run_number`),
`LIFEOS_RUN_ID`, `LIFEOS_RUN_ATTEMPT`, `LIFEOS_BUILD_REPOSITORY`,
`LIFEOS_BUILD_SERVER_URL`, and `LIFEOS_BUILD_WORKFLOW`. The same names are used
by `scripts/generate-config.mjs` for direct builds. The validation workflow embeds
its own run, whereas published images embed the Docker publication workflow's run.
Do not supply metadata from a later deployment or another workflow.

## Routes and hosting

The app uses hash-based routes so it can be served from nginx or a path-based
static host without server-side route rewrites. The default base href is `/`;
for a subpath host, build with `npm run build -- --base-href /<path>/`.
