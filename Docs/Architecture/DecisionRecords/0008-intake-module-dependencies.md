# ADR-0008 — Application Intake depends on the forms and registration modules; draft lifecycle deviations

- **Status:** Accepted (POC scope; revisit when the review workflow lands)
- **Date:** 2026-09-28
- **Deciders:** Development team
- **Related:** Handbook §3.2 (Rule 3: every cross-module dependency is an ADR), §3.4, §7.1, §7.2; ADR-0001, ADR-0002; US-APP-01; `docs/development_design/income-assistance-application.md` and `docs/design/forms-architecture.md` §4.4, §4.7 (workspace)

## Context

The applicant slice of Application Intake introduces the `intake` schema and
the shared `platform.event` log. To validate and render a draft it needs the
form spec the citizen was shown, and to gate who may start an application it
needs to know whether the caller has registered. Both facts belong to other
modules. Handbook §3.2 Rule 3 requires each such dependency to be recorded, and
Rule 2 requires it to go through a typed in-process API rather than the other
module's tables.

Two smaller choices in the same slice deviate from the wording of the forms
architecture design and are recorded here so they are not mistaken for
oversights.

## Options considered

1. **Reuse `IFormsService.SubmitAsync` for validation.** The house pattern for
   another feature reusing forms (bus pass does this). It writes a
   `forms.form_submissions` row as a side effect, which would put intake's
   answers in the forms schema.
2. **Inject the forms spec provider and the static validator directly.** No
   forms table is touched; intake owns its answers in its own schema. The
   dependency is three symbols wide.
3. **Copy the validator into intake.** No dependency, but two validators that
   drift.

For the profile check, the only option worth taking is the existing
`IUserProfileService.HasProfileAsync`: the flag is a lookup, not a token claim,
so it cannot be an authorization policy.

## Decision

Option 2. `Myss.Api.Intake` depends on exactly:

- `IFormSpecProvider.GetLatestAsync` to pin the spec version when a draft is
  created;
- `IFormSpecProvider.GetVersionAsync` to fetch the pinned version for
  validation and rendering;
- the static `FormSpecValidator.Validate` for server-side re-validation;
- `IUserProfileService.HasProfileAsync` to require a registered profile before
  a draft is created (enforced in the API, 403 with keyword
  `INTAKE.APPLICATION.PROFILE_REQUIRED`).

Intake never reads `forms.*` tables. It reaches the event log only through
`IEventStore` in `Myss.Api.Platform`.

Two lifecycle deviations, deliberate:

- **No `PartialSaved` event.** The forms design has save-and-resume as an
  event plus the JSONB write. Draft saves here update the single answers row,
  guarded by a row version. Draft history has no audit value, and what was
  submitted lives in the `Submitted` event payload.
- **Spec version pinned at create, not stamped at submit.** A draft must keep
  rendering with the spec it was started on; submit stamps that pinned version
  into the event. The effect the forms design wants (a submission renders from
  the spec that produced it) holds.

## Consequences

- Easier: intake owns its data end to end; the dependency graph is three
  symbols plus one profile lookup; the fold and rules are pure and
  table-tested.
- Harder: a change to the validator's error keywords is now a contract with
  two consumers. The draft save filters `FORM.FIELD.REQUIRED` by keyword, so
  renaming it breaks partial saves.
- Watch: when the Identity domain lands, the profile lookup moves behind that
  module's API and this ADR is amended. When the review workflow adds worker
  actions, the stream version becomes a client-facing field beside the row
  version, and the applicant-facing fold must keep ignoring worker events it
  does not model.
