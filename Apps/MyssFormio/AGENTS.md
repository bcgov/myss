# AGENTS.md — @myss/formio

The Form.io components shared by MyssWebclient and MyssAdminWebclient. The
workspace-wide rules in the repo root [AGENTS.md](../../AGENTS.md) and the
webclient's [AGENTS.md](../MyssWebclient/AGENTS.md) apply; this file adds what
is specific to the package.

- **Both apps render through this code.** A change shows up in the citizen
  forms and in the admin editor's preview at once; that is the point of the
  package. Run both apps' browser suites as well as this one.
- **No app imports.** Relative imports only, no `@/` alias, and no reading of
  `window.APP_CONFIG` or `import.meta.env`. Anything app-specific is passed in
  through `registerBcgovComponents` options (see `config.ts`).
- **Keep node-safe entry points light.** `@myss/formio/errorCatalogue` and
  `@myss/formio/phone` are imported by node-environment unit tests in the
  apps; they must not import Form.io, React or CSS.
- **Public surface is `src/index.ts`** plus the subpaths in `package.json`
  `exports`. Add an export there before an app imports it.
- `npm run build` (`tsc -b`) is the full type-check; vitest does not check
  types.
- The validation rules here are the client half of a two-sided contract; the
  server half is `FormSpecValidator`. Both are pinned by
  `Shared/validation/validation-vectors.json` and `error-messages.json`.
