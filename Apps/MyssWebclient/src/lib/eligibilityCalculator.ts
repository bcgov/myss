import type {
  AssetCategory,
  AssetColumn,
  ClientType,
  EligibilityRates,
  EligibilityRequest,
  EligibilityResult,
  HouseholdType,
  IncomeColumn,
} from "@/api/eligibility";

// The eligibility calculation. Pure and dependency-free: rates are passed in,
// nothing is read or written. The estimate runs in the browser; MyssApi only
// serves the form spec and the rate table.
//
// Money is decimal-safe: all arithmetic is done in INTEGER CENTS (never JS
// floats), and formatted at the edge by the caller. The income scheme (A-I) and
// the asset scheme (A-D) are two DIFFERENT schemes — kept as two functions;
// never derive one from the other.

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

/** How one adult counts towards the client type. */
type AdultStatus = "pwd" | "senior" | "neither";

/** PWD takes priority over 65+, at any age. */
function adultStatus(pwd: boolean, senior: boolean): AdultStatus {
  if (pwd) return "pwd";
  return senior ? "senior" : "neither";
}

const SINGLE_TYPES: Record<AdultStatus, ClientType> = {
  neither: "B",
  senior: "E",
  pwd: "G",
};

// Symmetric: the applicant and the spouse can swap places.
const COUPLE_TYPES: Record<AdultStatus, Record<AdultStatus, ClientType>> = {
  neither: { neither: "A", senior: "D", pwd: "F" },
  senior: { neither: "D", senior: "C", pwd: "I" },
  pwd: { neither: "F", senior: "I", pwd: "H" },
};

/**
 * Classify the family unit as client type A-I from single/couple and
 * each adult's status (PWD, else 65+, else neither). DEPENDANTS NEVER AFFECT
 * THE TYPE.
 */
export function classifyClientType(request: EligibilityRequest): ClientType {
  const applicant = adultStatus(request.applicantPwd, request.applicantSenior);
  if (request.relationshipStatus !== "Couple") return SINGLE_TYPES[applicant];

  const spouse = adultStatus(request.spousePwd, request.spouseSenior);
  return COUPLE_TYPES[applicant][spouse];
}

/**
 * Pick the asset limit category A-D. A SEPARATE scheme from the income
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

  const column = clientType.toLowerCase() as IncomeColumn;
  const limit = row[column];
  if (typeof limit !== "number") {
    throw new TypeError(
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
  const column = category.toLowerCase() as AssetColumn;
  const limit = rates.assetLimits[column];
  if (typeof limit !== "number") {
    throw new TypeError(`No asset limit for category ${category}`);
  }
  return toCents(limit);
}

/**
 * The estimate as a pure function of (request, rates). Asset gate is checked
 * FIRST: assets over the ceiling disqualify outright (equal to the
 * ceiling still passes). Otherwise the benefit is what's left of the income
 * limit: `<= 0` is ineligible, else eligible.
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

  // the asset gate is checked first — assets disqualify outright.
  const assetCeilingCents = assetLimitCentsFor(assetLimitCategory(request), rates);
  if (totalAssetsCents > assetCeilingCents) {
    return {
      ...base,
      eligible: false,
      estimatedAmount: 0,
      ineligibilityReasonKeyword: INELIGIBLE_ASSETS,
    };
  }

  // the benefit is what's left of the income limit.
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
