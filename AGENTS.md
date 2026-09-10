# AGENTS.md

This file provides guidance to AI coding agents working with code in this repository.

## What this is

My Self Serve (MySS) — a rebuild of the BC income/disability assistance self-serve
portal (bcgov). Three deployable apps under `Apps/`, plus ADRs under
`Docs/Architecture/DecisionRecords/` and a local Docker stack at `compose.yaml`.

| App | Stack | Role |
| --- | --- | --- |
| `Apps/MyssApi` | C# / ASP.NET Core .NET 10 | The API. Modular monolith; namespace `Myss.Api` |
| `Apps/MyssWebclient` | React 19 + Vite + TS | SPA; BC Gov Design System; Form.io renderer |
| `Apps/MyssContent` | Strapi 5 | Content engine; owns versioned Form.io specs |
| `Apps/IcmApi` | C# class library + Refit | Typed client for ICM (Siebel); namespace `Icm.Api` |
| `Apps/IcmApi.Console` | C# console app | Hand-run functional test against a real ICM |

## Secrets: do not read them

**Never read, print, or otherwise consume the developer's secrets.** They exist so that
credentials stay out of the repository; an agent that reads one puts it straight back into
a transcript, a log, or a summary. This is a hard rule, not a default to weigh against
convenience.

Off limits:

- The .NET user-secret store — `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`,
  used by `Apps/IcmApi.Console`.
- `dotnet user-secrets list` / `dotnet user-secrets get`, which print values.
- `appsettings.local.json` anywhere (gitignored; `Apps/MyssApi` uses one too), and any
  `.env` file.
- Environment variables holding credentials — the `Icm_*` and `Myss_*` prefixes, anything
  named `*Secret*`, `*Password*`, `*Token*`, `*ApiKey*`.

That the store *exists* is fine to check (`ls` a directory, read a `UserSecretsId` from a
csproj — the id is only a folder name and is committed on purpose). Reading the contents
is not.

**Verify by behaviour instead.** Nearly every question about a secret can be answered
without seeing it:

- *Did configuration load?* Run the tool and look at whether validation passed, or at the
  exit code — `IcmApi.Console` returns 1 for unusable settings, 2 for a failed call.
- *Are the credentials right?* The failure says so. A rejected client secret comes back as
  `{"error":"invalid_client"}` from the authorization server.
- *Is a value set?* Ask whether the run got past the placeholder check, not what the value
  is.

If a task genuinely cannot proceed without a credential, ask the user to set it
themselves — with `dotnet user-secrets set`, or by exporting an environment variable in
their own shell — rather than handling it. The same goes for writing one: never paste a
credential into a file, a command, or a commit.

## Commands

Whole local stack, one command (Aspire app host — containers, EF migrations, and the
three apps; dashboard URL printed at startup):

```bash
dotnet run --project Apps/MySS.AspireHost
```

