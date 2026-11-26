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
| `/form-management/:formSpecId/from-json` | Save pasted JSON as a new form or a new version |
| `/form-management/:formSpecId/messages` | One form's messages                    |
| `/form-management/:formSpecId/messages/:messageKey` | Edit one form message       |
| `/applications`                | Applications to review (WORKER or ADMIN role)   |
| `/applications/:id`            | One application: pick up, decide, notes         |
| `/worker-override`             | Start an application for a client (workers)     |
| `/worker-override/drafts`      | Every worker's unsubmitted override drafts      |
| `/worker-override/:id`         | One worker override application                 |
| `/support/clients`             | Support View client search (workers)            |
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

**Form messages.** The form messages pages edit the published message
catalogue that the citizen webclient shows (`api/formMessages.ts`,
`hooks/useFormMessages.ts`), one form at a time.

**Repeat sections.** In **Build form**, the *Content and layout* group offers
**Repeat section**: a group of questions asked once per unit of a number
answer, such as one "Child Info" group per child. Add the number question
first, then the section, and drag the group's questions into it. Its settings
are **Heading for each entry** (shown above each group), **Count question**
(chosen from the form's number questions outside any repeat section) and
**Most entries** (1 to 50, default 10). For citizens, the section shows only
while its count question does; the count must be a whole number from 1 to
the most entries; lowering it removes the last groups, and raising it again
brings their answers back. **Save draft** and **Publish** refuse a section
without a count question, a count question that is not a number question, is
hidden or sits inside a repeat section, a maximum outside 1 to 50, and a
repeat section inside another. The answers are a list, one object per group,
under the section's key.

**Channel rules.** A channel is how an application was started: **Client**
(the applicant started it) or **Worker override** (a worker started it for a
client who cannot submit their own). Channel rules apply to applications for
assistance only; other forms ignore them. In **Build form**, the settings of
every question, content block and page have a **Channel rules** tab with one
group per channel: **Visibility**, where **Hidden** leaves the component, and
everything inside it, out of the form for that channel; and, for a question,
**Required**, where **Required** or **Optional** replaces Required on the
Validation tab for that channel. A choice left empty, or cleared with ×, reads
**As authored** and keeps the component as the other tabs set it. A repeat
section's count question offers no Required choice, because it is always
required. In **Edit labels**, a note under a row names its rules, such as
"Hidden for Worker override" or "Optional for Client", and **Preview as**
shows the form as one channel gets it: **Base form**, **Client** or **Worker
override**. **Save draft** and **Publish** refuse a component shown in a
channel that depends on one hidden in it (through its condition, the email it
confirms, a day's month and year, or a repeat section's count question), and,
in pasted JSON, a rule for an unknown channel, with a setting other than
`show` and `required`, or with a setting that is not `true` or `false`. In
pasted JSON, `required` on a component that takes no
answer, or on a count question, is accepted and has no effect.

- To leave a whole page out for a channel, hide the page itself. Hiding each of
  its fields leaves an empty page.
- On the worker override form (`worker-override-afa`), keep `firstName`,
  `lastName`, `email`, `phone` and `dateOfBirth` shown and required for Worker
  override, and keep `sin` shown. The staff API reads them to create the
  client's account, so a submit without one of the first five is refused.

**No error message catalogue in the preview.** The citizen webclient loads the published
error wording from `/v1/forms/error-messages` and the Form.io components use it
for rule failures. This app does not load it, so the preview words rule failures
with the compiled default messages. The fields, layout and rules are the same.

## Developer setup

- Install [Node.js](https://nodejs.org) (see the Dockerfile for the version).
- Run `npm install` from `Apps/`, the npm workspace root (one lockfile,
  `Apps/package-lock.json`, for all four workspaces: this app, the citizen
  webclient, `@myss/formio` and `@myss/web-common`).
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
| `npm run format:check`          | Check formatting without writing         |
| `npm run test:unit`             | Run unit tests (Vitest)                  |
| `npm run test:browser`          | Run browser tests (Vitest + Playwright)  |
| `npm run test:browser-headless` | Browser tests, headless                  |
| `npm run test:coverage`         | All tests, headless, with coverage       |

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
