# My Self Serve

"My Self Serve (MySS) provides online access to income and disability assistance for residents of British Columbia."

# Guidance for AI agents (and humans): the AGENTS.md hierarchy

Agent guidance is layered, and each layer is authoritative for its own
depth — deeper files add app specifics, they never restate what a level
above already says:

```
AGENTS.md                     workspace-wide rules: commands, architecture,
                              conventions, secrets policy (CLAUDE.md points here)
Apps/<App>/AGENTS.md          app-specific rules; links the app's full docs
Apps/<App>/Docs/*.md          the full docs (architecture, design principles,
                              accessibility, …) — each carries a status banner
                              saying whether it describes reality or a target
```

Today `Apps/MyssWebclient` has the app level built out
([AGENTS.md](Apps/MyssWebclient/AGENTS.md),
[Docs/](Apps/MyssWebclient/Docs/)); other apps add theirs as they grow
one. When guidance changes, update the layer that owns it — duplicated
rules drift.

# Local development

Prerequisites: Docker, the .NET 10 SDK, Node 22+.

## One command: Aspire

Everything below — the containers, the EF migrations, and all three apps — can be run
with a single command through the Aspire app host. Its configuration comes from
`Apps/MySS.AspireHost/appsettings.json` (non-secret defaults, committed) plus user
secrets for everything password-shaped, stored under `Aspire:Parameters:*` — the
`.env` files are for the compose path only. Get the secret values from another
developer or the team's central secrets store, and load them into the app host's
user-secret store once. A clean checkout without them fails fast at startup, naming
the first missing value. Then:

```bash
dotnet run --project Apps/MySS.AspireHost
```

