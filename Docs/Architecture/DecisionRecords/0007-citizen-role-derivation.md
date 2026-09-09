# ADR-0007 — Citizen role derives from the identity provider, not CSS-assigned roles

- **Status:** Proposed — implemented (behind `Oidc:DeriveClientRoleFromIdp`), but the decision itself is still owed: it deviates from RULE-WKR-04's delivery mechanism and must be confirmed with IDIM at CSS/BCeID onboarding before it counts as Accepted
- **Date:** 2026-08-31
- **Deciders:** Development team
- **Related:** RULE-WKR-04, RULE-IDA-08, RULE-MSG-04, US-IDA-01 AC8, DS-04; Handbook §4.1; the standard-realm reality delta (client roles, not realm roles)

## Context

RULE-WKR-04 defines a six-role model in which the citizen roles are
state-derived — APPLICANT (BCeID-authenticated, no promoted case) vs CLIENT
(PROFILE + ICM case) — and says roles are "attached to the session token at
login" by a pluggable auth gateway. That presumed a Keycloak we control. The
build actually targets the shared Pathfinder SSO `standard` realm, where we
get **client roles assigned per user, per environment, via the CSS
dashboard/API, and only after the user's first login**. The shared realm
cannot know MySS account state, and per-user assignment cannot work for ~100k
self-registering citizens. Without a decision, a real BCeID/BCSC sign-in
authenticates and then fails every `[Authorize(Policy = Client)]` endpoint.

## Options considered

1. **Per-user CSS role assignment (the realm's native model).** Works for the
   small IDIR worker population; a non-starter for citizens — nobody can
   hand-assign 100k users, and assignment is only possible after first login,
   which is exactly when the citizen is trying to use the app.
2. **Automated assignment via the CSS REST API at registration/promotion.**
   The token genuinely carries the role — but the user's in-hand token
   predates the assignment (forced re-auth or a broken first session), it
   puts an external admin API on the registration and promotion hot paths
   (RULE-IDA-06 wants registration atomic against local Postgres only), it
   accumulates ~100k mappings in the shared realm team's infrastructure, and
   it adds a privileged credential. The one benefit — per-user revocation in
   Keycloak — is not a real requirement: blocking a client is MySS/ICM
   account-state business logic.
3. **Derive the citizen role from the token's `identity_provider` claim,
   app-side, in one pure calculator.** Per RULE-IDA-08 the citizen sign-in
   path *is* Basic BCeID (the build also offers BC Services Card), so
   "authenticated via a citizen IDP" and "is a citizen" are the same
   statement — the role adds no information the token doesn't carry. Worker
   roles (small population) stay CSS-assigned and pass through.

## Decision

Option 3. `RoleCalculator.Calculate(TokenIdentity, MyssAccountSnapshot)` is THE
single place effective roles are computed: pure and table-tested. The
calculator *decides*; `RoleCalculationClaimsTransformation` *publishes* the
decision — nothing in ASP.NET Core calls the calculator, and authorization
policies and `CurrentUserAccessor` read only the principal's flat role
claims, so the verdict is written back as claims by an idempotent
`IClaimsTransformation` (the hook that runs after **every** authentication
scheme — real JWT, mock personas, Option 2's future cookie — and before
authorization). `RequireRole` policies and `CurrentUserAccessor` stay
untouched, identical under Option 1 (JWT) and Option 2 (BFF). Derivation keys on
`identity_provider ∈ {bceidbasic, bcservicescard}` — never on
`bceid_user_guid` presence, which Business BCeID tokens also carry. Cross-line
stripping (citizen IDP never keeps worker roles; IDIR never keeps CLIENT) is
unconditional hardening. The derivation itself sits behind
`Oidc:DeriveClientRoleFromIdp` (default true) as the escape hatch if IDIM
mandates CSS-managed citizen roles. The SPA never derives or reads roles from
token claims: it consumes the server's verdict via `GET /v1/auth/me`
(`AuthController`), merged into the session by `buildSession` — one place
roles are decided, and the same seam Option 2 (BFF) re-backs the SPA session
with. The session reports loading until the `/me` response arrives, so
role-gated rendering fails closed rather than flashing a wrong surface.

This deviates from RULE-WKR-04's "the MySS application does not implement
auth logic": we read that clause as banning re-authentication and
per-request directory calls (its context is the legacy `GetIDIRGroupsInfo`
lookup), not as banning derivation of authorization state from data the
token and MySS already own — concentrated in one auditable function.

## Consequences

- Easier: BCeID/BCSC sign-ins are usable with zero per-user provisioning; the
  whole role matrix is one table of pure tests; no new runtime dependencies.
- Owed: the APPLICANT/CLIENT split (RULE-WKR-04, RULE-MSG-04, US-IDA-01 AC8)
  — `MyssAccountSnapshot` is the seam; when the Identity domain lands, its facts
  (APPLICANT record / PROFILE / promoted case, session-cached, invalidated by
  the promotion saga) become new calculator rows. Until then every citizen
  computes as CLIENT.
- Watch: IDIM has not confirmed the role model (`MyssRoles` note) — present
  this ADR at CSS/BCeID onboarding; the IDP aliases are IDIM's (a rename
  shows up as citizens losing CLIENT — pinned by RoleCalculatorTests);
  `identity_provider` must be verified present in a real access token from
  this client (fold into the DS-04 first-login verification); the `/me`
  response is cached ~5 minutes in the SPA — promotion must invalidate
  `ME_QUERY_KEY` when the Identity domain lands.
