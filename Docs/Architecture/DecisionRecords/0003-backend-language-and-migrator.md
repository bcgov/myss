# ADR-0003 — Back-end language & the language-native migrator

- **Status:** Accepted (closes Standing question SQ-01)
- **Date:** 2026-08-10
- **Deciders:** Manuel Rodriguez (Tech Lead), Adam Hodgins (Senior Developer), Xiaozhen Niu (Senior Developer), Sinan Soykut (Senior Developer), Stephen Laws (Solution Architect)
- **Related:** SQ-01; Handbook §3.3 Bill Of Materials (BOM), Part 7 (Data & persistence, migrator decision), Part 0 (illustrative dialect); risks R-01, R-03; ADR-0002

## Context

The handbook left the back-end framework as the open week-1 decision (SQ-01)
and stated that migration tooling follows the language automatically ("the
dominant language-native migrator"). The BOM offered three options; handbook
code samples are written in illustrative TypeScript/NestJS with some
FastAPI/Python.

## Options considered

1. **NestJS (TypeScript)** — BOM primary; migrator: Prisma Migrate. Shares a
   language with the React front end; handbook samples already in this dialect.
2. **FastAPI (Python)** — BOM alternative; migrator: Alembic.
3. **Spring Boot (Java)** — BOM alternative; migrator: Flyway.
4. **ASP.NET Core / .NET 10 (C#)** — outside the BOM; migrator: Entity
   Framework Core, code-first.

## Decision

**Option 4.** The back-end is written in **C#** on **ASP.NET Core / .NET 10**
(`Apps/MyssApi`). Database schema is managed **code-first with Entity
Framework Core**: the EF model is the source of truth for the schema, and
schema changes are made as EF Core migrations generated from the model and
applied through the standard migration pipeline. Hand-written SQL migrations
are not the primary mechanism.

This is a deliberate deviation from the BOM's three options (Part 2.8), which
is why it is recorded here. The choice was made outside the handbook's
three-options list; team experience with the .NET stack (risk R-03) is the
driving consideration.

## Consequences

- Handbook code samples remain _illustrative dialect_; the language-independent
  rules (Appendix G) apply unchanged and translate to C#.
- The SQ-02 boundary-enforcement options (ADR-0002) are read in .NET terms
  (project references / InternalsVisibleTo, analyzers, architecture tests).
- Code-first EF Core means: per-module DbContexts and schemas map naturally to
  the module-boundary rules (Part 3.2, 7.1); migrations are reviewed in the same
  PR as the model change; the event-store DDL in §3.4 is expressed as an EF
  model rather than raw DDL.
- Watch: EF-generated migrations must be inspected before merge (destructive
  changes, index/constraint intent), and the migrator must remain the only path
  that mutates schema.
