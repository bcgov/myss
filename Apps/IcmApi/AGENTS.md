# AGENTS.md — IcmApi

Guidance for AI agents working in this library. The workspace-wide rules in the
repo root [AGENTS.md](../../AGENTS.md) apply in full. `Apps/IcmApi/README.md` is
the fuller guide — structure, wiring, and known pitfalls — and
`Apps/IcmApi/docs/integration/` holds the upstream specs it implements.
The REST service that hosts this library for MyssApi is `Apps/IcmApi.Host`
(see its AGENTS.md).

A client library for ICM (Siebel), layered so that Siebel's shape never leaves the
assembly. Namespaces are `Icm.Api` (Refit interfaces), `Icm.Api.Contracts`,
`Icm.Api.Models`, `Icm.Api.Repositories`, `Icm.Api.Services`, `Icm.Api.Workflows`,
`Icm.Api.Workflows.Contracts` — the `Api/` folder does not add a segment, since
`Icm.Api.Api` reads worse than it informs.

```
Services/            IServiceRequestService, IContactService, ICaseService, IBusPassService, IOAuthTokenService  ← inject these
Repositories/        IServiceRequestRepository, IContactRepository, ICaseRepository, IBusPassRepository, IOAuthTokenRepository
Api/                 IServiceRequestApi, IContactApi, ICaseApi, IOAuthTokenApi (Refit)  ┐
Api/Contracts/       Siebel* and Token* wire models, the mapper       │ internal
Workflows/           IBusPassWorkflowApi (Refit)                      │
Workflows/Contracts/ SiebelBusPass* wire envelope, BusPassMapper      ┘
Models/              the published models
```

`Api/` is direct REST over a business component; `Workflows/` calls Siebel workflow
processes that invoke other services behind them (the bus pass workflow matches the
contact and creates the service request itself). Both publish only through
`Models`/`Repositories`/`Services`. The bus pass mapping's vocabulary is MEASURED (SIT2
2026-09-03 and a ten-submission comparison against the old form on 2026-09-14): the
caller sends the SR classification triple (`SRType`/`SRSubType`/`SRSubSubType`, all
three or the upsert fails), the prospect `Purpose` comes from `FreeText`, and the old
path's `Alternate Phone #` is mirrored; a First Nations new application adds `Memo: "New
Application"` (what lets it file unmatched, as the old form's does), and the
leave-message consent travels as `AlternatePhone#`; what remains inference (whether a
real account number in the prospect `ClientId` matches, attachments) is listed in the
README's "The bus pass integration (INT-316)" section (INT-316 names MySS's integration, the caller — the workflow itself
is ICM's `ICM Receive Bus Pass Online Request Wrapper WF`). Live since 2026-09-10 (the
earlier `SBL-DAT-00825` BUS_PROC block is lifted), and two things are MEASURED from
those first submissions: the workflow's upsert requires non-empty caller-supplied keys
(`SRKey`/`ProspectKey`/`AttKey` — the mapper generates them, unique per submission),
and a business rejection still answers `Status: "SUCCESS"` with an `ApplicationNumber`
— the rejection is visible only in the filed SR (sub type `Error - Web`, reason in
`Memo`), so submit-and-read-back is the only real verification.
`IcmApi.Console --Mode=buspass` is that test (it creates a record in the target ICM).

## Contact search

`IContactService.SearchAsync(ContactQuery)` searches `data/ICMContact/ICMContact`
(`docs/integration/Contact_OpenApi.json`, fetched from SIT1's `describe` endpoint with
`IcmApi.Console --Mode=describe`). The criteria — ids, BCSC DID, SIN, PHN, names, birth
date, email, phones — are ANDed. Things that must survive a change, all MEASURED on SIT1
2026-09-17 and detailed in the README's "Contact search":

- **There is no raw `SearchSpec` on `ContactQuery`, and `ContactMapper.Vet` is a security
  control, not tidiness.** Values go into a `searchspec`; a double quote in one rewrites
  the search and returned other people's records. Never add a raw-expression property,
  never build a contact searchspec anywhere but `ContactMapper.ToSiebel`, and never
  loosen what `Vet` refuses without measuring an escape syntax. Blank values and
  criterion-less queries are refused for the same family of reasons.
- **Names and email use `~=` / `~LIKE`** because `=` and `LIKE` are case-sensitive.
  Wildcards come only from `ContactNameMatch`; a caller's `*` is refused.
- **No BCeID**: the component has no such field. Do not add a criterion that cannot be
  mapped.
- **The returned field list is fixed and minimal on purpose** — 20 of 80, none of the
  health or child-welfare fields. It does include SIN and PHN, so never log a `Contact`
  whole. `fields` takes
  live names (`Primary Email`), `searchspec` the document's (`Email Address`); a test ties
  `RequestedFields` to `SiebelContact`.
- **It returns a page.** For an identifying search, anything but exactly one item means
  "not identified" — never take the first.
