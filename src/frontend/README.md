# src/frontend

This template should help get you started developing with Vue 3 in Vite.

## Recommended IDE Setup

[VS Code](https://code.visualstudio.com/) + [Vue (Official)](https://marketplace.visualstudio.com/items?itemName=Vue.volar) (and disable Vetur).

## Recommended Browser Setup

- Chromium-based browsers (Chrome, Edge, Brave, etc.):
  - [Vue.js devtools](https://chromewebstore.google.com/detail/vuejs-devtools/nhdogjmejiglipccpnnnanhbledajbpd)
  - [Turn on Custom Object Formatter in Chrome DevTools](http://bit.ly/object-formatters)
- Firefox:
  - [Vue.js devtools](https://addons.mozilla.org/en-US/firefox/addon/vue-js-devtools/)
  - [Turn on Custom Object Formatter in Firefox DevTools](https://fxdx.dev/firefox-devtools-custom-object-formatters/)

## Type Support for `.vue` Imports in TS

TypeScript cannot handle type information for `.vue` imports by default, so we replace the `tsc` CLI with `vue-tsc` for type checking. In editors, we need [Volar](https://marketplace.visualstudio.com/items?itemName=Vue.volar) to make the TypeScript language service aware of `.vue` types.

## Customize configuration

See [Vite Configuration Reference](https://vite.dev/config/).

## Project Setup

```sh
npm install
```

### Compile and Hot-Reload for Development

```sh
npm run dev
```

### Type-Check, Compile and Minify for Production

```sh
npm run build
```

### Lint with [ESLint](https://eslint.org/)

```sh
npm run lint
```

## Docker image

`src/frontend/Dockerfile` builds the app with Vite (baking the `VITE_SUPABASE_URL` /
`VITE_SUPABASE_ANON_KEY` build args into the bundle) and serves the static output with
nginx. To build and run it locally from `src/frontend`:

```bash
docker build -t lifeos-web \
  --build-arg VITE_SUPABASE_URL="https://<project-ref>.supabase.co" \
  --build-arg VITE_SUPABASE_ANON_KEY="<anon-key>" \
  .
docker run --rm -p 8080:80 lifeos-web
```

### Continuous delivery

The Vue Dockerfile remains available for local or parallel builds. The
`.github/workflows/build-push-docker-images.yml` workflow now publishes the Angular
frontend from `src/frontend-angular` as `ghcr.io/bricegomis/lifeos-web`; it no longer
publishes this Vue image.

The GitHub Pages build injects the commit SHA as the UI build ID and can read the API
build ID from `GET /api/version`. Set the repository variable `VITE_LIFEOS_API_URL` to
the API origin (without `/api`) to enable that lookup; configure the same site's origin
as `GITHUB_PAGES_URL` in the API deployment environment so CORS permits the request.
The sidebar shows abbreviated UI/API IDs immediately above the signed-in user's email.
For local development the UI reports `dev`; a local production build reports `local`.
When building the Vue Docker image directly, pass matching
`VITE_LIFEOS_BUILD_ID` and `VITE_LIFEOS_API_URL` build arguments.
