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
| OAuth 2.0 token endpoint (client credentials) | RFC 6749 §4.4 | `IOAuthTokenService` |

The token request uses **`client_secret_post`** (RFC 6749 §2.3.1) — `client_id` and
`client_secret` as form fields, no `Authorization` header. That is what the ministry's
existing integrations use.

`docs/integration/SR_OpenApi.json` is ICM's own OpenAPI 3.0.1 document for the Service
Request business component — the contract this client implements, kept in the repository
so a change upstream shows up in the same diff as the change here. It is the source of
every field name, length and data type in the models. Nothing generates code from it; the
client is hand-written and the tests assert against it.

## Layout

```console
Api/            Refit interfaces + settings                    internal
Api/Contracts/  Siebel and OAuth wire models, the mapper       internal
Models/         the published models                           public
Repositories/   data access — call, map, translate status      public
Services/       behaviour — token caching, authentication      public
docs/           the upstream specs this client implements
```

Namespaces follow the folders, except that `Api/` adds no segment — `Icm.Api.Api` reads
worse than it informs. So: `Icm.Api`, `Icm.Api.Contracts`, `Icm.Api.Models`,
`Icm.Api.Repositories`, `Icm.Api.Services`.

**Everything from `Api/` down is `internal`.** `IcmApi.Tests` reaches it through
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
        ServiceRequestPage page = await serviceRequests.SearchAsync(
            new ServiceRequestQuery
            {
                SearchSpec = "[Status] = \"Open\"",
                Fields     = ["SR Number", "Status", "Created"],
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

**Dates are ISO 8601**, per
[Siebel: Date and Time Formats](https://docs.oracle.com/en/applications/siebel/siebel-crm/26.3/szapc/c-Date-and-Time-Formats-ja1008698.html)
— the OpenAPI document types the date fields but states no format, so that page is the
authority. `MM/DD/YYYY` is deliberately **not** accepted: `03/04/2026` is two different
days depending on the order and nothing in the value says which. A non-ISO value lands in
`ServiceRequest.UnparsedValues` with the raw text rather than being guessed at. The three
Siebel date types stay distinct — `DTYPE_UTCDATETIME` is a `DateTimeOffset`,
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
ICM and prints what came back. Everything in `IcmApi.Tests` runs against canned responses —
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

- **An `UnparsedValues` warning** means ICM sent a date that is not ISO 8601. That is the
  open question flagged above, and this is the tool that answers it. Note the exact shape
  and add it to `SiebelDate` — do not guess the month/day order.
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
- `IcmApi.Tests` is not in `.github/workflows/tests-dev.yml`, which only runs the MyssApi
  suite.
- Only a simple query has been run against a live ICM. The date format and the `ViewMode`
  defaults are the two things to confirm first against SIT — run `IcmApi.Console` above,
  which exists for exactly that.
