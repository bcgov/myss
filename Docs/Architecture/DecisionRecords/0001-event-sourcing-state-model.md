# ADR-0001 — Event sourcing as the default state model; Temporal scoped to durable orchestration

- **Status:** Accepted
- **Date:** July 2026
- **Deciders:** Senior Developer (architecture owner), per Handbook Part 2.8
- **Related:** Handbook §2.5 (invalid states unrepresentable), §3.4 (the state model), §3.7 (eventing futures), Part 4.7 (audit before promote), Part 4.9 (promotion saga); ADR-0004, ADR-0005; risks R-16

## Context

Every application, service request, monthly report, signed form and account
change must record what happened to it, when, and by whom — and the system must
be able to _audit before it promotes_ (Part 4.7). The legacy portal used
mutable status columns. The rebuild needed one system-wide answer to how state
is represented, and needed to decide the role of Temporal so it did not become
a de-facto state machine for the human review workflow.

This is the architectural decision with the widest reach, so Part 2.8 requires
it to be recorded as an ADR.

## Options considered

1. **Mutable status columns (CRUD).** Simple and familiar; but history and state
   are separate artefacts, invalid states are representable, and the audit
   trail must be built alongside rather than being the data.
2. **Temporal workflows as the state machine for every process, including
   human review.** Durable and restart-safe; but couples business state to a
   workflow engine, makes "what happened and when" a workflow-history concern
   rather than a domain artefact, and adds ceremony to every human action.
3. **Event sourcing for user-driven processes + Temporal narrowed to
   automated/queue-like orchestration.** History _is_ the data; state and the
   currently-available actions are computed by rule evaluation over the log;
   Temporal handles the multi-step, side-effecting flows that must survive
   restarts and partial failure.

## Decision

Option 3.

- **User-driven processes are event-sourced** — applications, service
  requests, monthly reports, employment-plan signing, account changes. Actions
  are recorded as immutable typed events; state and the set of available
  actions are computed by rule evaluation over the log. There is **no mutable
  status column** as the source of truth.
- **Automated / queue-like processes use Temporal** — the Application
  Promotion saga, attachment consolidation, targeted-broadcast and bulk-email
  dispatch, the INT334 submission queue, data migrations/transfers.
- **Where it is ambiguous, default to event sourcing.**
- They compose: a human event (e.g. reviewer _Accepts_) is recorded in the
  event log and _triggers_ a Temporal workflow (the promotion saga). Event
  sourcing records the decision; Temporal executes the side effects.

Rubric (Handbook §3.4): a human acting on a file needing an auditable record →
event sourcing; an automated multi-step side-effecting flow needing timers,
retries and compensation → Temporal; both → event records the decision, event
triggers the workflow.

The shared mechanism (append-only event store, fold to state, rule evaluation
for available actions) is owned by the platform package; domains supply their
own event and state types. Reference implementation in Handbook §3.4.

## Consequences

- Easier: audit trail and business state are the same artefact; invalid states
  are unrepresentable; the transactional outbox for future eventing (§3.7,
  ADR-0005) falls out of the event append naturally.
- Harder: every domain must model its events and folds; reporting reads need
  projections/read models rather than a status column; team must learn the
  pattern.
- Watch: Temporal must stay narrowed to durable orchestration — a "review
  state machine in Temporal" is a deviation and would need a superseding ADR.
  The promotion saga's atomicity remains open (ADR-0004 / SB-02).
