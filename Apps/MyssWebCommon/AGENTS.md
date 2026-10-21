# AGENTS.md — @myss/web-common

Sign-in plumbing, runtime settings, API shapes and small components shared by
MyssWebclient and MyssAdminWebclient. The workspace-wide rules in the repo root
[AGENTS.md](../../AGENTS.md) and the webclient's
[AGENTS.md](../MyssWebclient/AGENTS.md) apply; this file adds what is specific
to the package.

- **Both apps run this code.** A change here changes the citizen site and the
  admin site at once. Run both apps' suites as well as this one.
- **No app imports.** Relative imports only, no `@/` alias, no route paths, and
  no reading of `import.meta.env`. What differs between the apps (return paths,
  the API address, the `/auth/me` payload) is a parameter; each app binds it in
  its own thin module.
- **Only what both apps use.** Code used by one app stays in that app.
- **Keep node-safe entry points light.** `@myss/web-common/config`, `/auth` and
  `/api` are imported by node-environment unit tests in the apps; they must not
  import CSS or components. Components go in `/components`.
- **Public surface is the `exports` in `package.json`** (each points at an
  `index.ts` barrel, or `config.ts`). Add an export there before an app imports
  it.
- `npm run build` (`tsc -b`) is the full type-check; vitest does not check
  types.
