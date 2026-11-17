import type { Page } from "@playwright/test";

import { seededErrorMessages } from "../../../MyssContent/src/lib/error-message-seed-data";
import { eligibilityRateOctober2026 } from "../../../MyssContent/src/lib/eligibility-rate-seed-data";
import {
  ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
  seededForms,
} from "../../../MyssContent/src/lib/form-spec-seed-data";

// The two anonymous reads the estimator page makes (src/api/eligibility.ts),
// answered from the Strapi SEED read straight out of MyssContent at test time.
// Same linked-not-copied rule as the validation vectors and the bus pass
// specs: the scenarios drive the form citizens are actually served, and a
// seed change that breaks the page breaks these scenarios.
//
// Set MYSS_E2E_LIVE_API=1 to leave the requests alone and run the same
// scenarios against whatever API the webclient is configured for.

const SPEC_ROUTE = "**/v1/EligibilityEstimator/spec";
const RATES_ROUTE = "**/v1/EligibilityEstimator/rates";

// The two anonymous reads every page makes before it renders: the feature
// flags (src/api/features.ts) and the error message catalogue
// (src/api/errorMessages.ts). Without an answer the app shell stays empty.
const FEATURES_ROUTE = "**/v1/features";
const ERROR_MESSAGES_ROUTE = "**/v1/forms/error-messages";

/**
 * The flags these scenarios need, shaped as GET /v1/features serves it. A flag
 * left out reads as off (src/api/features.ts), which keeps the stub from
 * importing app code into the e2e typecheck.
 */
export const estimatorFeatures = { EligibilityEstimator: true };

/** The seeded catalogue, keyword to wording, as GET /error-messages serves it. */
export const seededErrorMessageCatalogue = Object.fromEntries(
  seededErrorMessages.map((row) => [row.keyword, row.message]),
);

/** The newest seeded estimator version, shaped as GET /spec serves it. */
export function latestSeededEstimatorSpec() {
  const form = seededForms.find(
    (f) => f.formSpecId === ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
  );
  if (!form) {
    throw new Error(`No seeded form ${ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID}`);
  }
  const latest = [...form.versions].sort((a, b) => b.version - a.version)[0];
  return {
    formSpecId: form.formSpecId,
    version: latest.version,
    title: form.title,
    spec: latest.spec,
  };
}

/** The rate table as GET /rates serves it. */
export const seededRates = eligibilityRateOctober2026;

export function isLiveApi(): boolean {
  return Boolean(process.env.MYSS_E2E_LIVE_API);
}

/** Answer the shell's and the estimator's reads from the seed, unless running live. */
export async function stubEstimatorApi(page: Page): Promise<void> {
  if (isLiveApi()) return;
  await page.route(FEATURES_ROUTE, (route) =>
    route.fulfill({ json: { payload: estimatorFeatures } }),
  );
  await page.route(ERROR_MESSAGES_ROUTE, (route) =>
    route.fulfill({ json: { payload: seededErrorMessageCatalogue } }),
  );
  await page.route(SPEC_ROUTE, (route) =>
    route.fulfill({ json: { payload: latestSeededEstimatorSpec() } }),
  );
  await page.route(RATES_ROUTE, (route) =>
    route.fulfill({ json: { payload: seededRates } }),
  );
}
