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
- The client half of the validation rule registry (`validationRules.ts`).
- The spec helpers the admin editor uses (`builderOptions`, `specEdit`,
  `specPreview`, `specRules`).
- `@myss/formio/errorCatalogue`: the error message catalogue the components word
  rule failures from. The citizen webclient fills it from
  `/v1/forms/error-messages`; the admin webclient leaves it empty, so the
  preview uses the compiled default wording.
- `@myss/formio/phone` and `PhoneInput`: phone number rules and input, also used
  by the citizen app's own forms.
- `ReadOnlySpecForm`: a spec drawn read-only over submitted answers, with its
  buttons removed. The citizen app shows an applicant their submitted
  application with it; the admin app shows it to the reviewing worker.

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

`validationRules.unit.test.ts` reads the shared validation vectors and error
message catalogue from `Shared/validation/`, the same files the API's tests
read.