It starts the compose containers' equivalents (Postgres 17, ClamAV, MinIO with its
bucket-creation one-shot), applies the four EF migration contexts once Postgres is healthy,
then starts `MySSApi` (http://localhost:5000), `MySSAdminApi` (http://localhost:5010),
`MySSContent` (Strapi, http://localhost:1337, run with npm on the host), `MyssWebClient`
(Vite, http://localhost:5173), `MyssAdminWebClient` (Vite, http://localhost:5174) and, when an
ICM base URL is configured (`Icm:BaseUrl` in the shared user-secret store), the ICM
middleware `IcmApi` (http://localhost:5100; see `Apps/IcmApi.Host/README.md`) — with a
dashboard (URL printed at startup) showing every resource's logs, health and telemetry.

Container data lives in named volumes (`myss_postgres-data`, `myss_clamav-db`,
`myss_minio-data`) and survives restarts; the containers themselves stop when the app
host stops. The names are exactly what `docker compose` creates for compose.yaml, so the
Aspire and compose paths share one set of data in either direction — but only one of the
two can be up at a time (same host ports).

Still manual, because they live inside Strapi's admin UI: the first-visit admin user,
the API token for `Strapi:ApiToken` (goes in `Apps/MyssApi/appsettings.local.json`), and
the write-scoped token for `Strapi:AdminApiToken` (goes in
`Apps/MyssAdminApi/appsettings.local.json`; `find`, `findOne`, `create` and `update` on
Form Spec and Eligibility Rate, never `delete`). Only the admin API holds the write token.
The admin API's bus pass PDF also needs the CDOGS credentials (`Cdogs:ClientId` and
`Cdogs:ClientSecret`) in the same file; without them only the PDF fails.
The token is read-only and scoped: it needs `find` and `findOne` on Form Spec,
Eligibility Rate and Error Message. A token created before the Error Message collection
existed lacks the last grant; MyssApi then logs a warning and words refused submissions
from its compiled defaults until the token is updated.

## Start the stack manually (compose)

Strapi reads its configuration from a gitignored `.env` — create it from the
example first (the committed values work as-is for local development):

```bash
cp Apps/MyssContent/.env.example Apps/MyssContent/.env
docker compose up -d --wait
```

This brings up Postgres 17, Strapi, ClamAV and MinIO (the ClamAV first start
downloads its signature databases, so `--wait` can take a few minutes; the
`minio-init` one-shot creates the `myss-attachments` bucket). The databases
initialize themselves on the first start:

- `myss` (the application database) is created by the Postgres image
  (`POSTGRES_DB`).
- `strapi` and its role come from
  `Infra/Development/Postgres/init/01-strapi-db.sql`. Init scripts only run
  when the data volume is empty — they do not re-run on restarts.
- Strapi applies its own schema migrations and seeds the POC form specs at
  boot.

The forms, attachments, platform and intake schemas in `myss` are **not**
automatic. Apply the EF migrations once (and again after pulling new
migrations):

```bash
cd Apps/MyssApi
dotnet tool restore
dotnet ef database update --context FormsDbContext
dotnet ef database update --context AttachmentsDbContext
dotnet ef database update --context PlatformDbContext
dotnet ef database update --context IntakeDbContext
```

or, equivalently, `cd Apps/MyssApi && dotnet run -- --migrate`, which applies
all four and exits. Deployed environments get their migrations from the
pipeline, which runs the same migrate mode as a Job before restarting the API.

The connection string in `appsettings.Development.json` already points at the
compose Postgres.

## Run the apps

- API: `cd Apps/MyssApi && dotnet run` → http://localhost:5000
- Admin API: `cd Apps/MyssAdminApi && dotnet run` → http://localhost:5010
- ICM middleware: `dotnet run --project Apps/IcmApi.Host` → http://localhost:5100
  (needs the `Icm:*` user secrets; see `Apps/IcmApi.Host/README.md`)
- Webclients: the two webclients and the two packages they share (`Apps/MyssFormio`,
  the Form.io components, and `Apps/MyssWebCommon`, sign-in and other common code) are
  one npm workspace, so install once from `Apps/` with `cd Apps && npm install`. Then:
  - Webclient: `cd Apps/MyssWebclient && npm run dev` → http://localhost:5173 (the
    forms demo is under `/techdemos/forms`)
  - Admin webclient: `cd Apps/MyssAdminWebclient && npm run dev` →
    http://localhost:5174 (IDIR sign-in; the form and rates editors, and, for the
    WORKER or ADMIN role, the applications to review)
- Strapi admin: http://localhost:1337/admin — the first visit asks you to
  create the initial admin user (local account, any credentials)

## Tests

- ClamAV: `./test-clamav.sh` — confirms the local clamd answers and actually
  detects (EICAR via INSTREAM, the same protocol the API uses). Run it when
  attachment scanning misbehaves; on a first-ever ClamAV start it will say the
  daemon is not answering until the signature download finishes.
- API: `dotnet test Apps/MyssApi.Tests`
- Shared libraries and admin API: `dotnet test Apps/MyssFormSpec.Tests`,
  `dotnet test Apps/MyssCommon.Tests` and `dotnet test Apps/MyssAdminApi.Tests`
- ICM client and middleware: `dotnet test Apps/IcmApi.Tests` and
  `dotnet test Apps/IcmApi.Host.Tests`
- Webclients and the shared packages: in `Apps/MyssWebclient`, `Apps/MyssAdminWebclient`,
  `Apps/MyssFormio` and `Apps/MyssWebCommon`, `npm run test:unit` and
  `npm run test:browser-headless` — the browser tests need a one-time
  `npx playwright install chromium`

## SonarQube Cloud

Non-draft pull requests from this repository targeting `dev` run four SonarQube Cloud
analyses, each with its own quality gate. Fork pull requests skip analysis because they
cannot access the `SONAR_TOKEN` secret.

| Project         | Key                               |
| --------------- | --------------------------------- |
| MySS API        | `bcgov-sonarcloud_myss_api`       |
| MySS Web Client | `bcgov-sonarcloud_myss_webclient` |
| MySS Content    | `bcgov-sonarcloud_myss_content`   |
| MySS ICM Client | `bcgov-sonarcloud_myss_icmapi`    |

The code split out of the API and the web client is analysed under its origin project
until it has its own: MySS API also covers `MyssFormSpec`, `MyssData`, `MyssCommon` and `MyssAdminApi`, and MySS Web
Client also covers `MyssAdminWebclient`, `MyssFormio` and `MyssWebCommon`.

The workflow imports OpenCover reports from the .NET tests and LCOV reports from the
TypeScript test suites. Configure the `SONAR_TOKEN` GitHub Actions secret for the
`bcgov-sonarcloud` organization, and disable Automatic Analysis for all four projects
so the CI analyses are the single source of results.

## Reset

`docker compose down` keeps the data. `docker compose down -v` wipes it — the
next `up` re-runs the init script and the Strapi seed; re-apply the EF
migrations afterwards.

# Releases

A release is the set of six images deployed together, named
`v<major>.<sprint>.<n>`. `version.json` holds major and sprint; `n` counts the
releases published for that sprint, starting at 0.

1. **Every merge to `dev`:** Tekton builds only the apps whose code changed,
   tags each image with its 7-character commit, and deploys it to dev. The
   folders that rebuild each app are listed in `.github/scripts/release.sh`.
2. **Draft release:** the **Release draft** workflow keeps one draft on the
   Releases page, already named with the next version. Its `release` block
   lists the build each app would ship (its latest dev build) and whether it
   changed since the last release. To ship a different build of an app, edit
   the draft: put another commit or an earlier release (`v0.7.2`) on its
   line, and add `# pinned` to keep it when new builds land.
3. **Test:** a repository admin runs **Deploy release** (Actions tab) with
   `test`. One approval. It tags the listed images `v0.7.4`, promotes them,
   and publishes the draft as a pre-release with the exact list deployed.
   Name a published release in the `release` field to redeploy it.
4. **Prod:** the same workflow with `prod`. It deploys the newest
   pre-release, which must be what test is running, needs two approvers other
   than the requester, and marks the release as the latest.

`myss-api` applies the database migrations for both APIs, so a release must
give `myss-api` and `myss-admin-api` builds with the same `Apps/MyssData`; the
deploy refuses otherwise. A hotfix is a normal PR to `dev`, then a release. Setup
(environments, reviewers, the deployer token) is described at the top of
`.github/workflows/deploy.yml`.

# Previous work

## Reference Prototypes

https://github.com/bcgov/myss-web
https://github.com/bcgov/myss-api

## Current running app

https://myselfserve.gov.bc.ca/
