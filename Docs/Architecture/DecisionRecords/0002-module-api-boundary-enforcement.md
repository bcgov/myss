# ADR-0002 — Module-API boundary enforcement mechanism

- **Status:** Proposed — pending Standing question SQ-02 (week-9 decision)
- **Date:** July 2026
- **Deciders:** Senior Developer
- **Related:** SQ-02; Handbook §3.2 (module boundaries, Rules 1–3), §2.8; risks R-01, R-02

## Context

MySS is a modular monolith: nine domain modules plus a platform package. Part
3.2 forbids cross-schema joins, requires modules to talk only through published
module-APIs (read models, not table rows), and requires every cross-module
dependency to be recorded as an ADR (Rule 3). *How* those boundaries are
mechanically enforced is still open (SQ-02). Until decided, the handbook says
build to option A and keep call sites injectable so option B remains possible.

## Options considered

1. **(A, proposed default) Static discipline.** Workspace/package boundaries +
   lint-level module-restriction rules (a module may import only from its own
   folder, the platform package, and another module's published API) + the
   ADR-per-dependency rule. Cheap, fast feedback in CI, no runtime cost; relies
   on tooling and review discipline.
2. **(B) Runtime-checked in-process API gateway.** Contracts validated at call
   time. Stronger guarantee; more ceremony and a runtime indirection on every
   cross-module call.
3. *(Three-options discipline: a third option — e.g. architecture tests such
   as NetArchTest/ArchUnit-style assertions in the test suite — should be
   evaluated when SQ-02 is decided.)*

## Decision

**Pending.** Interim: build to option A; keep cross-module call sites
injectable so option B stays possible.

Note for the decider: the back-end is ASP.NET Core / .NET 10 (see ADR-0003),
so the handbook's ESLint illustration translates to the .NET equivalents
(project references / InternalsVisibleTo boundaries, analyzers, architecture
tests).

## Consequences

To be completed when decided. Whichever option is chosen, Rule 3
(ADR-per-dependency) stands and the dependency graph must remain visible.
