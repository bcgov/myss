# AGENTS.md — MyssWebclient

Guidance for AI agents working in this app. The workspace-wide rules in
the repo root [AGENTS.md](../../AGENTS.md) apply in full; this file adds
what is specific to the webclient. Full docs:
[Docs/frontend-architecture.md](Docs/frontend-architecture.md),
[Docs/designprinciples.md](Docs/designprinciples.md) and
[Docs/accessibility.md](Docs/accessibility.md).

## The rules that catch most mistakes

1. **Dependency rule:** pages compose widgets compose components;
   imports only point down the hierarchy, never up, never sideways
   across pages. Shared things get promoted down a tier.
2. **Tokens:** consume `@bcgov/design-tokens` custom properties bare —
   `var(--layout-margin-medium)` with **no literal fallback**, no raw
   hex/px where a token exists, and never re-declare a token anywhere.
3. **BCDS first:** before creating a component, check
   `@bcgov/design-system-react-components`; wrap it rather than rebuild
   it. Variants are props, not sibling files.
4. **`npm run build` is the only full typecheck** (`tsc -b`): vitest
   strips types without checking them, so run the build before
   considering any change done. CI runs it on every PR.
5. **Accessibility regressions are defects** (WCAG 2.1 AA): no
   `div`-with-`onClick`, no dropped ARIA, no contrast-breaking
   overrides, focus styles stay token-driven. Drive tests by accessible
   role, not DOM name (Form.io randomizes names).

## Layout of `src/`

`pages/` (routed screens + page-scoped logic) · `widgets/` (composed
sections; `widgets/formio/` is the Form.io custom-component bridge) ·
`components/` (shared presentational leaves) · `hooks/` (`useX` naming)
· `routes/paths.ts` (every URL) · `ui/` (global CSS layers) · `api/`
(generated — regenerate with `npm run generate:schema`, never
hand-edit) · `auth/` · `lib/` · `constants.ts` (config + external
links, the only registry).

Migration status and the current→target mapping are in
[Docs/frontend-architecture.md](Docs/frontend-architecture.md) — new
code follows the target structure even where old code has not moved yet.

## Conventions

- Component files PascalCase, no `Component` suffix (`Button.tsx`,
  `SinInput.tsx`); folders lowercase.
- Styling is CSS Modules beside the component; global layers live in
  `ui/` and are imported once from `main.tsx`.
- Tests colocated: `*.unit.test.ts` (node) / `*.browser.test.tsx`
  (Playwright); the vitest projects select on those suffixes.
- Path alias `@` → `src`.
- Runtime config: `window.APP_CONFIG` → `VITE_*` → default; adding a
  value touches `entrypoint.sh`, `public/config.js` and
  `src/constants.ts` together.
