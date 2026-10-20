# MySS Admin Web Client

Single-page front-end for **My Self Serve Administration**: the IDIR-only tools
ministry staff use to manage what citizens see in MySS, and the queue where
Ministry workers review submitted Income Assistance applications. It talks to
**MyssAdminApi** only. Built with React 19, Vite and TypeScript on the
[BC Government Design System](https://www2.gov.bc.ca/gov/content/digital/design-system),
like the citizen webclient.

## What it does

| Route                          | Page                                            |
| ------------------------------ | ----------------------------------------------- |
| `/`                            | Admin landing page: the list of admin tools     |
| `/form-management`             | The forms list, and starting a new form         |
| `/form-management/:formSpecId` | The form editor (and, for the estimator, rates) |
| `/applications`                | Applications to review (WORKER or ADMIN role)   |
| `/applications/:id`            | One application: pick up, decide, notes         |
| `/auth/callback`               | OIDC redirect target                            |
| `/signed-out`                  | Where sign-out and idle logout land             |

Every page except the callback and `/signed-out` needs an IDIR sign-in
(`auth/RequireIdir.tsx`). `/signed-out` is unguarded on purpose: a guarded
landing page would start IDIR sign-in at once and, with the IDIR session still
alive upstream, sign the user straight back in. Signing in again from there is
a button, never automatic.
Sign-in always goes straight to IDIR (`kc_idp_hint=azureidir`); there is no
provider chooser. The API enforces the same rule with its `AdminIdir` policy.

The application review pages also need the WORKER or ADMIN role
(`auth/RequireWorker.tsx`), read from MyssAdminApi's `GET /v1/auth/me`
(`auth/useRoles.ts`), the same rule as the API's `WorkerWithIdir` policy. The
landing page only lists "Applications to review" for those roles; an IDIR user
without them who opens the URL gets an explanation, not a redirect.

The editor's preview renders specs with the same Form.io components as the
citizen webclient, from the shared `@myss/formio` workspace
(`Apps/MyssFormio`).

**No error message catalogue.** The citizen webclient loads the published
error wording from `/v1/forms/error-messages` and the Form.io components use it
for rule failures. This app does not load it, so the preview words rule failures
with the compiled default messages. The fields, layout and rules are the same.

## Developer setup

- Install [Node.js](https://nodejs.org) (see the Dockerfile for the version).
- Run `npm install` from `Apps/`, the npm workspace root (one lockfile,
  `Apps/package-lock.json`, for this app, the citizen webclient and
  `@myss/formio`).
- Copy `.env.sample` to `.env`. `VITE_MYSS_API_URL` is MyssAdminApi
  (default `http://localhost:5010`).
- Run `npm run dev`. The dev server listens on <http://localhost:5174>, so it
  can run beside the citizen webclient on 5173. The SSO client needs
  `http://localhost:5174/*` as a valid redirect URI.

## Scripts

Run here, or from `Apps/` with `-w myss-admin-webclient`.

| Script                          | Purpose                                  |
| ------------------------------- | ---------------------------------------- |
| `npm run dev`                   | Start the Vite dev server                |
| `npm run build`                 | Type-check (`tsc -b`) and build for prod |
| `npm run preview`               | Preview the production build             |
| `npm run lint`                  | Run ESLint                               |
| `npm run format`                | Format with Prettier                     |
| `npm run test:unit`             | Run unit tests (Vitest)                  |
| `npm run test:browser`          | Run browser tests (Vitest + Playwright)  |
| `npm run test:browser-headless` | Browser tests, headless                  |

## Container image

The image builds from the workspace root, not this directory:

```bash
# from Apps/
docker build -f MyssAdminWebclient/Dockerfile -t myss-admin-webclient .
```

## Runtime configuration

The build is environment-agnostic. At container startup `entrypoint.sh` writes
`config.js` from these environment variables; they land on `window.APP_CONFIG`
and are read by `src/constants.ts`. During local dev, `src/constants.ts` falls
back to the corresponding `VITE_*` variables.

| Variable              | Value                                                      |
| --------------------- | ---------------------------------------------------------- |
| `MYSS_API_URL`        | MyssAdminApi's base URL (the name matches the citizen app) |
| `OIDC_AUTHORITY`      | The Keycloak realm URL                                     |
| `OIDC_CLIENT_ID`      | The SSO client the admin app signs in with                 |
| `CANADA_POST_API_KEY` | AddressComplete key for the preview's address field        |

The Canada Post key is visible in the browser by design, so deployed keys must
be restricted by valid URLs and a daily cap. Leave it empty to turn address
suggestions off in the preview.
