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
