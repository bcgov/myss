# MyssContent

The **Strapi 5** content service for MySS. Ministry staff manage the content the
apps serve here: form specs, form messages, error messages and the eligibility
estimator's rate table. It is a standalone npm project (its own
`package-lock.json`), not part of the `Apps/` workspace.

## Content types

Each lives under `src/api/<name>/` (schema, controller, routes, services):

| Type                 | What it holds                                         |
| -------------------- | ----------------------------------------------------- |
| `form-spec`          | Versioned Form.io form specs                          |
| `form-message`       | The texts a form's page shows applicants              |
| `error-message`      | The validation error message catalogue                |
| `eligibility-rate`   | The eligibility estimator's rate table                |
| `testing-collection` | A scratch collection for testing                      |

## Seed data and rules

`src/lib/` holds the seed data and the rules that check it; `src/index.ts`
seeds at bootstrap:

- `form-spec-seed-data.ts` (with the bus pass specs in `bus-pass-specs/`) and
  `form-spec-rules.ts`. Every seeded form version is published; a changed spec
  rolls out as a **new** version in the seed, never by editing a published one,
  and bootstrap refuses to start if a seed disagrees with a published version.
- `eligibility-rate-seed-data.ts`, `eligibility-rate-seeding.ts` and
  `eligibility-rate-rules.ts`.
- `error-message-seed-data.ts`.

The `*.test.ts` files beside them are the Vitest suite.

## Developer setup

- Node.js 20 to 26 (`package.json` `engines`; the Dockerfile uses Node 22).
- Run `npm install` in this directory.
- Copy `.env.example` to `.env`. Its placeholder secrets are fine locally.
- The database is Postgres (`DATABASE_CLIENT=postgres`, `DATABASE_HOST`,
  `DATABASE_PORT`, `DATABASE_NAME`, `DATABASE_USERNAME`, `DATABASE_PASSWORD`,
  `DATABASE_SSL`; `DATABASE_URL` also works). `config/database.ts` also accepts
  `mysql` and `sqlite`, and defaults to `sqlite` when `DATABASE_CLIENT` is unset.
- Either run the root `compose.yaml` (the `strapi` service, on port 1337,
  against the compose `postgres`), or start Postgres and run `npm run dev`.
  The admin panel is at <http://localhost:1337/admin>.

## Scripts

| Script                  | Purpose                                     |
| ----------------------- | ------------------------------------------- |
| `npm run dev`           | Start Strapi with auto-reload (`develop`)   |
| `npm run develop`       | Same as `dev`                               |
| `npm run build`         | Build the admin panel                       |
| `npm run start`         | Start without auto-reload (production)      |
| `npm run console`       | Strapi console                              |
| `npm run deploy`        | Strapi Cloud deploy                         |
| `npm run strapi`        | The Strapi CLI                              |
| `npm test`              | Run the Vitest suite once                   |
| `npm run test:watch`    | Vitest in watch mode                        |
| `npm run test:coverage` | Vitest with coverage                        |
| `npm run upgrade`       | Upgrade Strapi (`npm run upgrade:dry` first) |
