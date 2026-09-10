# Frontend architecture

> **Status:** convention — adopted 2026-09-09. New code follows it now;
> the folder migration (see the table at the bottom) has **not** been
> executed yet, so parts of the tree still show the old layout.

How `Apps/MyssWebclient` is organized and how the pieces are allowed to
depend on each other. Design rules (tokens, accessibility, contribution
model) live in [designprinciples.md](designprinciples.md); agent-facing
rules are summarized in [../AGENTS.md](../AGENTS.md).

## The composition hierarchy

Three tiers, one dependency rule:

> **Pages compose widgets; widgets compose components. Imports only point
> down the hierarchy — never up, never sideways across pages.**

- **Page** — a routed screen (`src/pages/`). Owns route-level concerns:
  data fetching orchestration, page layout, wiring widgets together.
  Page-scoped logic lives next to the page (`estimatorCalculator.ts`
  beside `EligibilityEstimatorPage.tsx`), with its tests alongside.
- **Widget** — a composed section of a page (`src/widgets/`): the account
  panel, the cheque calendar, the announcement banner, the Form.io bridge.
  A widget may use hooks and be data-aware, and may appear on more than
  one page.
- **Component** — a shared presentational leaf (`src/components/`):
  props in, markup out. No data fetching, no routing. This is where
  MySS-specific patterns live (`SinInput`, status pills, the error
  summary), with variants expressed as props — not parallel files.

If two pages need the same thing, it moves _down_ a tier; nothing ever
reaches _up_ or _across_. When a folder move feels ambiguous, the
question to ask is "who else is allowed to import this?"

## Folder structure

```
src/
  pages/            routed screens + page-scoped logic and tests
  widgets/          composed sections (incl. widgets/formio/ — the
                    Form.io custom-component bridge)
  components/       shared presentational components (BCDS-first)
  hooks/            shared behaviour hooks (useX naming)
  routes/           paths.ts — every URL in one place
  ui/               global style layers: typography.css, themes.css
                    (plain CSS consuming design-token variables)
  api/              generated OpenAPI client (never hand-edited)
  auth/             auth feature module (provider, guards, session)
  lib/              shared pure utilities
  constants.ts      runtime config + external link registry
```

Notes:

- Folder names stay lowercase (matches the existing tree); component
  files are PascalCase (`Button.tsx`, `SinInput.tsx` — no `Component`
  suffix, no underscores).
- External links and config both live in `constants.ts` — deliberately
  one registry, so link URLs cannot fork into a second home.
- `ui/` holds no fonts: BC Sans comes from `@bcgov/bc-sans`, tokens from
  `@bcgov/design-tokens` (see designprinciples.md). No SCSS: token
  delivery is CSS custom properties, and a Sass variable layer would be
  a second source of truth.

## Styling

CSS Modules per component/widget/page (`X.module.css` beside `X.tsx`),
consuming `--bcds` custom properties directly. Global layers
(`ui/typography.css`, `ui/themes.css`) are imported once from
`main.tsx`. Token rules — no literal fallbacks, no raw hex — are in
designprinciples.md.

## Tests

Colocated with the code under test, selected by suffix (the vitest
projects key on these):

- `X.unit.test.ts` — node
- `X.browser.test.tsx` — Playwright/chromium

`npm run build` (`tsc -b && vite build`) is the **only full typecheck**
in the workflow — vitest transpiles without checking types. Run it
before pushing; CI runs it on every PR.

## Migration from the current tree

New code follows this structure now. Existing files move in a dedicated
mechanical PR (pure renames, no behaviour) scheduled when few feature
branches are open, to minimize merge conflicts:

| Today                                                 | Target                                               |
| ----------------------------------------------------- | ---------------------------------------------------- |
| `components/home/*` (AccountPanel, ChequeCalendar, …) | `widgets/home/*`                                     |
| `components/layout/*` (AnnouncementBanner, …)         | `widgets/layout/*`                                   |
| `components/AttachmentUpload.*`                       | `widgets/`                                           |
| `formio/`                                             | `widgets/formio/`                                    |
| `data/`                                               | fold into `lib/` or the owning page                  |
| (new)                                                 | `components/` grows the shared leaves as they appear |
