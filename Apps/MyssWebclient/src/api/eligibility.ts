import type { FormType } from "@formio/react/lib/components/Form";

import { API_URL } from "@/constants";

// Calls to the public Pre-Eligibility Estimator gateway
// (/v1/EligibilityEstimator). Option B: the browser computes the estimate, so
// MyssApi only serves the Form.io spec and the rate table — there is no
// POST /calculate. Both reads are ANONYMOUS by design (the estimator is public
// and persists nothing): these fetches carry NO auth header intentionally. Do
// not add authHeaders() here.
//
// This module also holds the pure, unit-tested glue between the rendered form
// and the calculator: mapAnswersToEstimate (Form.io answers -> EligibilityRequest)
// and screenPreCheck (the residency/status hard gate). The calculation itself
// lives in @/lib/eligibilityCalculator.

/** Household composition after collapsing the six relationship options. */
export type HouseholdType = "Single" | "Couple";

/** MYSS-25 income client type (A-E). A separate axis from the asset category. */
export type ClientType = "A" | "B" | "C" | "D" | "E";

/** MYSS-25 asset limit category (A-D). A separate axis from the income type. */
export type AssetCategory = "A" | "B" | "C" | "D";

/**
 * The calculator's flat input. Mirrors the parked C# EligibilityRequest minus
 * the server-only validation attributes. Spouse fields are 0/false for a Single
 * household; the three asset fields already combine applicant + spouse values.
 */
export interface EligibilityRequest {
  relationshipStatus: HouseholdType;
  dependants: number;
  applicantPwd: boolean;
  spousePwd: boolean;
  monthlyIncome: number;
  spouseMonthlyIncome: number;
  primaryVehicleValue: number;
  otherVehicleValue: number;
  otherAssetValue: number;
}

/** One family-size row of monthly income limits by client type (MYSS-25). */
export interface EligibilityRateRow {
  familySize: number;
  a: number;
  b: number;
  c: number;
  d: number;
  e: number;
}

/** The asset ceilings by category A-D. */
export interface EligibilityAssetLimits {
  a: number;
  b: number;
  c: number;
  d: number;
}

/**
 * The rate table the browser computes against. Shape mirrors the JSON served by
 * GET /rates (camelCase, single-letter lowercase columns) — see MyssApi
 * EligibilityRatesModel.
 */
export interface EligibilityRates {
  effectiveDate: string;
  incomeRows: EligibilityRateRow[];
  assetLimits: EligibilityAssetLimits;
}

/**
 * The estimate. Mirrors the old server response MINUS the dropped
 * Support/Shelter breakdown (0826 removed the itemised table). `monthlyIncome`
 * echoes the TOTAL household income, as the parked C# response did.
 */
export interface EligibilityResult {
  eligible: boolean;
  estimatedAmount: number;
  clientType: ClientType;
  ineligibilityReasonKeyword: string | null;
  familySize: number;
  familySizeClamped: boolean;
  householdType: HouseholdType;
  monthlyIncome: number;
  totalAssets: number;
}

/** The published estimator spec payload from GET /spec. */
export interface EstimatorSpecPayload {
  formSpecId: string;
  version: number;
  title?: string | null;
  spec: FormType;
}

/**
 * Result of the residency/status hard screen. A "No" to either question is a
 * hard eligibility screen the page short-circuits WITHOUT running the
 * calculation (no EligibilityRequest is built).
 */
export interface PreCheckResult {
  residesInBc: boolean;
  hasEligibleStatus: boolean;
  passed: boolean;
}

/** The two relationship values that reveal the spouse section and count as a couple. */
const COUPLE_STATUSES = new Set(["married", "marriagelike"]);

/** Collapse the six relationship options to the two the calculator cares about. */
function householdTypeFrom(status: unknown): HouseholdType {
  return typeof status === "string" && COUPLE_STATUSES.has(status)
    ? "Couple"
    : "Single";
}

/** yes/no radios seed the string "true"/"false"; anything else is treated as No/false. */
function toBool(value: unknown): boolean {
  return value === true || value === "true";
}

/** Coerce a money answer to a non-negative number; blank/NaN/negative -> 0. */
function toMoney(value: unknown): number {
  const n = typeof value === "number" ? value : parseFloat(String(value));
  if (!Number.isFinite(n) || n < 0) return 0;
  return n;
}

/** Coerce a count answer to a non-negative integer; blank/NaN/negative -> 0; truncates. */
function toCount(value: unknown): number {
  const n = typeof value === "number" ? value : parseFloat(String(value));
  if (!Number.isFinite(n) || n < 0) return 0;
  return Math.trunc(n);
}

/**
 * Turn Form.io answers into an EligibilityRequest. Pure and defensive:
 * - collapses the six relationship values to Single/Couple;
 * - a Single household forces every spouse field to 0/false;
 * - coerces "true"/"false" -> boolean, blanks/negatives -> 0, dependants -> integer;
 * - sums applicant + spouse into each of the three combined asset fields.
 *
 * Field KEYS here are half of the seed<->frontend contract (see the plan §3);
 * they must match the seeded spec exactly.
 */
export function mapAnswersToEstimate(
  answers: Record<string, unknown>,
): EligibilityRequest {
  const relationshipStatus = householdTypeFrom(answers.relationshipStatus);
  const isCouple = relationshipStatus === "Couple";

  return {
    relationshipStatus,
    dependants: toCount(answers.dependentChildren),
    applicantPwd: toBool(answers.pwd),
    spousePwd: isCouple ? toBool(answers.partnerPwd) : false,
    monthlyIncome: toMoney(answers.monthlyIncome),
    spouseMonthlyIncome: isCouple ? toMoney(answers.partnerMonthlyIncome) : 0,
    primaryVehicleValue:
      toMoney(answers.vehicleValueMinusTransportation) +
      (isCouple ? toMoney(answers.partnerVehicleValueMinusTransportation) : 0),
    otherVehicleValue:
      toMoney(answers.vehicleValue) +
      (isCouple ? toMoney(answers.partnerVehicleValue) : 0),
    otherAssetValue:
      toMoney(answers.assetValue) +
      (isCouple ? toMoney(answers.partnerAssetValue) : 0),
  };
}

/**
 * The residency/citizenship hard screen. Passes only when BOTH answers are
 * "Yes"; a "No" to either fails the screen. The page runs this first and, on a
 * failure, shows the not-eligible outcome without building a request.
 */
export function screenPreCheck(answers: Record<string, unknown>): PreCheckResult {
  const residesInBc = toBool(answers.residesInBc);
  const hasEligibleStatus = toBool(answers.hasEligibleStatus);
  return {
    residesInBc,
    hasEligibleStatus,
    passed: residesInBc && hasEligibleStatus,
  };
}

/**
 * The latest published estimator spec. Anonymous — no auth header (intentional).
 */
export async function getEstimatorSpec(): Promise<EstimatorSpecPayload> {
  const res = await fetch(`${API_URL}/v1/EligibilityEstimator/spec`);
  if (!res.ok) throw new Error(`Estimator spec fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/**
 * The rate table the browser computes against. Anonymous — no auth header
 * (intentional). This is a DATA fetch (the limits), not a calculation.
 */
export async function getEstimatorRates(): Promise<EligibilityRates> {
  const res = await fetch(`${API_URL}/v1/EligibilityEstimator/rates`);
  if (!res.ok) throw new Error(`Estimator rates fetch failed (${res.status})`);
  return (await res.json()).payload;
}
