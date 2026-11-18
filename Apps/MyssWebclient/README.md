# MySS Web Client

Single-page front-end for the **My Self Serve (MySS)** API. Built with React 19, Vite and
TypeScript, using the [BC Government Design System](https://www2.gov.bc.ca/gov/content/digital/design-system)
(`@bcgov/design-system-react-components`, `@bcgov/bc-sans`, `@bcgov/design-tokens`).

Data is fetched with [TanStack Query](https://tanstack.com/query) through a typed client that is
generated from the MySS API's OpenAPI document.

## Developer setup

- Install [Node.js](https://nodejs.org) 24 (the version the Dockerfile builds with;
  `package.json` declares no `engines`).
- Run `npm install` from `Apps/`, the npm workspace root. The webclient shares
  its Form.io components with the admin webclient through the `@myss/formio`
  workspace (`Apps/MyssFormio`) and its auth, runtime config and shared
  components through the `@myss/web-common` workspace (`Apps/MyssWebCommon`).
  There is one lockfile for all four workspaces, at `Apps/package-lock.json`. Scripts run here as before, or from `Apps/` with
  `npm run <script> -w myss-webclient`.
- Copy `.env.sample` to `.env`, set `VITE_MYSS_API_URL` to the MySS API base URL,
  and set `VITE_CANADA_POST_API_KEY` to a URL-restricted Canada Post
  AddressComplete browser key. `VITE_OIDC_AUTHORITY` and `VITE_OIDC_CLIENT_ID`
  set the sign-in realm and client (the sample has the dev values), and
  `VITE_LEGACY_MYSS_URL` is where `/` redirects while the `HomePage` feature
  flag is off (empty uses https://myselfserve.gov.bc.ca).
- Run `npm run dev` to start the dev server with hot reloading.

## API client generation

The typed API client under `src/api/generated/` is produced by
[`@hey-api/openapi-ts`](https://heyapi.dev) from the running API's Swagger document:

```bash
# Requires the MySS API running and VITE_MYSS_API_URL set in .env
npm run generate:schema
```

Configuration lives in `openapi-ts.config.ts`.

## Scripts

| Script                       | Purpose                                  |
| ---------------------------- | ---------------------------------------- |
| `npm run dev`                | Start the Vite dev server                |
| `npm run build`              | Type-check (`tsc -b`) and build for prod |
| `npm run preview`            | Preview the production build             |
| `npm run lint`               | Run ESLint                               |
| `npm run format`             | Format with Prettier                     |
| `npm run format:check`       | Check formatting without writing         |
| `npm run test:unit`          | Run unit tests (Vitest)                  |
| `npm run test:browser`       | Run browser tests (Vitest + Playwright)  |
| `npm run test:browser-headless` | Run browser tests headless            |
| `npm run test:coverage`      | Run all tests headless with coverage     |
| `npm run generate:schema`    | Regenerate `src/api/generated/`          |

## Container image

The image builds from the workspace root, not this directory:

```bash
# from Apps/
docker build -f MyssWebclient/Dockerfile -t myss-webclient .
```

## Runtime configuration

The build is environment-agnostic. At container startup `entrypoint.sh` writes
`config.js` from the `MYSS_API_URL`, `OIDC_AUTHORITY`, `OIDC_CLIENT_ID`,
`CANADA_POST_API_KEY` and `LEGACY_MYSS_URL` environment variables; they land on
`window.APP_CONFIG` and are resolved by `resolveRuntimeConfig` from
`@myss/web-common/config`, called from `src/constants.ts`. During local dev
the resolver falls back to the corresponding `VITE_*` variables. The Canada Post key is visible in the browser by design, so deployed
keys must be restricted by valid URLs and a daily cap.
