# AGENTS.md — MyssAdminApi

Guidance for AI agents working in this app. The workspace-wide rules in the
repo root [AGENTS.md](../../AGENTS.md) apply in full; this file adds what is
specific to the admin API.

## What it is

The API behind the admin webclient, in two parts:

- **Content** (`AdminIdir`: any authenticated caller with an IDIR identity): the form
  editor (list forms, open and save a draft, publish) and the eligibility rates
  editor (read the published table, save a new one). Strapi only.
- **Worker side** (`WorkerWithIdir`: the WORKER or ADMIN role and an IDIR identity):
  application review (`v1/intake/review/applications...`), stored submissions
  (`GET v1/forms/{id}/submissions`, `GET v1/forms/submissions/{id}`) and the bus pass
  PDF (`GET v1/bus-pass/submissions/{id}/pdf`, rendered by CDOGS).

`GET v1/auth/me` returns the caller with their effective roles, for the webclient to
choose which screens to show. Every route is the one MyssApi served before the split.

## Database

The worker side reads and appends through the shared data model in `Apps/MyssData`
(forms, platform and intake contexts, the event store, the intake fold), on the same
database and with the same connection-string keys as MyssApi (`ConnectionStrings:FormsDb`,
optional `PlatformDb` and `IntakeDb` falling back to it). **This API never migrates**:
no `Database.Migrate`, no `EnsureCreated`, no EF Design package. MyssApi owns the
migrations and its `--migrate` Job runs before either API restarts; a model change in
MyssData lands with its migration in MyssApi.

## Shared code

- `MyssFormSpec` (project reference) holds `FormSpecValidator` and the models it
  uses. The admin save and the citizen submit must run the same rules, so this
  code is shared, never copied.
- `MyssData` (project reference) holds the EF model both APIs use. Shared, never
  copied: two copies of a model over one schema would drift.
- `MyssCommon` (project reference) holds what both APIs must do the same way: the host
  setup (`StartupConfiguration`, `ProgramConfiguration`, logging, tracing, Swagger,
  CORS), `KeycloakClaims`, `RoleCalculator` (what stops a citizen-IDP token from
  keeping WORKER or ADMIN), mock auth (the same personas; this API defaults to
  `admin`), `CurrentUser`, the Strapi readers (`StrapiFormSpecProvider`,
  `EligibilityRateTableReader`), the rate table (`EligibilityRateColumns`,
  `FddRateData`) and `BusPassAnswers`. A change there changes both APIs. If you change
  the rate table shape, change MyssContent's schema with it.
- What differs stays here: the authorization policies, `/auth/me` (no registration
  fields), and `StrapiEligibilityRateProvider`, which reads through the shared reader
  but is not cached and returns a 502 instead of falling back.
- `MyssAdminApi.Tests` references MyssApi too (alias `citizen`), only for
  `TestSupport/CitizenAndStaffTestHost`: the review tests follow one file from a
  citizen's submit in MyssApi to a worker's decision here, over one in-memory copy of
  the shared schemas.

## Configuration

Same layering and `Myss_` env var prefix as MyssApi (the same `ProgramConfiguration`).
The only Strapi token this API holds is `Strapi:AdminApiToken` (write-scoped); MyssApi
keeps the read-only `Strapi:ApiToken`. The shared readers read with it too (it has find
and findOne): `Startup` registers `StrapiReadOptions` with this setting.
`AllowOrigins` lists the admin webclient's origins (`*` is refused). The bus pass PDF needs `Cdogs:BaseUrl`, `Cdogs:TokenEndpoint`,
`Cdogs:ClientId` and `Cdogs:ClientSecret` (checked at the first render, not at startup,
as in MyssApi before the move). Copy `appsettings.local.sample.json` to
`appsettings.local.json` for local dev; it listens on http://localhost:5010.

MyssApi caches the rate table it serves citizens for five minutes and cannot be
told about a save here, so a saved table reaches citizens within that window.
