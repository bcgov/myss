# Webclient end-to-end scenarios

Gherkin features run by Playwright Test through
[playwright-bdd](https://vitest-dev.github.io/playwright-bdd/). The browser
drives the real app served by Vite; only the API is stubbed, from the Strapi
seed, so the scenarios exercise the form citizens are actually served.

```
e2e/
  features/   *.feature — one per journey, scenarios tagged with the business rules they prove
  steps/      step definitions, speaking in questions and answers, never in selectors
  pages/      page objects that own the selectors and the page's vocabulary
  support/    API stubs built from the MyssContent seed
  fixtures.ts the Playwright test instance the steps run on
```

```bash
npm run test:e2e            # bddgen + playwright test (starts Vite on 5174)
npm run test:e2e:ui         # the same, in Playwright's UI mode
npm run test:e2e:report     # open the last Playwright report
MYSS_E2E_LIVE_API=1 npm run test:e2e   # no stubs: run against the configured API
```

`bddgen` writes the generated specs to `.features-gen/` (ignored). The
cucumber-style report lands in `e2e-report/index.html`.

## Nightly integration run

`.github/workflows/e2e-integration.yml` runs the same scenarios against a
deployed environment every day at 10:00 UTC (02:00 PST, 03:00 PDT), and on
demand from the Actions tab ("Run workflow", choosing `dev` or `test`). It sets
`MYSS_E2E_LIVE_API=1` and `E2E_BASE_URL` to the environment's webclient route,
so no Vite server is started and nothing is stubbed: the deployed `config.js`,
MyssApi and MyssContent are what the scenarios exercise. Reproduce a run
locally with:

```bash
MYSS_E2E_LIVE_API=1 \
E2E_BASE_URL=https://myss-webclient-e07f31-dev.apps.gold.devops.gov.bc.ca \
npm run test:e2e
```

The reports (HTML, cucumber and traces) are uploaded as the run's artifact.
Because the scenarios assert the seeded copy and rates, content published on
the target environment that differs from the seed fails them.
