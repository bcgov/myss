# ADR-0005 — Durable event-broker selection

- **Status:** Deferred — decide if/when the OCR/AI futures activate
- **Date:** July 2026
- **Deciders:** Senior Developer
- **Related:** Handbook §3.1 (extraction stays affordable), §3.4, §3.7 (eventing futures), Part 1.6 (future goals); ADR-0001

## Context

Today the domains coordinate in-process (module-API calls, or a lightweight
in-process signal for fire-and-forget). No durable external broker is
deployed. A durable event log is only needed when an out-of-process consumer
must reliably react to something that happened in MySS — the canonical example
being an OCR or AI job reacting to a *new-attachment* event.

## Options considered

1. **Redis Streams** — already in the BOM for caching; sufficient for
   in-process signalling at this scale; limited durability/replay guarantees.
2. **NATS JetStream** — durable, replayable; an extra system to operate.
3. **RabbitMQ** — mature broker; extra system to operate.
4. **Do nothing now; design the seam.** Keep the migration cheap without
   paying to run a broker before any consumer exists.

## Decision

**Deferred (option 4 now).** Use in-process signalling (Redis Streams is
sufficient) and adopt a durable event log only when OCR/AI activate. **NATS
must not be presupposed** — the broker is chosen against the live requirement
(durability, replay, ordering, operational cost) from the BOM alternatives.

To keep the later move cheap:

1. Publish through **one seam** now (a single platform `events.publish(...)`
   interface) — never direct module-to-module calls for notifications.
2. Use the **transactional outbox** pattern where an event must not be lost —
   written in the same transaction as the ADR-0001 event append, relayed
   asynchronously.
3. Keep payloads **broker-agnostic** (plain typed DomainEvent records).
4. Decide the broker on the evidence when the requirement is real.

## Consequences

- Easier: no broker to operate today; extraction/eventing later is a swap
  behind one seam.
- Watch: any module bypassing the publish seam erodes this; the outbox relay
  must be built before a broker is introduced. Re-open this ADR when OCR/AI
  work is scheduled.
