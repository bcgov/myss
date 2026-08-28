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

Local stack (from repo root):

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
dotnet ef migrations add <Name> --context AttachmentsDbContext --output-dir Migrations/Attachments
```

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

### MyssApi layering

`Controllers → Services (I*Service) → Providers (I*Provider) → Data (EF DbContexts)`.
Providers are the boundary to anything external (Strapi, ClamAV, S3, CDOGS); services
hold the rules; everything is registered by interface in `Startup.ConfigureServices` so
tests substitute fakes (`MyssApi.Tests/TestDoubles/`) rather than mocking HTTP.

`Program.cs` is a thin shim over `Configuration/ProgramConfiguration` +
`Startup`/`Configuration/StartupConfiguration` (the old-style two-class host, not
minimal APIs). Configuration precedence: `appsettings.json` →
`appsettings.{Environment}.json` → `appsettings.local.json` (gitignored) →
environment variables **prefixed `Myss_`** (`Myss_Strapi__ApiToken`,
`Myss_ObjectStorage__Bucket`, …).

Endpoints are versioned: `[Route("v{version:apiVersion}/...")]`, responses wrapped in
`BaseResponseModel<T>`.

**Fail-closed startup is deliberate.** `ObjectStorage` missing → the app refuses to
start. `Strapi:ApiToken` is not defaulted. Preserve that shape rather than adding
permissive fallbacks.

### Auth

Option 1 (current): the API is a stateless resource server validating Keycloak bearer
tokens; the SPA does Auth Code + PKCE. `StartupConfiguration.ConfigureAuthentication`
is the documented swap point to Option 2 (BFF cookie + OIDC) — the lines marked SHARED
move across verbatim and nothing outside that method changes. Keep it that way.

`Configuration/KeycloakClaims` flattens Keycloak's nested `realm_access`/`resource_access`
roles so `Configuration/AuthorizationPolicies` (`Client`, `Worker`, `Admin`,
`WorkerWithIdir`) works identically under either option.

`Configuration/MockAuthGate` is a three-lock, fail-closed dev sign-in
(`AllowMockAuth` + `MockAuth` + a non-production `EnvironmentName`, all explicit).
A production-named environment with either flag set throws at startup. Enable it via
`appsettings.local.json` (see `appsettings.local.sample.json`); pick a persona with
`MockAuthPersona` or the `X-Mock-Persona` header.

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
assembly. `Apps/IcmApi/README.md` is the fuller guide — structure, wiring, and the
gotchas — and `Apps/IcmApi/docs/integration/` holds the upstream specs it implements. Namespaces are `Icm.Api` (Refit interfaces), `Icm.Api.Contracts`,
`Icm.Api.Models`, `Icm.Api.Repositories`, `Icm.Api.Services` — the `Api/` folder does not
add a segment, since `Icm.Api.Api` reads worse than it informs.

```
Services/      IServiceRequestService, IOAuthTokenService   ← inject these
Repositories/  IServiceRequestRepository, IOAuthTokenRepository
Api/           IServiceRequestApi, IOAuthTokenApi (Refit)   ┐ internal
Api/Contracts/ Siebel* and Token* wire models, the mapper   ┘
Models/        the published models
```

**Everything from `Api/` down is `internal`**, reachable only by `IcmApi.Tests` through
`InternalsVisibleTo` — ADR-0002 names exactly this as one of the .NET readings of the
module-boundary rule. A consumer physically cannot get at `SiebelServiceRequest` or the
Refit interfaces, so the mapping and the status-code handling cannot be bypassed.
`Contracts/PublishedSurfaceTests` pins the exported type list, so widening the surface
means saying so in that file rather than doing it with one stray keyword.

Each layer earns its place:

- **Api + Contracts** speak Siebel: spaced field names, `"Y"`/`"N"` flags,
  everything a nullable string, `items` an array on a read and an object on a write.
  Refit methods return `IApiResponse<T>` because the status code is the only thing
  separating "found nothing" from a real failure.
- **Repositories** are the published data-access boundary. They map, and they turn ICM's
  status codes into terms a caller can use: missing is `null` or an empty page (ICM says
  204 on some operations and 404 on others), `304` is "nothing changed", and anything
  else throws `ApiException`. They take a bearer token as a parameter, because ICM applies
  the calling identity's Siebel visibility to every read and write. The second half of that
  identity is `X-ICM-TrustedUserName`, naming the ICM user a call acts as — configured once
  on `ServiceRequestRepository` from `Icm:TrustedUserName` (a secret), and omitted entirely
  rather than sent empty when there is none.
- **Services** add behaviour. `OAuthTokenService` is pure caching over
  `IOAuthTokenRepository`; `ServiceRequestService` ties that token to the repository so
  callers deal in service requests and never in tokens. Reach for a repository directly
  only when the caller already holds a token of its own.
- **Models** are worth the mapping only because they are better than the wire. Every
  property is typed from the spec's `x-siebel-datatype`: `Y`/`N` is `bool?`, and the
  three Siebel date types stay distinct — `DTYPE_UTCDATETIME` is a `DateTimeOffset`,
  `DTYPE_DATETIME` a zone-less `DateTime`, `DTYPE_DATE` a `DateOnly`. (The record has no
  numeric fields at all; the only integers in the spec are `PageSize`/`StartRowNum`.)
  `ServiceRequest` (read, `init`-only) is also split from `ServiceRequestInput` (the 34
  fields ICM will actually accept), so setting a Siebel-calculated field is a compile
  error rather than a silently ignored one.

  **Field names come from real responses, not from the OpenAPI documents** — MEASURED on
  2026-08-28, they disagree on 27 of 51 fields (`SR Number` vs `Service Request Number`,
  `SR Type` vs `Type`). Not an environment difference: `docs/integration/` holds both the
  SIT1 and SIT2 documents and they are identical bar `CP Outcome`, with neither using a
  single live name. Both describe the direct Siebel host (`*-ai2.icm.gov.bc.ca:8443`) while
  the client calls the API gateway (`icmsit2.api.gov.bc.ca`), which is the likeliest
  explanation but is not confirmed. The specs still supply read-only flags. Anything unmodelled lands
  in `ServiceRequest.AdditionalFields` as raw JSON rather than being dropped, which is how
  the mismatch was found; `--Output=raw` on the console app shows the untouched response.
  The four date fields are zone-less `DateTime`, not `DateTimeOffset`: the wire carries no
  offset and the value matches the Siebel UI verbatim.

  Dates come back as `MM/DD/YYYY HH:MM:SS` — Siebel's display format, MEASURED against SIT
  on 2026-08-28. The vendor's date-format page specifies ISO 8601 but describes a different
  connector; both shapes are accepted on reads, writes use the observed one and are
  untested. Month-first is established by evidence (`03/28/2016`, `06/17/2026`,
  `08/28/2026` — second component above 12), not assumed. An unrecognised shape still lands
  in `ServiceRequest.UnparsedValues` with the raw text, which is how the format was caught.

  `DTYPE_DATE` → `DateOnly` is load-bearing, not cosmetic: the same Oracle page warns that
  a date defaulting to midnight UTC shifts to the previous day in Western Hemisphere zones,
  which is every zone this runs in. The date is read exactly as written and never zone-
  converted.

Token caching is keyed on token URL + client id + **scope**, and deliberately not on the
secret — a narrower cached token served to a caller that asked for more scopes would fail
later at the resource server, and secrets do not belong in cache keys. Register the token
service as a singleton; per-request means no cache. A single-flight gate stops a
cold-cache burst from becoming one token request per caller, and failures are never
cached.

`IcmApi.Console` is the functional test: it reads `appsettings.json` (committed,
placeholders) then the user-secret store (`dotnet user-secrets set "Icm:Auth:ClientSecret"
"…"` — keyed by the csproj's `UserSecretsId`) then `Icm_` environment variables then the
command line, gets a token, runs one search against a real ICM and dumps the result. The token endpoint
is composed from `Icm:Auth:BaseUrl` + `Icm:Auth:Realm` (both non-secret, both committed) so
the realm is a visible setting rather than a path segment inside a pasted URL; an optional
`Icm:Auth:TokenUrl` overrides both. Nothing in the unit
suite touches the network, so this is the only thing that can confirm the date format and
the `ViewMode` default against SIT. Exit codes: 0 success, 1 bad settings, 2 failed call.

**It needs the ministry VPN.** `*.icm.gov.bc.ca` is internal and does not resolve in
public DNS, so without it the run dies at DNS lookup (`nodename nor servname provided`).
The token endpoint `*.loginproxy.gov.bc.ca` *is* public, so a run that gets a token and
then fails on the ICM call is the signature of a VPN that is down — not a credentials
problem. Building and `dotnet test Apps/IcmApi.Tests` need neither VPN nor credentials.

Tests mirror the layers: `Contracts/` covers the wire contract and the mapper, `Api/`
asserts on real `HttpRequestMessage`s through a recording handler (Refit generates its
implementation at compile time, so a dropped query parameter is otherwise invisible),
`Repositories/` runs the real Refit stack over canned responses, and `Services/` uses
fakes to count round trips.

### Attachments

`validate → insert quarantined row → ClamAV INSTREAM scan → object store → release`.
The row is written before the scan on purpose, so a crash leaves a findable quarantined
row instead of an orphaned object; a flagged file keeps its row as an audit record and
its content never reaches the store. `Attachments:MaxSizeBytes` must stay at or below
clamd's `StreamMaxLength`.

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
- **Webclient**: path alias `@` → `src`; test files are `*.unit.test.ts` (node) or
  `*.browser.test.tsx` (Playwright/chromium) — the vitest projects select on those
  suffixes. CSS modules per component. Routes go in `src/routes/paths.ts`. Runtime
  config resolves `window.APP_CONFIG` (written by `entrypoint.sh` at container start) →
  `import.meta.env.VITE_*` → default; adding a value means touching `entrypoint.sh`,
  `public/config.js` and `src/constants.ts` together.
