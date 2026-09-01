# Architecture Decision Records

Architecture Decision Records (ADRs) for MySS, kept in-repo beside the code they
govern (Handbook Part 2.8 / Part 11 Appendix F). Each record follows the
lightweight MADR-style template in Appendix F: Status, Date, Deciders, Related,
Context, Options considered, Decision, Consequences.

An ADR is required for (at minimum): any cross-module dependency, any deviation
from the target architecture, the event-sourcing/Temporal split, and any
"three options" technology selection (Handbook Part 2.8, Part 3.2 Rule 3).

| ADR                                                      | Decision                                                                                                              | Status                   |
| -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- | ------------------------ |
| [ADR-0001](0001-event-sourcing-state-model.md)           | Event sourcing as the default state model for user-driven processes; Temporal scoped to automated/queue orchestration | Accepted                 |
| [ADR-0002](0002-module-api-boundary-enforcement.md)      | Module-API boundary enforcement mechanism (static discipline vs runtime gateway)                                      | Proposed — pending SQ-02 |
| [ADR-0003](0003-backend-language-and-migrator.md)        | Back-end: C# / ASP.NET Core .NET 10; EF Core code-first migrations                                                    | Accepted (closes SQ-01)  |
| [ADR-0004](0004-promotion-saga-atomicity.md)             | Promotion-saga atomicity model (orchestrated-compensating vs coarse transactional endpoint)                           | Proposed — pending SB-02 |
| [ADR-0005](0005-durable-event-broker.md)                 | Durable event-broker selection (if/when OCR/AI activate)                                                              | Deferred                 |
| [ADR-0006](0006-mis-portalservices-cache-per-session.md) | MIS PORTALSERVICES cache = once per session (freshness tuned by DS-02)                                                | Accepted                 |
| [ADR-0007](0007-citizen-role-derivation.md)              | Citizen CLIENT role derives from the identity provider (RoleCalculator); worker roles stay CSS-assigned               | Proposed — implemented, pending IDIM confirmation |

## Adding an ADR

1. Copy `TEMPLATE.md` to `NNNN-short-title.md` (next free four-digit number).
2. Fill it in; keep the three-options discipline where a technology is chosen.
3. Add a row here and in Handbook Part 10.4; cite it as a **Decision** callout
   wherever it applies.
4. Review it in the same pull request as the change it justifies.
