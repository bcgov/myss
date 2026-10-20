# ADR-0010 — The staff API is a second deployable sharing MyssApi's data model

- **Status:** Proposed
- **Date:** 2026-10-20
- **Deciders:** Development team
- **Related:** ADR-0001 (event sourcing), ADR-0002 (module boundaries), ADR-0003 (EF Core code-first), ADR-0008 (intake dependencies), ADR-0009 (migrations as pipeline jobs)

## Context

The administration features (the form editor and the estimator rates editor)
and the worker features (reviewing submitted Income Assistance applications,
reading form submissions, rendering the bus pass PDF) were served by MyssApi
and shown on the citizen web client. They are moving to their own site and API,
MyssAdminWebclient and MyssAdminApi, so that staff-only code, staff-only
credentials (the Strapi write token, the CDOGS client) and the IDIR sign-in no
longer sit in the citizen-facing deployables.

The administration features only touch Strapi. The worker features do not:
they read the intake working copy, fold and append to the platform event log
(ADR-0001), and read form submissions. The citizen side writes the same rows
and streams. Moving the worker endpoints therefore means two deployables using
one database and one set of schemas, which ADR-0002's module rules (modules
talk through published module APIs, never shared tables) do not cover.

## Options considered

1. **Keep the worker endpoints in MyssApi; move only the screens.** The staff
   site calls MyssApi for worker actions. One owner per schema, no new
   dependency. But the citizen API keeps the worker code, the CDOGS
   credentials and the IDIR worker surface, which is what the split set out
   to remove, and the staff site depends on two APIs.
2. **The staff API calls MyssApi over HTTP for intake data.** A proper module
   API between deployables. It needs a service-to-service credential and an
   internal API surface for every read and every review action, duplicates
   the authorization on both sides, and adds a network hop and a failure mode
   to every worker action — a large build for a POC-stage module.
3. **The staff API shares the EF model with MyssApi.** The DbContexts, entities,
   the event store and the intake projection move to a class library
   (`Apps/MyssData`) that both APIs reference. MyssApi stays the only
   deployable that changes the schema: the migrations stay in its assembly and
   only its migrate Job applies them (ADR-0009).

## Decision

Option 3.

- `Apps/MyssData` holds the shared model: the Forms, Intake and Platform
  contexts and entities, the event store, and the intake events, state and
  projection. Both APIs fold the same events with the same code, so a review
  action and a citizen resubmission cannot disagree about an application's
  state.
- MyssApi owns the schema. Its registrations set the migrations assembly to
  MyssApi; MyssAdminApi never migrates and never calls `EnsureCreated`. A
  model change is made in MyssData, its migration added in MyssApi, and both
  APIs are released together.
- Concurrency stays where ADR-0001 put it: appends are guarded by the
  `(stream_id, version)` primary key, so a citizen and a worker acting on the
  same stream at once get an ordinary expected-version conflict, whichever
  process they are in.

This is a deliberate deviation from ADR-0002: the intake module is now used by
two deployables through a shared library rather than through a module API.

## Consequences

- Easier: the citizen API no longer holds worker code, the CDOGS client or the
  Strapi write token; each site talks to one API.
- Harder: a schema change now ships in two deployables. A release must run
  migrations before either API restarts, and the two APIs must not run
  different model versions for long. The promotion pipeline orders this
  (migrate, then restart both).
- Watch: anything cached in one API process that the other changes. Today
  that is the citizen rate table (five minutes, see MyssAdminApi) and nothing
  in the intake path; a future cache over intake data needs a cross-process
  invalidation story.
- If ADR-0002 settles on a runtime gateway (its option 2), revisit option 2
  above and replace the shared library with the gateway.
