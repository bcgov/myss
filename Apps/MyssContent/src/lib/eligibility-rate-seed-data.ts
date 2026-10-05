// The rate table the estimator computes against. These values must stay
// identical to the compiled fallback in Apps/MyssApi/Data/FddRateData.cs, so the
// estimate is the same whether the rates came from Strapi or the fallback.
// Client types (dependants affect family size only; PWD takes priority over 65+):
//   A = couple, both under 65, neither PWD   B = single, under 65, not PWD
//   C = couple, both 65+, neither PWD        D = couple, one 65+, neither PWD
//   E = single, 65+, not PWD                 F = couple, one PWD, other under 65 and not PWD
//   G = single, PWD                          H = couple, both PWD
//   I = couple, one PWD and the other 65+
// Couple columns (A, C, D, F, H, I) have no limit at family size 1 and hold 0.

/** One family-size row of monthly income limits, by client type A-I. */
export interface EligibilityRateIncomeRow {
  readonly familySize: number;
  readonly a: number;
  readonly b: number;
  readonly c: number;
  readonly d: number;
  readonly e: number;
  readonly f: number;
  readonly g: number;
  readonly h: number;
  readonly i: number;
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

/** The date the nine-type rate table takes effect. */
export const ELIGIBILITY_RATE_EFFECTIVE_DATE = "2026-10-02";

/** The nine-type rate table (must match FddRateData in MyssApi). */
export const eligibilityRateOctober2026: EligibilityRateSeed = {
  effectiveDate: ELIGIBILITY_RATE_EFFECTIVE_DATE,
  incomeRows: [
    { familySize: 1, a: 0, b: 1060, c: 0, d: 0, e: 1360, f: 0, g: 1535.5, h: 0, i: 0 },
    { familySize: 2, a: 1650, b: 1405, c: 2200, d: 1950, e: 1705, f: 2290.5, g: 1880.5, h: 2766, i: 2590.5 },
    { familySize: 3, a: 1845, b: 1500, c: 2395, d: 2145, e: 1800, f: 2485.5, g: 1975.5, h: 2961, i: 2785.5 },
    { familySize: 4, a: 1895, b: 1550, c: 2445, d: 2195, e: 1850, f: 2535.5, g: 2025.5, h: 3011, i: 2835.5 },
    { familySize: 5, a: 1945, b: 1600, c: 2495, d: 2245, e: 1900, f: 2585.5, g: 2075.5, h: 3061, i: 2885.5 },
    { familySize: 6, a: 1995, b: 1650, c: 2545, d: 2295, e: 1950, f: 2635.5, g: 2125.5, h: 3111, i: 2935.5 },
    { familySize: 7, a: 2045, b: 1700, c: 2595, d: 2345, e: 2000, f: 2685.5, g: 2175.5, h: 3161, i: 2985.5 },
  ],
  assetLimits: { a: 5000, b: 10000, c: 100000, d: 200000 },
};

/**
 * Every rate table the bootstrap seeds. Seeding is create-only, keyed by
 * `effectiveDate`: a missing table is created and an existing one is never
 * changed, so new rates go in as a new dated table. Kept as an array to mirror
 * `seededForms`.
 */
export const seededRates: readonly EligibilityRateSeed[] = [eligibilityRateOctober2026];
