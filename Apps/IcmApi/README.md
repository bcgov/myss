# IcmApi

A .NET client library for **ICM** (the ministry's Siebel CRM), used by the rest of MySS
to read and write Siebel records without knowing anything about Siebel.

Built on [Refit](https://github.com/reactiveui/refit) 15.2.0. Targets `net10.0`, no other
dependencies.

## Prerequisites

- **.NET 10 SDK** — to build, and to run the unit tests.
- **The ministry VPN, connected** — for anything that actually talks to ICM.

ICM is reachable only when BC Gov VPN is running.

Building the library and running `IcmApi.Tests` need no VPN and no credentials — the whole
suite runs against canned responses, on purpose.

## What it covers today

| Upstream API | Spec | Published as |
| --- | --- | --- |
| Service Request (`data/ServiceRequest/ServiceRequest`) | [`docs/integration/SR_OpenApi.json`](docs/integration/SR_OpenApi.json) | `IServiceRequestService` / `IServiceRequestRepository` |
| Bus pass workflow (`workflow/ICM Receive Bus Pass Online Request Wrapper WF`) | [`docs/integration/BusPassWorkflow_OpenApi.json`](docs/integration/BusPassWorkflow_OpenApi.json) | `IBusPassService` / `IBusPassRepository` |
| OAuth 2.0 token endpoint (client credentials) | RFC 6749 §4.4 | `IOAuthTokenService` |

The token request uses **`client_secret_post`** (RFC 6749 §2.3.1) — `client_id` and
`client_secret` as form fields, no `Authorization` header. That is what the ministry's
existing integrations use.

`docs/integration/` holds ICM's own OpenAPI 3.0.1 documents for the Service Request
business component — `SR_OpenApi.json` (SIT1) and `SR_OpenApiSIT2.json` (SIT2) — kept in
the repository so a change upstream shows up in the same diff as the change here.

**The two are the same document.** 49 shared fields with identical read-only flags, Siebel
datatypes, lengths and required lists; the only differences are the server URL and
`CP Outcome`, which SIT1 declares and SIT2 does not. So the environment is not what makes
the field names disagree with the live endpoint — both documents disagree with it equally,
and not one of the live names appears in either.

The likely reason is the host. Both documents describe the **direct Siebel host**
(`sit2-ai2.icm.gov.bc.ca:8443`); this client calls the **API gateway**
(`icmsit2.api.gov.bc.ca`), which is a different address and appears to publish its own
field naming — friendlier (`Service Request Number`, `Type`, `Created Date`) and with
`Id` and `Row Id` added, neither of which is in any spec. That is inference, not
established fact: it could equally be a different Siebel integration object behind the
same path. Calling the direct host over the VPN, or asking whoever owns the ICM
integration which layer renames, would settle it — and would be worth doing before
trusting the specs for anything else, including the read-only flags the models still take
from them.

**It is not the source of the field names.** MEASURED against SIT on 2026-08-28 over 100
records: the document and the live endpoint disagree on 27 of the 51 fields. The document
says `SR Number`, ICM sends `Service Request Number`; the document says `SR Type`, ICM
sends `Type`. The models follow **what ICM actually sends**; the document is still the
source of which fields are read-only, and of the types for the handful of fields no record
has yet carried a value for.

The id/name pairs are the subtle ones: the document's `Created By` is a row id, which ICM
calls `Created By Id`, while ICM's `Created By` is the login name the document calls
`Created By Name`. Same for the Updated pair.

## Layout

```console
Api/                 Refit interfaces + settings — direct REST      internal
Api/Contracts/       Siebel and OAuth wire models, the mapper       internal
Workflows/           Refit interfaces — Siebel workflow endpoints   internal
Workflows/Contracts/ workflow wire envelopes, their mappers         internal
Models/              the published models                           public
Repositories/        data access — call, map, translate status      public
Services/            behaviour — token caching, authentication      public
docs/                the upstream specs this client implements
```

`Api/` and `Workflows/` are peers with one distinction. `Api/` is direct REST over a
business component: the caller names the record and the fields, and Siebel does exactly
that. `Workflows/` calls Siebel workflow processes, which call other services behind
them — the bus pass workflow matches or creates the contact, creates the service request
and files the transaction itself. The caller sends a message and gets an outcome, not a
record. Everything published still comes out through `Models/`, `Repositories/` and
`Services/`, so a consumer cannot tell which kind of endpoint served it — which is the
point.

Namespaces follow the folders, except that `Api/` adds no segment — `Icm.Api.Api` reads
worse than it informs. So: `Icm.Api`, `Icm.Api.Contracts`, `Icm.Api.Models`,
`Icm.Api.Repositories`, `Icm.Api.Services`, `Icm.Api.Workflows`,
`Icm.Api.Workflows.Contracts`.

**Everything from `Api/` and `Workflows/` down is `internal`.** `IcmApi.Tests` reaches it through
`InternalsVisibleTo`; nothing else can. That is what makes the boundary real rather than a
convention: a consumer cannot get at a Siebel wire model or a Refit interface, so it
cannot bypass the mapping or the status-code handling that make ICM's answers usable.
`Contracts/PublishedSurfaceTests` pins the exported type list, so widening the surface
means saying so there.

Each layer has one job:

- **Api + Contracts** speak Siebel. Fields named `Contact Cell #`, `"Y"`/`"N"` in place of
  booleans, dates as text, everything a nullable string, `items` an array on a read and an
  object on a write. Refit methods return `IApiResponse<T>` because the status code is the
  only thing separating "found nothing" from a real failure.
- **Repositories** are the data-access boundary: call, map, and turn ICM's status codes
  into terms a caller can use. They take a bearer token as a parameter.
- **Services** add behaviour. `OAuthTokenService` is pure caching over the token
  repository; `ServiceRequestService` supplies the token so callers never handle one.
- **Models** are typed from the spec, and are the reason the mapping is worth doing.

## Usage

Register the services once, then work in service-request terms:

```csharp
// Credentials for the ICM client this application authenticates as.
var credentials = new OAuthClientCredentials
{
    TokenUrl     = new Uri("https://dev.loginproxy.gov.bc.ca/auth/realms/…/token"),
    ClientId     = configuration["Icm:ClientId"],
    ClientSecret = configuration["Icm:ClientSecret"],
    Scopes       = ["read", "write"],
};

// One HttpClient per upstream host, from IHttpClientFactory.
services.AddHttpClient<IServiceRequestRepository, ServiceRequestRepository>(client =>
    client.BaseAddress = new Uri("https://sit1-ai2.icm.gov.bc.ca:8443/gov/v1.0"));

// The token cache must be a singleton — per-request means every request pays for a token.
services.AddSingleton<IOAuthTokenRepository>(_ => new OAuthTokenRepository());
services.AddSingleton<IOAuthTokenService, OAuthTokenService>();

services.AddScoped<IServiceRequestService>(provider => new ServiceRequestService(
    provider.GetRequiredService<IServiceRequestRepository>(),
    provider.GetRequiredService<IOAuthTokenService>(),
    credentials));
```

```csharp
public class Example(IServiceRequestService serviceRequests)
{
    public async Task<ServiceRequest?> OpenAsync(CancellationToken ct)
    {
        // Search. Nothing matched is an empty page, not an exception.
        // SearchSpec uses the OpenAPI document's field names; Fields uses the names the
        // live gateway answers with. The two disagree — see below.
        ServiceRequestPage page = await serviceRequests.SearchAsync(
            new ServiceRequestQuery
            {
                SearchSpec = "[Status] = \"Open\"",
                Fields     = ["Service Request Number", "Status", "Created Date"],
                PageSize   = 25,
            },
            ct);

        // Read one. Missing — or not visible to this caller — is null.
        ServiceRequest? existing = await serviceRequests.GetAsync("1-ABCDE", cancellationToken: ct);

        // Write. Only the properties you set are sent, so this changes one field.
        return await serviceRequests.UpdateAsync(
            "1-ABCDE",
            new ServiceRequestInput { Status = "Closed", RestrictedFlag = true },
            ct);
    }
}
```

Reach for `IServiceRequestRepository` directly only when the caller already holds a token
of its own — a request carrying a citizen's token, say, where a client-credentials token
would be the wrong identity entirely.

## The bus pass integration (INT-316)

Two names, two things: **INT-316 is MySS's integration** — the only MySS integration
that calls this workflow, named in the message header's `TransactionName` — and
**`ICM Receive Bus Pass Online Request Wrapper WF` is the workflow** it calls, which is
ICM's and serves other callers too. Calling the workflow "INT-316" conflates the caller
with the callee.

`IBusPassService.SubmitAsync` takes a `BusPassApplication` — the same facts the old MCP
`/BusPass` form captured, per the INT-316 field-mapping analysis — and posts it to
`workflow/ICM Receive Bus Pass Online Request Wrapper WF`, the REST receiver the retired
SOAP integration fed. The envelope's header carries the old `SetGenericHeader`
identities (`TransactionName: INT-316`, `SourceSystem: MCP`, `UserId: MCP_proxy`); the
bookkeeping fields the old integration sent as empty strings are omitted — MEASURED
SIT2 2026-09-10, submissions succeed identically without them (SR `1-11085048718`).
The applicant travels as one `SRProspects` row.

**A business rejection is a 200.** The workflow reports failure in its out-args
(`Error Code` / `Error Message`), not in the status code, and its status vocabulary is
undocumented — so `BusPassResult` carries the outcome whole and callers must check
`ErrorCode`. An HTTP-level failure still throws `ApiException`. A rejected match is not
even silent on the ICM side: the workflow files an SR of sub type `Error - Web` with the
reason in `Memo` (`Contact or Case Match not Found`, observed on stored records).

**What the workflow's own output establishes.** MEASURED against SIT2 on 2026-09-03, by
querying the SRs this workflow has been creating since 2022 (`Created By SIEBEL_EAI`,
`Comm Method Web` — including SR `1-11082491438` / row `1-53A894E`, created 2026-08-10)
and reading their `SRProspects` child rows over the gateway:

- The workflow classifies its SRs as `SR Type "Bus Pass"`, sub type **`Application` /
  `Change of Circumstance` / `Replacement`** (other sub types exist: `Application PWD`,
  `Card Replacement`, the error sub types — and `AANDC Online Request`, which MEASURED
  2026-09-10 is what the old form's **First Nations toggle** files as, row `1-53CBZL6`,
  accepted cleanly *without* a contact match), sub sub type
  **`One Address` / `Multiple Addresses`**, status `Ready`, priority `3-Standard`.
- One prospect row per address set. A single-address submission stores
  `Purpose: "Residence/Mailing"`; rows with `Purpose: "Residence"` appear on
  `Multiple Addresses` SRs. `Preferred Communication Method` holds **`Home Phone` /
  `Cell Phone` / `Email`** — the phone preference is qualified by which phone, not the
  old form's bare `Phone`. SIN and phones are stored as bare digits; the address's
  province lands in a field the gateway calls `State`.
- The prospect business component reads back with **spelled-out names** (`First Name`,
  `Social Security Number`, `Street Address`, `Birth Date`), not the integration
  object's abbreviations — the same two-namings situation the Service Request API has.
- Searchspec field names are the *spec's*, not the response's: `[SR Type]` works where
  `[Type]` matches nothing, `[Contact Last Name]` where `[Last Name]` errors, and
  `[Created] >= "MM/DD/YYYY"` comparisons work. `LIKE "*…*"` silently matches nothing.
- A clean old-path **Replacement** (SR `1-11086394388` / row `1-53CJWNO`, 2026-09-14,
  applicant matched to contact `1-30CFI36`) stores as sub type `Replacement`, sub sub
  type `One Address`, no `Memo`, prospect row `Purpose: "Residence/Mailing"` — and
  nothing on the SR or the prospect row records the form's replacement acknowledgement.
  The SR-level `Given Names`/`Last Name`/`Address` are the *matched contact's*, not the
  submitted ones. The prospect business component exposes 37 fields (empties included;
  read them with `--Query:ChildCollection=SRProspects` below); none is named for an
  acknowledgement, a leave-message consent or an applicant type — `Applicant Flag` is
  `N` on every row seen, the First Nations one included.
- A live submission through the **retired SOAP path** (SR `1-11085201468` / row
  `1-53BUC70`, 2026-09-03) stores in exactly the same shape, so both channels land on the
  same workflow. It also showed: a duplicate-case rejection still files the SR (sub type
  `Error - Web`, the reason in `Memo`) **and still returns that SR's number to the
  caller** — so an `ApplicationNumber` coming back is not by itself a success; the phone
  type selected on the form picks the stored field (Home was selected, `Home Phone #` is
  where the number landed — confirming this client's typed-field routing) though the
  number is *also* copied into `Alternate Phone #` (this client fills only the typed
  field — unknown whether the workflow or the old sender duplicates it); and the
  SR-level `Address` is the *matched contact's* address on file, not the submitted one.

**The upsert needs caller-supplied keys.** MEASURED against SIT2 on 2026-09-10, the
first live submissions through this client: with `SRKey` absent — or present but
empty — the workflow fails its `Create SR_Prospect_Att` upsert with `SBL-EAI-04397`
(`No user key can be used for the Integration Component instance 'Service Request'`),
returned as `WF_ERR_CUSTOM_1` in the out-args. With a non-empty `SRKey` (and
`ProspectKey`/`AttKey` on the child rows) the same submission succeeds — SR
`1-11085048654`, row `1-53BR2A6`. The mapper generates the keys from the submission
moment, unique per submission, so the upsert can only ever create a record, never
match and update an earlier one.

**A failed match reports SUCCESS in the out-args.** MEASURED on that same submission:
the applicant was the made-up "Myss IntegrationTest", no ICM contact matched — and the
out-args still came back `Status: "SUCCESS"` with an `ApplicationNumber` and empty
`Error Code`/`Error Message`. The rejection is visible only in the stored SR itself:
sub type `Error - Web`, the reason in `Memo` (`Contact or Case Match not Found`). So
the out-args establish only that the workflow accepted and filed the message; whether
the request actually attached to a client requires reading the SR back and checking
its sub type. `Error Code` catches workflow malfunctions (like the missing-key
failure above), not business rejections.

`BusPassMapper` sends that measured vocabulary. **MEASURED SIT2 2026-09-14, ten
submissions** (four through the old Test1 form, six through this client — see the
"Reference records" table in the console app's memory notes for row ids):

1. **The SR classification is the caller's.** A request-type word alone stores an SR
   with *no* sub type, *no* sub sub type and no prospect `Purpose`, even for a matched
   contact (SR `1-11086395524`), and a no-case contact then files as `Error - Web`.
   Sending `SRType: "Bus Pass"`, `SRSubType` (the request-type words) and
   `SRSubSubType` (`One Address` / `Multiple Addresses`) together files every scenario
   exactly as the old path does — Replacement `1-11086395541`, a new Application for a
   contact with no case `1-11086395558` (the old form's own filed the same way,
   `1-11086391783`), Change of Circumstance / Multiple Addresses `1-11086395579`. All
   three must travel together: `SR Sub Sub Type` is a bounded hierarchical picklist, and
   `One Address` sent without its parents fails the upsert (`SBL-EAI-04401`) and files
   nothing.
2. **`FreeText` feeds the prospect's `Purpose`.** `Role` — as `Residence/Mailing` or
   `Residential` — lands in no visible field and yields no `Purpose`; the submission that
   carried `FreeText: "Residence/Mailing"` stored exactly that (`1-11086395601`). So the
   mapper sends the purpose words in `FreeText` and does not send `Role`.
3. **The workflow keeps one prospect row.** The old form's differing-mailing-address
   submission (`1-53CJUMM`, `Multiple Addresses`) stores one row, `Purpose: "Residence"`;
   so did this client's two-row submission. The second row is still sent, since nothing
   else carries the mailing address; where it goes is not visible from the SR.
4. **Over 65 and Neither both file as plain `Application`** on the old path
   (`1-53CJUNB`, `1-53CJXFC`). Nothing on the stored SR or prospect distinguishes them,
   so they are not transmitted. **`FirstNations` is reproduced by the `Memo`.**
   `AANDC Online Request` as request type and sub type alone still filed an unmatched
   applicant as `Error - Web` (`1-53CJXME`); with `Memo: "New Application"` added, the
   same submission filed exactly like the old form's First Nations one — sub type
   `AANDC Online Request`, `Primary Contact Id: No Match Row Id`, that Memo
   (`1-53CJXMS` vs `1-53CBZL6`). The old path stores that Memo only on its First
   Nations SR, so the mapper sends it only then; it also lets a plain `Application`
   through unmatched (`1-53CJXN6`) — though so does no Memo at all (`1-53CJXO6`): a new
   application never needs a contact match, only the AANDC one did. A First Nations
   client who already holds a pass still gets the duplicate-case rejection
   (`1-53CJXNP`).
5. **The acknowledgements** leave no trace anywhere on the old path's records
   (`1-53CJWNO` replacement with acknowledgement) and are **not sent**. **The
   leave-message consent is `Alternate Phone #`:** across four old-form submissions
   made for this, the one with "leave messages" ticked stores the number there too
   (`1-53CJUMM`) and the three without do not (`1-53CJUNB`, `1-53CJXFC`, `1-53CJV62`),
   whatever the phone type; `AlternatePhone#` on the wire fills it (`1-11086395601`).
   The mapper does the same.
6. **The DOB write format is `MM/DD/YYYY`** — MEASURED 2026-09-14: `01/25/2000` sent,
   `Birth Date: 01/25/2000` stored (row `1-53CJXMK`).
7. **The account number matches the old path in failure only.** A replacement
   identified by a made-up account number and no SIN files identically on both paths —
   `Error - Web` / `Contact or Case Match not Found`, `No Match Row Id`, the number
   stored nowhere visible (`1-53CJV62` old, `1-53CJXOP` this client, which sends it as
   the prospect `ClientId`). Whether ICM matches a *real* account number sent that way
   needs a client with a known account number; the old SOAP payload carried it on a
   `Case` element, and the payload-level `ClientId` is the other candidate slot.
8. **Attachments** (`minItems: 1` in the spec) — still awaiting a live call that sends
   one.

**The authorization block is history.** The first attempt (2026-09-03) failed with
`403` `SBL-DAT-00825` — `Access to Resource 'ICM Receive Bus Pass Online Request
Wrapper WF' of type BUS_PROC is denied`; ICM granted the gateway client (`myss-api`
acting as `SIEBEL_EAI`) execute access, and live submissions have succeeded since
2026-09-10.

## Things worth knowing before you change it

**Two things identify a call, not one.** The bearer token says which *application* is
calling and is a per-call parameter, never ambient — a shared service-account token
injected by a `DelegatingHandler` would answer as the wrong application.
`X-ICM-TrustedUserName` says which *ICM user* the call acts as, and ICM applies that
user's Siebel visibility. It is set once on `ServiceRequestRepository`, because it
identifies this application's ICM service account:

```csharp
new ServiceRequestRepository(icmClient, trustedUserName: configuration["Icm:TrustedUserName"]);
```

Null sends no header at all rather than an empty one. If MySS ever needs to act as
different ICM users per request, this becomes a per-call value alongside the token — a
contained change, since the transport already takes it per call.

**Missing is `null` or empty; anything else throws.** ICM answers "found nothing" with a
`204` on some operations and a `404` on others, and `304` for a write that changed
nothing. The repository translates all of that. A real failure — bad credentials, a
rejected write, ICM down — surfaces as a Refit `ApiException`.

**Nulls are never serialized.** Every field on a record is nullable, so writing nulls
would blank fifty fields on a one-field update. That is also why `ServiceRequestInput`
cannot be built by copying a `ServiceRequest` wholesale — decide what is changing and set
only that.

**Booleans render lower-case in query strings.** Siebel reads `"True"` as false and
returns a perfectly valid response that ignored the flag.

**Read-only fields are absent from `ServiceRequestInput`.** The sixteen fields Siebel
calculates cannot be set, so passing one back is a compile error rather than a silently
ignored field.

**Nothing ICM sends is discarded.** A field with no property lands in
`ServiceRequest.AdditionalFields` as raw `JsonElement`, keyed by its ICM name — because a
missing `[JsonPropertyName]` match compiles, returns 200 and yields null for ever, which is
exactly how those 27 fields went unnoticed. Run the console app with `--Output=raw` to see
the untouched response. Different records returning different payloads is
`excludeEmptyFieldsInResponse=true` omitting empties, not a varying schema — with it off,
all 100 records carried the same 51 keys.

**The four date fields are zone-less `DateTime`.** `Call Date`, `Created Date`,
`Updated Date` and `Closed Date` arrive with no offset, and the value matches what the
Siebel UI displays character for character, so no zone is invented for them. The OpenAPI
document calls three of them `DTYPE_UTCDATETIME`; that is not something the wire supports,
and claiming UTC would be a silent seven-hour error.

**Dates come back as `MM/DD/YYYY HH:MM:SS`** — Siebel's display format, MEASURED against
SIT on 2026-08-28. The
[Oracle date-format page](https://docs.oracle.com/en/applications/siebel/siebel-crm/26.3/szapc/c-Date-and-Time-Formats-ja1008698.html)
specifies ISO 8601, but it describes the Financial Services Connector and this endpoint
does not follow it; both shapes are accepted on reads. Writes go out in the observed
format and are **untested** — no write has yet been made against a real ICM, and a
rejected date would be loud rather than silent.

**Month-first is evidence, not an assumption.** `10/06/2015` alone cannot say whether it
is 6 October or 10 June. Three of the eleven distinct values observed settle it, because
their second component cannot be a month: `03/28/2016`, `06/17/2026`, `08/28/2026`. None
had a first component above 12. To revisit this for another ICM instance, repeat that
check — find a record whose day exceeds 12 — rather than reasoning about it. A value in a
third shape still lands in `ServiceRequest.UnparsedValues` with the raw text, which is how
this was caught in the first place. The three Siebel date types stay distinct — `DTYPE_UTCDATETIME` is a `DateTimeOffset`,
`DTYPE_DATETIME` a zone-less `DateTime`, `DTYPE_DATE` a `DateOnly`. That last one is
load-bearing: the same Oracle page warns that a date defaulting to midnight UTC shifts to
the previous day in Western Hemisphere zones, which is every zone this runs in.

**Token cache keys are token URL + client id + scope**, and deliberately not the secret.
A narrower cached token served to a caller that asked for more scopes would fail later at
the resource server; secrets do not belong in cache keys. A single-flight gate stops a
cold-cache burst from becoming one token request per caller, and failures are never
cached.

## Tests

```bash
dotnet test Apps/IcmApi.Tests
dotnet test Apps/IcmApi.Tests --filter "FullyQualifiedName~ServiceRequestMapperTests"
```

They mirror the layers, and each one exists for a reason the layer above cannot check:

| Suite | What it catches |
| --- | --- |
| `Contracts/SiebelServiceRequestSerializationTests` | A wrong `[JsonPropertyName]` — it compiles, then returns null forever |
| `Contracts/ServiceRequestMapperTests` | Wire ↔ model conversion, and every date shape the ISO grammar allows |
| `Contracts/PublishedSurfaceTests` | An accidental `public` on a wire type |
| `Api/*` | The actual `HttpRequestMessage` — Refit builds its implementation at compile time, so a dropped query parameter is otherwise invisible |
| `Repositories/*` | Status-code translation, against the real Refit stack over canned responses |
| `Services/*` | Caching, by counting round trips through fakes |

## Functional test

`Apps/IcmApi.Console` is a console app that runs one Service Request search against a real
ICM and prints what came back. With `Query:ServiceRequestKey` set it then reads that row,
and with `Query:ChildCollection` set (say `SRProspects`) it reads that child collection
of the row raw — the library has no child-collection support yet, and a bus pass SR's
applicant rows live there. It has a second mode, `--Mode=buspass`, which **creates a
record in the target ICM**: it submits the synthetic application in the committed
`BusPass` settings section as transaction INT-316 through the bus pass workflow, prints the out-args (unmodelled
fields included), then searches recent Bus Pass SRs for the returned `ApplicationNumber`
and reads the created record back — the hand-run integration test for the workflow
client. The default mode remains the read-only query. Everything in `IcmApi.Tests` runs against canned responses —
deliberately, so the suite is fast and needs no credentials — which leaves exactly one
class of question open: whether the assumptions this client is built on hold upstream.
This is how you find out.

**This one needs the VPN connected** (see [Prerequisites](#prerequisites)) and real client
credentials.

```bash
cd Apps/IcmApi.Console
dotnet user-secrets set "Icm:Auth:ClientId"     "…"
dotnet user-secrets set "Icm:Auth:ClientSecret" '…'   # single quotes: see below
dotnet user-secrets set "Icm:TrustedUserName"   '…'   # the ICM user calls act as
dotnet run
```

The token endpoint is **not** a secret and is composed in `appsettings.json` from two
settings rather than pasted in whole:

```jsonc
"Auth": {
  "BaseUrl": "https://dev.loginproxy.gov.bc.ca/auth",   // Keycloak root; /auth on older deployments
  "Realm":   "standard",                                 // where the client is registered
  "TokenUrl": null                                       // optional; overrides both when set
}
```

giving `{BaseUrl}/realms/{Realm}/protocol/openid-connect/token`. The realm is its own
setting on purpose: a client that exists in one realm and not another fails with
`invalid_client`, which is indistinguishable from a wrong secret, so it needs to be a word
you can see and change rather than a path segment inside a URL. `TokenUrl` is there for an
authorization server that is not Keycloak — and if it is set anywhere, including in the
secret store, it wins and the realm is ignored. The run prints which happened.

`appsettings.json` is committed and carries `<replace-me>` placeholders for anything that
must be supplied. The credentials go in the **user-secret store** — a file under your user
profile, keyed by the `UserSecretsId` in the csproj, outside the repository entirely, so
there is nothing to gitignore and nothing to leak. Non-secret settings (`Icm:BaseUrl`, the
whole `Query` section) can go straight into `appsettings.json`.

Sources are read lowest priority first: `appsettings.json` → user secrets → `Icm_`
environment variables → command line. The environment is there for a shared machine or CI,
where a per-user secret store is the wrong place:

```bash
Icm_Icm__Auth__ClientSecret='…' dotnet run
```

Any setting can be overridden on the command line, though `dotnet run` will eat the first
argument, so run the built binary directly for that:

```bash
dotnet build && ./bin/Debug/net10.0/IcmApi.Console --Query:PageSize=1 --Output=summary
```

The run happens in two stages — get a token, then search — and says which one it got to.
That is deliberate: the two calls fail with the same exception type, and without the split
a rejected client secret reports as "ICM returned 401" for a request that never reached
ICM. The token is cached, so asking for it first costs nothing.

Exit codes are `0` success, `1` the settings are not usable, `2` the call failed. The
things worth watching in the output:

- **An `UnparsedValues` warning** means ICM sent a date in a shape `SiebelDate` does not
  know. Note the exact shape and add it there — and if it is ambiguous, find a record whose
  day exceeds 12 before deciding the month/day order rather than guessing it.
- **An empty result** is usually `Query:ViewMode` rather than an empty database. ICM
  defaults to `Sales Rep`, which returns only records the authenticated client owns.
- **`403` with `IP address not allowed`**, or **`Could not reach ICM`**, is the VPN rather
  than anything wrong with the settings — see [Prerequisites](#prerequisites).
- **`invalid_client`** from the authorization server is client authentication failing, and
  nothing was sent to ICM. In the order worth trying:
  1. **`Icm:Auth:Realm`** — a client registered in a different realm fails exactly this way.
  2. **The secret's quoting.** `dotnet user-secrets set "…:ClientSecret" "abc$def"` in bash
     or zsh expands `$def` to nothing, so the stored value differs from the one you pasted
     into Postman while looking identical everywhere you read it back. Re-set it with
     **single** quotes.
  3. **Service accounts** enabled on the client, though Keycloak usually reports that as
     `unauthorized_client` instead.

## Not done yet

- No DI extension method (`AddIcmServiceRequestApi`) — that needs
  `Refit.HttpClientFactory`, and the registration shape will be clearer once `MyssApi`
  actually consumes this. The wiring above works in the meantime.
- Only a simple query has been run against a live ICM. The date format and the `ViewMode`
  defaults are the two things to confirm first against SIT — run `IcmApi.Console` above,
  which exists for exactly that.
