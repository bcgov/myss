# AGENTS.md — MyssAdminWebclient

Guidance for AI agents working in the admin app. The workspace-wide rules in
the repo root [AGENTS.md](../../AGENTS.md) and the conventions in the citizen
webclient's [AGENTS.md](../MyssWebclient/AGENTS.md) (dependency rule, tokens,
BCDS first, accessibility, test naming, runtime config) apply in full; this file
adds what is specific to the admin app.

- **IDIR only.** Every route but `/auth/callback` and `/signed-out` sits behind
  `RequireIdir`. Logout must land on `/signed-out` (unguarded), and
  `RequireIdir` starts no sign-in while `isLogoutInProgress()`; together they
  keep a logout from silently signing the user back in.
  The session comes from the id token alone (`auth/useSession.ts`). Roles are
  asked for only where they matter: `auth/useRoles.ts` reads MyssAdminApi's
  `/auth/me`, and `RequireWorker` (inside `RequireIdir`) gates the review pages
  on WORKER or ADMIN. Authorization is the API's job (`AdminIdir`,
  `WorkerWithIdir`); the app's checks only decide what to show.
- **One API.** Every call goes to MyssAdminApi (`API_URL`) with
  `authHeaders()`. Do not call the citizen MyssApi from here.
- **Form.io comes from `@myss/formio`** (`Apps/MyssFormio`), the same code the
  citizen app renders with. Change components there, not here.
- **Shared with the citizen app through `@myss/web-common`** (`Apps/MyssWebCommon`):
  the access token, OIDC settings, SiteMinder sign-out, idle timeout, `/auth/me`
  query, sign-in callback page, runtime settings (`constants.ts`), `StatusPill`,
  `AnnouncementBanner`, and the API shapes both APIs answer with (form specs and
  the 422 error list, the application status and refusal, the rate table).
  `ReadOnlySpecForm` comes from `@myss/formio`. The files in `auth/` that remain
  bind the shared code to this app (IDIR hint, `/signed-out`, MyssAdminApi);
  change the shared behaviour in the package, not here.
- **`npm run build` is the only full type-check** (`tsc -b`); run it before
  considering a change done.
- Install from `Apps/` (npm workspace root, one lockfile).

## Layout of `src/`

`pages/` (the landing page, form management, the editor, the rates panel,
the application review queue and review page) · `hooks/` (`useForms`,
`useEstimatorRates`, `useReview`) · `api/` (raw fetches to MyssAdminApi; no
generated client) · `auth/` · `lib/` (rate column metadata) · `routes/` (`paths.ts`,
every URL) · `widgets/review/` (queue, application, history, notes) ·
`constants.ts` (runtime config).
