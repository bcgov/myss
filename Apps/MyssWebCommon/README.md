# @myss/web-common

The code both webclients need and would otherwise each keep a copy of:

- **MyssWebclient** (the citizen site) and
- **MyssAdminWebclient** (the staff site)

sign in the same way, read the same runtime settings, and show some of the
same API data. This package holds that code once.

## What is in it

| Entry point                   | What it holds                                                                                                                                                                                                                  |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `@myss/web-common/config`     | `resolveRuntimeConfig`: the API address, OIDC realm and client, and Canada Post key, read from `window.APP_CONFIG`, then `VITE_*`, then a default. Each app passes its own default API address and its `import.meta.env`.      |
| `@myss/web-common/auth`       | The access token store and `authHeaders()`, the OIDC settings builder, the SiteMinder sign-out, the 15-minute idle timeout (RULE-IDA-07), the `/auth/me` read and query, and the open-redirect guard for the sign-in callback. |
| `@myss/web-common/api`        | The shapes both APIs answer with: form specs and the 422 error list, the application status and problem-details refusal, and the eligibility rate table.                                                                       |
| `@myss/web-common/components` | `AuthCallbackPage`, `StatusPill` and `AnnouncementBanner`.                                                                                                                                                                     |

What differs between the apps is passed in rather than written here: where
sign-out returns to, where the callback lands when there is no return path,
which API `/auth/me` is read from, and each app's `/auth/me` payload type.
Each app keeps a small module that binds these (for example
`src/auth/siteMinderLogout.ts`), so the rest of the app imports from it as
before.

The Form.io components are a separate package, `@myss/formio`
(`Apps/MyssFormio`).

## How it is consumed

As TypeScript source: `package.json` `exports` point at `src/`, and there is no
build step. Each app's Vite build compiles it, and each app's `tsc -b`
type-checks the files it imports. React, react-router, the OIDC libraries,
TanStack Query and the design system are peer dependencies, so each app's copy
is the one used.

Inside the package, imports are relative (no `@/` alias), and nothing reads an
app's runtime config or routes.

## Scripts

Run from `Apps/` with `-w @myss/web-common`, or from this directory.

| Script                          | Purpose                               |
| ------------------------------- | ------------------------------------- |
| `npm run build`                 | Type-check (`tsc -b`), tests included |
| `npm run lint`                  | Run ESLint                            |
| `npm run test:unit`             | Unit tests (Vitest, node)             |
| `npm run test:browser`          | Browser tests (Vitest + Playwright)   |
| `npm run test:browser-headless` | Browser tests, headless               |
