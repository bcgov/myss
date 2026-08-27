// The MYSS-25 (US-ELG-02) income-assistance rate table the estimator computes
// against. These values MUST stay identical to Apps/MyssApi/Data/FddRateData.cs
// (`FddRateData.August2023`) — that compiled table is MyssApi's fallback when
// Strapi is unreachable, so the estimate is the same whether the browser's rate
// table came from Strapi or the fallback. Letter meanings (MYSS-25):
//   A = couple neither PWD, B = single not PWD, C = couple either PWD,
//   D = single PWD, E = couple both PWD (dependants feed family size only).
// See document/MYSS-25-vs-169-EE-Values-Diff.md.

/** One family-size row of monthly income limits, by client type A-E. */
export interface EligibilityRateIncomeRow {
  readonly familySize: number;
  readonly a: number;
  readonly b: number;
  readonly c: number;
  readonly d: number;
  readonly e: number;
}

/** The asset ceilings by category A-D (a separate axis from the income types). */
export interface EligibilityAssetLimits {
  readonly a: number;
  readonly b: number;
  readonly c: number;
  readonly d: number;
}

/** A complete, dated rate table — one published `eligibility-rate` entry. */
export interface EligibilityRateSeed {
  readonly effectiveDate: string;
  readonly incomeRows: readonly EligibilityRateIncomeRow[];
  readonly assetLimits: EligibilityAssetLimits;
}

/** The date the August-2023 FDD / MYSS-25 values take effect. */
export const ELIGIBILITY_RATE_EFFECTIVE_DATE = "2023-08-01";

/** The MYSS-25 rate table (must match FddRateData.August2023). */
export const eligibilityRateAugust2023: EligibilityRateSeed = {
  effectiveDate: ELIGIBILITY_RATE_EFFECTIVE_DATE,
  incomeRows: [
    { familySize: 1, a: 0, b: 1060, c: 0, d: 1535.5, e: 0 },
    { familySize: 2, a: 1650, b: 1405, c: 2290.5, d: 1880.5, e: 2766 },
    { familySize: 3, a: 1845, b: 1500, c: 2485.5, d: 1975.5, e: 2961 },
    { familySize: 4, a: 1895, b: 1550, c: 2535.5, d: 2025.5, e: 3011 },
    { familySize: 5, a: 1945, b: 1600, c: 2585.5, d: 2075.5, e: 3061 },
    { familySize: 6, a: 1995, b: 1650, c: 2635.5, d: 2125.5, e: 3111 },
    { familySize: 7, a: 2045, b: 1700, c: 2685.5, d: 2175.5, e: 3161 },
  ],
  assetLimits: { a: 5000, b: 10000, c: 100000, d: 200000 },
};

/**
 * Every rate table the bootstrap hook seeds. Adding a new dated table here is
 * the only change needed to seed another (create-only-if-missing, keyed by
 * `effectiveDate`). Kept as an array to mirror `seededForms`.
 */
export const seededRates: readonly EligibilityRateSeed[] = [eligibilityRateAugust2023];
