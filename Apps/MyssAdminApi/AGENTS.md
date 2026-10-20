# AGENTS.md — MyssAdminApi

Guidance for AI agents working in this app. The workspace-wide rules in the
repo root [AGENTS.md](../../AGENTS.md) apply in full; this file adds what is
specific to the admin API.

## What it is

The API behind the admin webclient: the form editor (list forms, open and save
a draft, publish) and the eligibility rates editor (read the published table,
save a new one). Every endpoint requires the `AdminIdir` policy (an
authenticated caller with an IDIR identity). It has no database; everything it
reads and writes is in Strapi.

The routes (`v1/forms`, `v1/forms/{id}/draft`, `v1/forms/{id}/publish`,
`v1/EligibilityRates`) are the ones MyssApi served before the split.

## Shared and copied code

- `MyssFormSpec` (project reference) holds `FormSpecValidator` and the models it
  uses. The admin save and the citizen submit must run the same rules, so this
  code is shared, never copied.
- The host plumbing (`Configuration/`), `KeycloakClaims`, mock auth, the rate
  table columns and `FddRateData` are copies of MyssApi's and may diverge. If you
  change the rate table shape, change both copies and MyssContent's schema.

## Configuration

Same layering and `Myss_` env var prefix as MyssApi. The only Strapi token this
API holds is `Strapi:AdminApiToken` (write-scoped); MyssApi keeps the read-only
`Strapi:ApiToken`. Copy `appsettings.local.sample.json` to
`appsettings.local.json` for local dev; it listens on http://localhost:5010.

MyssApi caches the rate table it serves citizens for five minutes and cannot be
told about a save here, so a saved table reaches citizens within that window.
