# AGENTS.md — MyssApi

Guidance for AI agents working in this app. The workspace-wide rules in the
repo root [AGENTS.md](../../AGENTS.md) apply in full (secrets policy, ADRs,
C# conventions, commands); this file adds what is specific to MyssApi.

## Layering

`Controllers → Services (I*Service) → Providers (I*Provider) → Data (EF DbContexts)`.
Providers are the boundary to anything external (Strapi, ClamAV, S3, ICM); services
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

## Auth

Option 1 (current): the API is a stateless resource server validating Keycloak bearer
tokens; the SPA does Auth Code + PKCE. `StartupConfiguration.ConfigureAuthentication`
is the documented swap point to Option 2 (BFF cookie + OIDC) — the lines marked SHARED
move across verbatim and nothing outside that method changes. Keep it that way.

`Configuration/KeycloakClaims` flattens Keycloak's nested `realm_access`/`resource_access`
roles so `Configuration/AuthorizationPolicies` (`Client`, `Worker`, `Admin`) works
identically under either option. The worker endpoints (application review, stored
submissions, the bus pass PDF) and their `WorkerWithIdir` policy moved to MyssAdminApi;
it copies `KeycloakClaims` and `RoleCalculator`, so a role means the same in both.

`Configuration/MockAuthGate` is a three-lock, fail-closed dev sign-in
(`AllowMockAuth` + `MockAuth` + a non-production `EnvironmentName`, all explicit).
A production-named environment with either flag set throws at startup. Enable it via
`appsettings.local.json` (see `appsettings.local.sample.json`); pick a persona with
`MockAuthPersona` or the `X-Mock-Persona` header.

## Attachments

`validate → insert quarantined row → ClamAV INSTREAM scan → object store → release`.
The row is written before the scan on purpose, so a crash leaves a findable quarantined
row instead of an orphaned object; a flagged file keeps its row as an audit record and
its content never reaches the store. `Attachments:MaxSizeBytes` must stay at or below
clamd's `StreamMaxLength`.

## EF migrations

Four DbContexts, one per schema, all required, applied manually (see the root
AGENTS.md for the stack-bootstrap commands). Each has its own history table in its
schema and its own migrations folder.

The forms, platform and intake contexts, their entities, the event store and the
intake fold are defined in `Apps/MyssData`, which MyssAdminApi shares; only
`AttachmentsDbContext` is defined here. Their migrations stay in this project: this API
is the only service that changes the schema. EF looks for migrations in the context's
own assembly by default, so every place this API builds one of those contexts points it
here through `Configuration/MyssMigrations` (Startup, `MigrationRunner`, the Postgres
tests); `Configuration/MyssMigrationsTests` fails if one does not. A model change in
MyssData needs its migration added here, in the same PR. Adding a migration:

```bash
cd Apps/MyssApi
dotnet tool restore    # dotnet-ef, pinned in .config/dotnet-tools.json
dotnet ef migrations add <Name> --context FormsDbContext
dotnet ef migrations add <Name> --context AttachmentsDbContext --output-dir Migrations/Attachments
dotnet ef migrations add <Name> --context PlatformDbContext --output-dir Migrations/Platform
dotnet ef migrations add <Name> --context IntakeDbContext --output-dir Migrations/Intake
```

The EF model is the source of truth for schema (ADR-0003); migrations are reviewed in
the same PR as the model change.

**Migrate mode.** `MyssApi --migrate`, or the env var `Myss_MigrateAndExit=true`
(`Configuration/MigrationRunner`), applies every
pending migration of every context in schema order and exits 0, or 1 at the first
failure. It builds a host without `Startup`, so it needs only the connection strings,
not the object-storage and ICM settings the API fails closed without. The deployment
pipeline runs it as a Kubernetes Job from the promoted image, between the retag and
the rollout restart (gitops `t-migrate-db`, ADR-0009). Migrations stay additive; a
destructive change is two releases (expand, then contract).
