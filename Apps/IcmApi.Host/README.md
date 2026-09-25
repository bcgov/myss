# IcmApi.Host — the ICM middleware

The REST service MyssApi calls for anything ICM-bound. It is the "ICM middleware"
of handbook Part 4.2: a separate deliverable that exposes a simpler API in MySS
business language and does the Siebel translation, so nothing Siebel-shaped enters
MyssApi. The translation itself lives in the [IcmApi](../IcmApi/README.md) class
library; this project hosts it behind one authenticated route.

```
Controllers/     BusPassController — POST v1/bus-pass/applications
Contracts/       BusPassApplicationRequest / BusPassApplicationResponse, the keywords
Services/        IBusPassSubmitter → IcmBusPassSubmitter (token, then submit), correlation id
Configuration/   Program/Startup bootstrap, JWT bearer + mock gate, the ICM client wiring
```

## The contract

`POST /v1/bus-pass/applications` with a bearer token. The body is MyssApi's
`BusPassApplicationModel` as JSON (camelCase, enums by name); the authoritative
example is [`Shared/contracts/bus-pass-application.sample.json`](../../Shared/contracts/bus-pass-application.sample.json),
which both `MyssApi.Tests` and `IcmApi.Host.Tests` check against. `submissionKey`
is MyssApi's stored submission id. It matters because ICM files a service request on
every call: a resend after an ambiguous failure that carries the same key is what
lets Siebel's upsert recognise it instead of filing a second request.

| Answer | Meaning |
|---|---|
| 200 `{applicationNumber, errorCode, errorMessage, status}` | The workflow answered. A populated `errorCode` is a business rejection that ICM still filed. |
| 400 | The body is not a request (missing `requestType`, unknown enum value). |
| 401 | No usable bearer token. |
| 502 / 503 / 504 problem with `keyword` | No answer could be obtained. The caller must not assume either way whether ICM has the request. |

The keywords are `ICM.BUSPASS.NOT_CONFIGURED` and `ICM.BUSPASS.TOKEN_UNAVAILABLE`
(503), `ICM.BUSPASS.UNREACHABLE`, `ICM.BUSPASS.UPSTREAM_ERROR` and
`ICM.BUSPASS.UNUSABLE_RESPONSE` (502), and `ICM.BUSPASS.TIMEOUT` (504). Nothing is
retried here; MyssApi owns that decision.

The workflow also echoes the matched first and last name. They are deliberately not
returned: handed to whoever typed a SIN, they would confirm another person's name.
The controller logs only whether the echo matched what was submitted.

The OpenAPI document at `/swagger/v1/swagger.json` (UI at `/swagger`) is the
published form of this contract.

## Authentication

Callers present a client-credentials token from the shared standard realm. The
token's `azp` must be in `Oidc:AllowedClients`; the list is required, and an empty
one refuses to start. Audience is validated only when `Oidc:Audience` is set,
because a standard-realm client-credentials token names the requesting client as
its audience, not this host. `appsettings.json` allows MyssApi's service integration,
`sdpr-my-ss-api-6645`, the client it authenticates as (`Oidc:ServiceAccount` on its
side), not the web client citizens sign in with; override per environment with
`Icm_Oidc__AllowedClients__0`.

For token-less local calls, the three-lock mock gate from MyssApi is here too:
`AllowMockAuth`, `MockAuth` and a non-production `EnvironmentName`, all explicit
(see `appsettings.local.sample.json`). A production-named environment that sees the
flags refuses to start.

## Configuration

The `Icm` section is the one `IcmApi.Console` reads, and this project shares the
console's user-secret store on purpose: set the values once, from either directory.

```bash
cd Apps/IcmApi.Host
dotnet user-secrets set "Icm:BaseUrl" "…"              # ICM base URL incl. version prefix
dotnet user-secrets set "Icm:Auth:Realm" "…"
dotnet user-secrets set "Icm:Auth:ClientId" "…"
dotnet user-secrets set "Icm:Auth:ClientSecret" "…"
dotnet user-secrets set "Icm:TrustedUserName" "…"
```

Get the values from another developer or the team's central secrets store. Deployed,
the same keys are environment variables, either prefixed `Icm_` (`Icm_Icm__BaseUrl`)
or unprefixed (`Icm__BaseUrl`), which is the form the tenant repo's manifests use. Startup is fail-closed on the non-secret parts:
without `Icm:BaseUrl` and a resolvable token endpoint the host does not start. The
credentials are checked on the first submission instead, which answers 503
`ICM.BUSPASS.NOT_CONFIGURED` until they are set, so a checkout without secrets still
boots for tests.

`Icm:TimeoutSeconds` (20) plus `Icm:TokenTimeoutSeconds` (5) is kept below MyssApi's
per-attempt timeout (30) on purpose: on a cold token cache the two calls run back to
back, and if MyssApi gave up first, ICM could still file the request after MyssApi
had recorded the attempt as failed.

## Run

```bash
dotnet run --project Apps/IcmApi.Host        # http://localhost:5100, Swagger at /swagger
dotnet test Apps/IcmApi.Host.Tests
```

Through the Aspire app host the middleware starts automatically when an ICM base
URL is configured (`Icm:BaseUrl` in the shared user-secret store, or
`Aspire:Parameters:Icm:BaseUrl`), and MySSApi is pointed at it; without one the
rest of the stack runs and bus pass submissions report the middleware as unavailable.
Reaching ICM needs the ministry VPN, as the library's README explains.

Every request carries an `X-Request-ID`: MyssApi's when it sent one, otherwise the
server's trace id. It is echoed on the response, on every log line, and forwarded
to ICM.
