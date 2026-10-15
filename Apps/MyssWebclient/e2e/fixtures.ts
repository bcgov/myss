// playwright-bdd's `test` is Playwright's, extended with the bdd fixtures the
// generated specs need; app fixtures are layered on top of it.
import { createBdd, test as base } from "playwright-bdd";

import { EstimatorPage } from "./pages/EstimatorPage";

type Fixtures = {
  estimator: EstimatorPage;
};

// The Playwright test instance the generated specs run on. Step definitions
// receive these fixtures (plus Playwright's own, such as `page`) as their
// first argument.
export const test = base.extend<Fixtures>({
  estimator: async ({ page }, use) => {
    await use(new EstimatorPage(page));
  },
});

export const { Given, When, Then } = createBdd(test);