- **Search only**: no contact write is declared, deliberately.
- A contact is all personal information. The console's `contact` mode prints criteria
  *names* and field *presence* unless `--Contact:ShowValues=true`; keep real DIDs, SINs
  and names out of committed settings, test data (use the synthetic values already in
  the tests) and transcripts.

## Case lookup

`ICaseService` reads `data/Cases/Case` (`docs/integration/Case_OpenApi.json`) and the
case's `Contact` child collection (no document exists for it). Things that must survive
a change, all MEASURED on SIT2 on 2026-09-24 and detailed in the README's "Case lookup":

- **`CaseQuery` has no raw `SearchSpec`**, for the contact search's reasons; values go
  through the shared `SiebelSearchValue.Vet`. Add a criterion as a property plus one line
  in `CaseMapper.ToSiebel`, never as an expression.
- **`ViewMode` defaults to `Manager`** (`CaseMapper.DefaultViewMode`), because ICM's
  default, and `Organization`, hide cases. Do not "simplify" it back to null.
- **The contact-to-case link is the key player**, three ways (`Key Player Id` = contact
  row id, `Key Player Contact Row Num` = person id, `Key Player Integration Id`). Nothing
  else on a case is searchable by contact, and the `Contact` child accepts no search.
- **Field lists are fixed and live-named.** `Created`, `Sales Rep`, `ICM Created By` and
  `ICM Updated By` are rejected in a `fields` list; the child-welfare fields on both
  components are deliberately not asked for. Tests tie each list to its wire contract.
- **The `Contact` child holds the BCeID** (`BCeID User Name`) and the SIN/PHN — a
  `CaseContact` is all personal information.
- **Reads only**: no case write is declared.

## The published surface is enforced

**Everything from `Api/` and `Workflows/` down is `internal`**, reachable only by
`IcmApi.Tests` through `InternalsVisibleTo` — ADR-0002 names exactly this as one of the
.NET readings of the module-boundary rule. A consumer physically cannot get at
`SiebelServiceRequest` or the Refit interfaces, so the mapping and the status-code
handling cannot be bypassed. `Contracts/PublishedSurfaceTests` pins the exported type
list, so widening the surface means saying so in that file rather than doing it with one
stray keyword.

## Each layer earns its place

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
  explanation but is not confirmed. The specs still supply read-only flags. Anything
  unmodelled lands in `ServiceRequest.AdditionalFields` as raw JSON rather than being
  dropped, which is how the mismatch was found; `--Output=raw` on the console app shows
  the untouched wire traffic (outgoing request bodies and every response). The four
  date fields are zone-less `DateTime`, not
  `DateTimeOffset`: the wire carries no offset and the value matches the Siebel UI
  verbatim.

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

## Token caching

Token caching is keyed on token URL + client id + **scope**, and deliberately not on the
secret — a narrower cached token served to a caller that asked for more scopes would fail
later at the resource server, and secrets do not belong in cache keys. Register the token
service as a singleton; per-request means no cache. A single-flight gate stops a
cold-cache burst from becoming one token request per caller, and failures are never
cached.

## IcmApi.Console — the functional test

`IcmApi.Console` reads `appsettings.json` (committed, placeholders) then the user-secret
store (`dotnet user-secrets set "Icm:Auth:ClientSecret" "…"` — keyed by the csproj's
`UserSecretsId`) then `Icm_` environment variables then the command line, gets a token,
runs one search against a real ICM and dumps the result (`--Mode=query`, the default;
`buspass` submits and reads back, `contact` searches contacts, `case` reads a case and
the people on it, `describe` saves a resource's OpenAPI document). The token endpoint is composed
from `Icm:Auth:BaseUrl` + `Icm:Auth:Realm` (both non-secret, both committed) so the realm
is a visible setting rather than a path segment inside a pasted URL; an optional
`Icm:Auth:TokenUrl` overrides both. Nothing in the unit suite touches the network, so
this is the only thing that can confirm the date format and the `ViewMode` default
against SIT. Exit codes: 0 success, 1 bad settings, 2 failed call.

**It needs the ministry VPN.** The direct Siebel hosts (`*.icm.gov.bc.ca`) are internal
and do not resolve in public DNS, so a run against them dies at DNS lookup (`nodename
nor servname provided`). The API gateway (`icmsit2.api.gov.bc.ca`) resolves publicly
but rejects a non-allowlisted source IP with `403` and a body naming it
(`IP address not allowed: …`) — MEASURED 2026-09-10. The token endpoint
`*.loginproxy.gov.bc.ca` *is* public, so a run that gets a token and then fails on the
ICM call is the signature of a VPN that is down — not a credentials problem. Building and `dotnet test Apps/IcmApi.Tests` need neither VPN nor credentials.

## Tests

Tests mirror the layers: `Contracts/` covers the wire contract and the mapper, `Api/`
asserts on real `HttpRequestMessage`s through a recording handler (Refit generates its
implementation at compile time, so a dropped query parameter is otherwise invisible),
`Repositories/` runs the real Refit stack over canned responses, and `Services/` uses
fakes to count round trips.
