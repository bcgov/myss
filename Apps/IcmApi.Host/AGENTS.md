# AGENTS.md — IcmApi.Host

Guidance for AI agents working in this app. The workspace-wide rules in the repo
root [AGENTS.md](../../AGENTS.md) apply in full; `README.md` here is the fuller
guide (contract, authentication, configuration).

The REST host over the [IcmApi](../IcmApi/AGENTS.md) client library: one
authenticated route in MySS business language, the library behind it, nothing
Siebel-shaped in between. MyssApi is the only intended caller.

```
Controllers/BusPassController       POST v1/bus-pass/applications
Contracts/                          the wire DTOs and ICM.BUSPASS.* keywords
Services/IcmBusPassSubmitter        token first, then submit; maps every failure to a keyword + status
Configuration/                      Program/Startup (MyssApi's two-class shape), JWT bearer, mock gate, AddIcmClient
```

Rules that shape changes here:

- **The wire contract is shared.** `Shared/contracts/bus-pass-application.sample.json`
  is linked into this project's tests and MyssApi's; change the request shape on both
  sides in one PR or one suite fails. The response is the four fields MyssApi reads;
  never add the echoed name.
- **Fail-closed startup, like MyssApi.** `Icm:BaseUrl`, a resolvable token URL and a
  non-empty `Oidc:AllowedClients` are required to boot (unless the mock gate is open).
  Credentials are checked per call. Preserve that shape rather than adding fallbacks.
- **No retries.** ICM files a service request on every call. The submitter reports;
  MyssApi decides.
- **Nothing from ICM's response bodies in logs.** Log status codes, keywords and ids.
- **Configuration precedence** is `appsettings.json` → environment file → user secrets
  (Development; the store is shared with IcmApi.Console) → `appsettings.local.json` →
  environment variables prefixed `Icm_`.
- Tests substitute `IBusPassSubmitter` (endpoint tests) or the library's
  `IBusPassRepository`/`IOAuthTokenService` (submitter tests) with the fakes in
  `IcmApi.Host.Tests/TestDoubles/`; nothing connects to ICM or the realm.
