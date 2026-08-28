import type {
  AssetCategory,
  ClientType,
  EligibilityRates,
  EligibilityRequest,
  EligibilityResult,
  HouseholdType,
} from "@/api/eligibility";

// The MYSS-25 eligibility calculation, ported verbatim from the parked C#
// EligibilityCalculator (features/myss-169-ee-form). Pure and dependency-free:
// rates are passed in, nothing is read or written. The estimate now runs in the
// browser (Option B), so this is the ported heart of the estimator and its
// numbers MUST match the parked `dotnet test` vectors exactly.
//
// Money is decimal-safe: all arithmetic is done in INTEGER CENTS (never JS
// floats), and formatted at the edge by the caller. The income scheme (A-E) and
// the asset scheme (A-D) are two DIFFERENT schemes — kept as two functions
// (Handbook §8 "A/B/C/D collision" footgun); never derive one from the other.

/** Total assets exceed the applicable asset ceiling (BR-D9-07). */
export const INELIGIBLE_ASSETS = "EST.INELIGIBLE.ASSETS";

/** Total income meets or exceeds the applicable income limit (BR-D9-08). */
export const INELIGIBLE_INCOME = "EST.INELIGIBLE.INCOME";

/** The rate table has rows up to family size 7; larger households use the "7+" row. */
const FAMILY_SIZE_CAP = 7;

/** Dollars -> integer cents, rounded (never trust a raw float multiply). */
function toCents(dollars: number): number {
  return Math.round(dollars * 100);
}

/** Integer cents -> dollars. */
function fromCents(cents: number): number {
  return cents / 100;
}

/**
 * Family unit size: adults (2 for a couple, else 1) plus dependants (BR-D9-03).
 * The rate lookup clamps this to 7; see `incomeLimitFor`.
 */
export function familySize(request: EligibilityRequest): number {
  const adults = request.relationshipStatus === "Couple" ? 2 : 1;
  return adults + request.dependants;
}

/**
 * BR-D9-04 (MYSS-25): classify the family unit as client type A-E. Decided
 * purely by single/couple and PWD status — DEPENDANTS NEVER AFFECT THE TYPE.
 * A = couple/neither, B = single/not-PWD, C = couple/either, D = single/PWD,
 * E = couple/both.
 */
export function classifyClientType(request: EligibilityRequest): ClientType {
  const isCouple = request.relationshipStatus === "Couple";

  if (isCouple) {
    if (request.applicantPwd && request.spousePwd) return "E";
    return request.applicantPwd || request.spousePwd ? "C" : "A";
  }

  return request.applicantPwd ? "D" : "B";
}

/**
 * BR-D9-06: pick the asset limit category A-D. A SEPARATE scheme from the income
 * type: both PWD -> D, either PWD -> C, else couple-or-has-dependants -> B, else A.
 */
export function assetLimitCategory(request: EligibilityRequest): AssetCategory {
  if (request.applicantPwd && request.spousePwd) return "D";
  if (request.applicantPwd || request.spousePwd) return "C";

  const isCouple = request.relationshipStatus === "Couple";
  return isCouple || request.dependants > 0 ? "B" : "A";
}

/** Income-limit lookup result: the limit (in cents) and whether family size was clamped. */
export interface IncomeLimit {
  limitCents: number;
  clamped: boolean;
}

/**
 * Look up the monthly income limit for a client type at a family size against
 * the FETCHED table. Clamps family size to 7 (the "7+" row) and reports it. A
 * missing row or column is a SURFACED ERROR — never a silent zero.
 */
export function incomeLimitFor(
  clientType: ClientType,
  size: number,
  rates: EligibilityRates,
): IncomeLimit {
  const clamped = size > FAMILY_SIZE_CAP;
  const lookupSize = clamped ? FAMILY_SIZE_CAP : size;

  const row = rates.incomeRows.find((r) => r.familySize === lookupSize);
  if (!row) {
    throw new Error(`No income-limit row for family size ${lookupSize}`);
  }

  const column = clientType.toLowerCase() as "a" | "b" | "c" | "d" | "e";
  const limit = row[column];
  if (typeof limit !== "number") {
    throw new Error(
      `No income limit for client type ${clientType} at family size ${lookupSize}`,
    );
  }

  return { limitCents: toCents(limit), clamped };
}

/**
 * Asset ceiling (in cents) for a category. A missing category is a surfaced
 * error, never a silent zero.
 */
function assetLimitCentsFor(
  category: AssetCategory,
  rates: EligibilityRates,
): number {
  const column = category.toLowerCase() as "a" | "b" | "c" | "d";
  const limit = rates.assetLimits[column];
  if (typeof limit !== "number") {
    throw new Error(`No asset limit for category ${category}`);
  }
  return toCents(limit);
}

/**
 * The estimate as a pure function of (request, rates). Asset gate is checked
 * FIRST (BR-D9-07): assets over the ceiling disqualify outright (equal to the
 * ceiling still passes). Otherwise the benefit is what's left of the income
 * limit (BR-D9-08): `<= 0` is ineligible, else eligible.
 */
export function calculateEstimate(
  request: EligibilityRequest,
  rates: EligibilityRates,
): EligibilityResult {
  const clientType = classifyClientType(request);
  const size = familySize(request);
  const householdType: HouseholdType = request.relationshipStatus;
  const familySizeClamped = size > FAMILY_SIZE_CAP;

  const totalIncomeCents =
    toCents(request.monthlyIncome) + toCents(request.spouseMonthlyIncome);
  const totalAssetsCents =
    toCents(request.primaryVehicleValue) +
    toCents(request.otherVehicleValue) +
    toCents(request.otherAssetValue);

  const totalIncome = fromCents(totalIncomeCents);
  const totalAssets = fromCents(totalAssetsCents);

  const base = {
    clientType,
    familySize: size,
    familySizeClamped,
    householdType,
    monthlyIncome: totalIncome,
    totalAssets,
  };

  // BR-D9-07: the asset gate is checked first — assets disqualify outright.
  const assetCeilingCents = assetLimitCentsFor(assetLimitCategory(request), rates);
  if (totalAssetsCents > assetCeilingCents) {
    return {
      ...base,
      eligible: false,
      estimatedAmount: 0,
      ineligibilityReasonKeyword: INELIGIBLE_ASSETS,
    };
  }

  // BR-D9-05 / BR-D9-08: benefit is what's left of the income limit.
  const { limitCents } = incomeLimitFor(clientType, size, rates);
  const benefitCents = limitCents - totalIncomeCents;
  if (benefitCents <= 0) {
    return {
      ...base,
      eligible: false,
      estimatedAmount: 0,
      ineligibilityReasonKeyword: INELIGIBLE_INCOME,
    };
  }

  return {
    ...base,
    eligible: true,
    estimatedAmount: fromCents(benefitCents),
    ineligibilityReasonKeyword: null,
  };
}
