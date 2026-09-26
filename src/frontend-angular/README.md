# LifeOS Angular frontend

The frontend is a standalone Angular 22 + TypeScript single-page application,
built with the Angular CLI and styled with PrimeNG and the existing LifeOS
design system. It is a separate, API-backed implementation alongside the
existing Vue frontend; it does not replace that app or its deployment.

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
Add each deployed host to the Supabase Auth redirect URL allow-list for magic-link
callbacks.
Open Food Facts search currently persists or caches matches through the backend,
so searching is a write-capable action rather than a side-effect-free preview.

The predev/prebuild scripts generate an ignored `public/config.js` from those
environment variables. The Supabase anonymous key is public client
configuration; it must never be replaced with a service-role key. API calls
fail visibly when the API URL is missing.

## Validation

```sh
npm run type-check
npm run lint
npm run build
```

There is no dedicated automated test suite yet. The Angular production build
performs strict template and TypeScript checks.

## Docker image

`Dockerfile` builds the Angular application with public runtime configuration
and serves `dist/browser` from nginx:

```sh
docker build -t lifeos-web-angular \
 --build-arg VITE_LIFEOS_API_URL="https://<api-host>" \
 --build-arg VITE_SUPABASE_URL="https://<project-ref>.supabase.co" \
 --build-arg VITE_SUPABASE_ANON_KEY="<anon-key>" \
 .
docker run --rm -p 8080:80 lifeos-web-angular
```

This Docker image is separate from the existing Vue deployment. The API host
must allow the deployed Angular origin through its CORS configuration.

## Routes and hosting

The app uses hash-based routes so it can be served from nginx or a path-based
static host without server-side route rewrites. The default base href is `/`;
for a subpath host, build with `npm run build -- --base-href /<path>/`. Keep its
deployment separate until the Angular app is explicitly selected to replace Vue.
