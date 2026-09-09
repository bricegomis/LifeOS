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

The `.github/workflows/build-push-docker-images.yml` workflow builds both this image
and the backend API image, then pushes them to the GitHub Container Registry
(`ghcr.io/bricegomis/lifeos-web` and `ghcr.io/bricegomis/lifeos-api`) on every push to
`main`. The `VITE_*` build args are read from repository **Variables** (Settings →
Secrets and variables → Actions → Variables tab), not Secrets, since they are baked into
the client bundle and are not sensitive.
