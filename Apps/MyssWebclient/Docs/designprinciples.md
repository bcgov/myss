# Design principles

> **Status:** convention — adopted 2026-09-09. The token rules describe
> the target; existing CSS still carries `var(--token, fallback)`
> literals and stray hex values that should be removed as files are
> touched.

The rules that keep the UI one system instead of a collection of
look-alikes. Structural rules live in
[frontend-architecture.md](frontend-architecture.md); the accessibility
commitment and its practices live in
[accessibility.md](accessibility.md). The component library is
`@bcgov/design-system-react-components`.

## Design tokens: defined once, consumed everywhere

`@bcgov/design-tokens` is the **single source of truth**. Its
`variables.css` is imported once in `main.tsx`; everything else consumes
the custom properties.

- **Use the variable, trust the variable.** Write
  `var(--layout-margin-medium)` — never `var(--token, 16px)`. Fallback
  literals mask a missing import and drift silently (we have shipped a
  `19px` "large padding" this way).
- **No raw values where a token exists.** `#ffffff`, `4px`, ad-hoc font
  sizes — if the design system names it, use the name. A hex in a
  review diff is a question to answer, not a style choice.
- **Never re-declare tokens** — not in CSS, not in Sass, not in JS
  constants. A second definition is where the fork starts.
- Global type and theme layers (`ui/typography.css`, `ui/themes.css`)
  are _consumers_ of tokens, not definers.

## Components: BCDS first, wrap don't rebuild

Before building anything in `src/components/`, check
`@bcgov/design-system-react-components`. If it exists there, wrap it
(to fix a prop surface or add a MySS default) rather than re-implement
it. Build only what BCDS lacks — the MySS-specific patterns: `SinInput`,
status pills, the attachment list, the error summary.

Variants are **props on one component**, not sibling files.

## One system across two render worlds

Forms arrive two ways: hand-built React screens, and Form.io-rendered
specs from Strapi (via the custom components in `widgets/formio/`).
The contract: a SIN field looks and behaves identically in both,
because both draw on the same components and tokens. Therefore:

- Form.io custom components render **real** shared components (the
  `bcgovAccordion` bridge mounts the actual BCDS `Accordion`), never
  CSS look-alikes.
- Any styling reachable by Form.io-rendered DOM uses the same token
  variables as everything else.

## Contribution model

When a feature needs a new pattern, it is **added to the shared tiers**
(components/widgets, reviewed like any code) — not invented locally
inside a page. A pattern that exists twice with two spellings is a
defect. This is what keeps the system from fragmenting as domains are
added.

## Accessibility

WCAG 2.1 AA is a hard commitment and a Definition-of-Done item; a
regression in an accessible behaviour is a defect, not a tweak. The
patterns, the BCDS footgun list, and the testing practices live in
[accessibility.md](accessibility.md).
