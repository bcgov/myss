# MySS Web Client

Single-page front-end for the **My Self Serve (MySS)** API. Built with React 19, Vite and
TypeScript, using the [BC Government Design System](https://www2.gov.bc.ca/gov/content/digital/design-system)
(`@bcgov/design-system-react-components`, `@bcgov/bc-sans`, `@bcgov/design-tokens`).

Data is fetched with [TanStack Query](https://tanstack.com/query) through a typed client that is
generated from the MySS API's OpenAPI document.

## Developer setup

- Install [Node.js](https://nodejs.org) (see `package.json` `engines`/Dockerfile for the version).
- Run `npm install` to install dependencies.
- Copy `.env.sample` to `.env` and set `VITE_MYSS_API_URL` to the MySS API base URL.
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
| `npm run test:unit`          | Run unit tests (Vitest)                  |
| `npm run test:browser`       | Run browser tests (Vitest + Playwright)  |

## Runtime configuration

The build is environment-agnostic. At container startup `entrypoint.sh` writes
`config.js` from the `MYSS_API_URL` environment variable; it lands on `window.APP_CONFIG`
and is read by `src/constants.ts`. During local dev, `src/constants.ts` falls back to
`import.meta.env.VITE_MYSS_API_URL`.
