# ADR-0004 — Promotion-saga atomicity model

- **Status:** Proposed — pending SB-02 (defined during implementation with the middleware)
- **Date:** July 2026
- **Deciders:** Senior Developer, with the ICM/middleware team
- **Related:** SB-02, SQ-03; Handbook §3.4, §3.5, Part 4.9 (promotion saga), Part 4.7 (audit before promote), Part 8 (INT516 etc.); risk R-16; ADR-0001

## Context

The applicant→client promotion (Application Intake *Accept*) is the
highest-stakes write in the system: contact create/patch → case create →
case patch → attachment upload against ICM. A partial failure that is not
cleanly compensated risks orphaned or duplicated contacts/cases in ICM. It is
run as a Temporal saga (per ADR-0001) with idempotency keys and a
`PROMOTION_FAILED` recovery state surfaced for worker recovery. Whether the
saga must be orchestrated with compensations, or can collapse to a single
guarded call, depends on what the middleware can offer.

## Options considered

1. **(A, default) Orchestrated, compensating saga in Temporal.** Each ICM step
   idempotent; compensations run in reverse on partial failure; MySS owns
   atomicity. Works with today's fine-grained ICM endpoints; more moving
   parts and compensation logic to keep correct.
2. **(B) Single coarse transactional promotion endpoint provided by ICM.**
   Atomicity becomes ICM's responsibility; the workflow simplifies to one
   guarded, idempotent call. Depends on the middleware contract.
3. *(Third option to be evaluated with the middleware team — e.g. a hybrid:
   coarse endpoint for contact+case, separate idempotent attachment step.)*

## Decision

**Pending.** Interim: build option A. Either way, idempotency keys, the
`PROMOTION_FAILED` recovery state, audit-before-promote, and integration
testing against the test-ICM (not mocks) remain.

## Consequences

To be completed when SB-02 lands. Risk R-16 (promotion partial failure) stays
open until then.
