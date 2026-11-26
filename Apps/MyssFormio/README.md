# @myss/formio

The BC Gov Form.io components, shared by the two webclients:

- **MyssWebclient** renders citizen forms (bus pass, registration, the
  eligibility estimator) with them.
- **MyssAdminWebclient** previews a form spec with them in the form editor.

Both apps register the same components, so a spec looks and validates the same
in the editor's preview as it does for citizens.

## What is in it

- `registerBcgovComponents()`: re-registers every stock Form.io input type with
  a BC Design System wrapper, plus the BC Gov custom types (`bcgovAccordion`,
  `bcgovCallout`, `bcgovAddressAutocomplete`, …). The host app calls it once
  from `main.tsx`, passing its Canada Post AddressComplete key.
- `bcgovRepeatSection` (`repeatSection.ts`): a group of questions asked once
  per unit of a number question's answer, on Form.io's data grid. Citizens see
  one headed group per row; the builder draws it stacked, like a panel.
  `findInvalidRepeatSections` in `specRules.ts` is the editor's copy of its
  publish rule (see `Shared/validation/repeat-section-specs.json`).
- Channel rules (`requestChannels.ts`, `channelSpec.ts`): a component's rules
  per request channel, `client` or `worker-override`, under
  `properties.myssChannels`, such as
  `{ "worker-override": { "show": false } }`; only `show` and `required`
  exist. The builder's Channel rules tab writes them (`builderOptions`),
  `describeChannelRules` (`specEdit`) words them for the editor's rows,
  `resolveSpecForChannel` is the editor's copy of the API's
  `ChannelSpec.Resolve` for its preview, and `findInvalidChannelRules` in
  `specRules.ts` is the editor's copy of their publish rule (see
  `Shared/validation/channel-specs.json`). Only the intake services resolve a
  form for a channel, so the rules apply to applications for assistance only
  (`CHANNEL_RULES_SCOPE`, shown in both help texts). The designer's notes are
  in the admin webclient's README.
- The client half of the validation rule registry (`validationRules.ts`).
- The spec helpers the admin editor uses (`builderOptions`, `specEdit`,
  `specPreview`, `specRules`, `specImport` for pasted-JSON imports), and
  `specWalk`, the component-tree walk they share.
- `@myss/formio/errorCatalogue`: the error message catalogue the components word
  rule failures from. The citizen webclient fills it from
  `/v1/forms/error-messages`; the admin webclient leaves it empty, so the
  preview uses the compiled default wording.
- `@myss/formio/phone` and `PhoneInput`: phone number rules and input, also used
  by the citizen app's own forms.
- `ReadOnlySpecForm`: a spec drawn read-only over submitted answers, with its
  buttons removed. The citizen app shows an applicant their submitted
  application with it; the admin app shows it to the reviewing worker.
- `@myss/formio/Callout.module.css`: the callout styles, exported so the
  citizen app's own `Callout` matches `bcgovCallout`.

## How it is consumed

As TypeScript source: `package.json` `exports` point at `src/`, and there is no
build step. Each app's Vite build compiles it, and each app's `tsc -b`
type-checks the files it imports. React, Form.io and the design system are peer
dependencies, so each app's copy is the one used.

Inside the package, imports are relative (no `@/` alias), and nothing reads an
app's runtime config: what the components need is passed to
`registerBcgovComponents`.

## Scripts

Run from `Apps/` with `-w @myss/formio`, or from this directory.

| Script                          | Purpose                                 |
| ------------------------------- | --------------------------------------- |
| `npm run build`                 | Type-check (`tsc -b`), tests included   |
| `npm run lint`                  | Run ESLint                              |
| `npm run test:unit`             | Unit tests (Vitest, node)               |
| `npm run test:browser`          | Browser tests (Vitest + Playwright)     |
| `npm run test:browser-headless` | Browser tests, headless                 |
| `npm run test:coverage`         | All tests, headless, with coverage      |

`validationRules.unit.test.ts` reads the shared validation vectors and error
message catalogue from `Shared/validation/`, the same files the API's tests
read.