Container data is in the same named volumes compose creates (`myss_postgres-data` etc.),
so the two paths share data; containers stop when the app host stops. Only one of
Aspire/compose can be up at a time (same host ports). All app host configuration comes
from `Apps/MySS.AspireHost/appsettings.json` plus `Aspire:Parameters:*` user secrets —
the `.env` files are for the compose path only. A clean checkout fails fast naming the
first missing secret; the values come from another developer or the team's central
secrets store (see README.md's "One command: Aspire"). Still manual either way: the
Strapi first-visit admin user, and the `Strapi:ApiToken` for MyssApi
(`appsettings.local.json`).

Local stack manually (from repo root):

```bash
cp Apps/MyssContent/.env.example Apps/MyssContent/.env   # once; committed values work locally
docker compose up -d --wait      # postgres, strapi, clamav, minio, minio-init
```

ClamAV's first start downloads signature databases (several minutes). `docker compose
down -v` wipes volumes; the Postgres init script and Strapi seed re-run on the next
`up`, and the EF migrations must be re-applied.

EF migrations are **not** applied automatically — two DbContexts, both required:

```bash
cd Apps/MyssApi
dotnet tool restore                                       # dotnet-ef, pinned in .config/dotnet-tools.json
dotnet ef database update --context FormsDbContext
dotnet ef database update --context AttachmentsDbContext
```

(Adding a migration: see `Apps/MyssApi/AGENTS.md`.)

Run:

```bash
cd Apps/MyssApi && dotnet run                 # http://localhost:5000, Swagger at /swagger
cd Apps/MyssWebclient && npm run dev          # http://localhost:5173
                                              # Strapi admin: http://localhost:1337/admin
```

Test:

```bash
dotnet test Apps/MyssApi.Tests
dotnet test Apps/MyssApi.Tests --filter "FullyQualifiedName~FormSpecValidatorTests"
dotnet test Apps/MyssApi.Tests --filter "DisplayName~rejects"

cd Apps/MyssWebclient
npm run test:unit                             # *.unit.test.ts, node env
npm run test:browser-headless                 # *.browser.test.tsx, needs `npx playwright install chromium`
npx vitest --config=vitest.config.ts --project=unit src/auth/decodeJwt.unit.test.ts
npm run lint && npm run format:check

cd Apps/MyssContent && npm test               # vitest run
```

CI (`.github/workflows/tests-dev.yml`, on PRs into `dev`) runs the API tests and both
webclient suites. `dev` is the main branch.

After changing an API contract, regenerate the typed client: with the API running,
`cd Apps/MyssWebclient && npm run generate:schema` (writes `src/api/generated/`, never
hand-edit it).

## Architecture

### Decisions that constrain new code (see `Docs/Architecture/DecisionRecords/`)

- **ADR-0001** — event sourcing is the default state model for user-driven processes
  (immutable typed events, state folded from the log, no mutable status column as the
  source of truth). Temporal is reserved for automated/queue-like orchestration
  (promotion saga, bulk dispatch). When ambiguous, default to event sourcing.
- **ADR-0002** — modular monolith. Modules talk only through published module-APIs
  (read models, not table rows); no cross-schema joins. Enforcement mechanism is still
  open, so keep cross-module call sites injectable, and record any cross-module
  dependency as a new ADR.
- **ADR-0003** — C#/.NET 10 with EF Core code-first. The EF model is the source of
  truth for schema; migrations are reviewed in the same PR as the model change.
- **ADR-0005** — no durable broker today. Publish notifications through one platform
  seam, never direct module-to-module calls.

An ADR is required for any cross-module dependency, any deviation from the target
architecture, and any three-options technology selection. Copy `TEMPLATE.md`, add a row
to the ADRs `README.md`, and review it in the same PR as the change it justifies.

### MyssApi

`Controllers → Services (I*Service) → Providers (I*Provider) → Data (EF DbContexts)`;
providers are the boundary to anything external, and **fail-closed startup on missing
config is deliberate** — preserve it. Layering, configuration precedence (`Myss_` env
prefix), auth (the Option 1 ↔ 2 swap point, MockAuthGate) and the attachments
pipeline: see `Apps/MyssApi/AGENTS.md`.

### Forms

Strapi owns Form.io specs as a `form-spec` collection keyed `(formSpecId, version)`;
published entries are immutable — a change is a new entry with `version + 1`, enforced
by `MyssContent/src/api/form-spec/content-types/form-spec/lifecycles.ts` delegating to
the pure rules in `src/lib/form-spec-rules.ts`. Strapi's bootstrap (`src/index.ts`)
revokes public read on form specs and seeds the POC forms on every boot (idempotent).

`MyssApi` reads specs through `StrapiFormSpecProvider` with a scoped read-only API token.
On submit, `FormsService` resolves **the version the client claims to have rendered**,
not the latest, and `FormSpecValidator` re-validates every value server-side —
client-side validation is UX only. Fields opt into domain rules through the Form.io
`properties` map (`{"myssValidator": "sin"}`, `{"myssMatches": "contactEmail"}`) or the
component type. Known gap: conditionally-required fields are exempt from the required
check.

### IcmApi

A client library for ICM (Siebel), layered so that Siebel's shape never leaves the
assembly: everything below the published `Models`/`Repositories`/`Services` surface
is `internal`, enforced by `InternalsVisibleTo` + `Contracts/PublishedSurfaceTests`
(ADR-0002's module-boundary rule). The measured wire truths (field names, date
formats), token caching, the `IcmApi.Console` functional test and its VPN
requirement: see `Apps/IcmApi/AGENTS.md`; `Apps/IcmApi/README.md` is the fuller
guide.

### Shared validation vectors

`Shared/validation/validation-vectors.json` is the contract between the C# and
TypeScript implementations of the same rules (SIN Luhn, email, confirmation match).
It is **linked**, not copied, into `MyssApi.Tests.csproj`; both suites read it, so a
divergence is a failing test. Adding a case means both suites must handle it. Every
value is synthetic — never add a real SIN, PHN or personal email. PHN vectors are
deliberately absent pending verification of the mod-11 spec.

## Conventions

- **C#**: block-scoped `namespace X { ... }` with `using` directives *inside* the
  namespace; XML doc comments on all public members (`GenerateDocumentationFile` is on
  and feeds Swagger). `Apps/.editorconfig` (870 lines) force-enables analyzer rules with
  `EnforceCodeStyleInBuild` — build warnings are the linter. Services use `_camelCase`
  private fields; the `Configuration/` host classes use `this.`-qualified fields.
- **Comments explain *why*, and record what was measured.** Several files carry
  observations verified against a running system on a date. Do not delete those; if you
  change the behaviour they describe, update the note.
- **Webclient**: see `Apps/MyssWebclient/AGENTS.md` — frontend architecture
  (pages/widgets/components hierarchy), token rules, BCDS-first components, test and
  runtime-config conventions — and the full docs under `Apps/MyssWebclient/Docs/`.
