import { useQuery } from "@tanstack/react-query";

import { getEstimatorRates, getEstimatorSpec } from "@/api/eligibility";

// React-query hooks for the public Pre-Eligibility Estimator. Option B: only the
// two READS are hooks — the estimate is a synchronous, local, pure call
// (`calculateEstimate`), so there is deliberately NO `useMutation` here.
//
// Both fetches are ANONYMOUS (see @/api/eligibility) — no Bearer interceptor,
// no auth header. The spec and the rate table are shared content, not per-user
// data, and change rarely, so they carry a long staleTime.

// Re-export the page's whole toolkit from one place: the reads, the pure
// mapper + pre-check gate (Step 4), and the calculator (Step 5) with its types.
export {
  getEstimatorSpec,
  getEstimatorRates,
  mapAnswersToEstimate,
  screenPreCheck,
} from "@/api/eligibility";
export type {
  AssetCategory,
  ClientType,
  EligibilityAssetLimits,
  EligibilityRateRow,
  EligibilityRates,
  EligibilityRequest,
  EligibilityResult,
  EstimatorSpecPayload,
  HouseholdType,
  PreCheckResult,
} from "@/api/eligibility";
export {
  assetLimitCategory,
  calculateEstimate,
  classifyClientType,
  familySize,
  incomeLimitFor,
  INELIGIBLE_ASSETS,
  INELIGIBLE_INCOME,
} from "@/lib/eligibilityCalculator";
export type { IncomeLimit } from "@/lib/eligibilityCalculator";

/** Content changes rarely and is not per-user, so keep it fresh for an hour. */
const CONTENT_STALE_TIME = 60 * 60 * 1000;

/** The latest published estimator form spec (anonymous read). */
export function useEstimatorSpec() {
  return useQuery({
    queryKey: ["estimator-spec"],
    queryFn: getEstimatorSpec,
    staleTime: CONTENT_STALE_TIME,
  });
}

/** The rate table the browser computes the estimate against (anonymous read). */
export function useEstimatorRates() {
  return useQuery({
    queryKey: ["estimator-rates"],
    queryFn: getEstimatorRates,
    staleTime: CONTENT_STALE_TIME,
  });
}
