# AGENTS.md — MyssApi

Guidance for AI agents working in this app. The workspace-wide rules in the
repo root [AGENTS.md](../../AGENTS.md) apply in full (secrets policy, ADRs,
C# conventions, commands); this file adds what is specific to MyssApi.

## Layering

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

## Auth

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

## Attachments

`validate → insert quarantined row → ClamAV INSTREAM scan → object store → release`.
The row is written before the scan on purpose, so a crash leaves a findable quarantined
row instead of an orphaned object; a flagged file keeps its row as an audit record and
its content never reaches the store. `Attachments:MaxSizeBytes` must stay at or below
clamd's `StreamMaxLength`.

## EF migrations

Two DbContexts, both required, applied manually (see the root AGENTS.md for the
stack-bootstrap commands). Adding a migration:

```bash
cd Apps/MyssApi
dotnet tool restore    # dotnet-ef, pinned in .config/dotnet-tools.json
dotnet ef migrations add <Name> --context FormsDbContext
dotnet ef migrations add <Name> --context AttachmentsDbContext --output-dir Migrations/Attachments
```

The EF model is the source of truth for schema (ADR-0003); migrations are reviewed in
the same PR as the model change.
