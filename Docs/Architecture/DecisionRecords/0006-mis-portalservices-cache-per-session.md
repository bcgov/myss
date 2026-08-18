# ADR-0006 — MIS PORTALSERVICES cache is once per session

- **Status:** Accepted (supersedes the "~24h TTL" note in the target architecture)
- **Date:** July 2026
- **Deciders:** Senior Developer; freshness target tuned via discovery session DS-02
- **Related:** SQ-04, DS-02; Handbook §3.6 (caching strategy), Part 6.7 (Payments & Tax), Account Info, Part 8.3 (MIS); risk R-07

## Context

Two upstream reads are hot, shared across domains and slow if uncached: the
INT331 tombstone (every client login) and MIS PORTALSERVICES (payments +
family/address, used by Payments & Tax and Account Info). Both are cached in
Redis. An earlier draft implied a multi-hour (~24h) TTL for the MIS data;
payment data freshness matters around the cheque-run cadence, so a long TTL
risks showing stale payment information.

## Options considered

1. **Multi-hour TTL (~24h).** Fewest upstream calls; risks stale payment data
   across a cheque run.
2. **Once per session, shared across domains, explicitly invalidated on
   relevant user edit.** One upstream call per login serves both consuming
   domains; freshness bounded by session length; edits (e.g. an Account Info
   phone change) invalidate immediately.
3. **No caching / per-request fetch.** Always fresh; hammers MIS on every page.

## Decision

Option 2. MIS PORTALSERVICES is cached **once per session** (key
`mis:portalsvc:{sessionId}`, TTL = session), **shared** between Payments & Tax
and Account Info, and **explicitly invalidated on a relevant user edit**. Cache
keys are session-scoped so one user's data never serves another. The concrete
freshness target is tuned by the MIS spec and the cheque-run cadence in DS-02.
(The INT331 tombstone is separately cached ~15 minutes per session.)

## Consequences

- Easier: bounded staleness; one shared read model for two domains.
- Watch: DS-02 may tighten the target further; the MIS spec (SQ-04 / R-07) is
  still an external dependency — build against a contract-true stub meanwhile.
