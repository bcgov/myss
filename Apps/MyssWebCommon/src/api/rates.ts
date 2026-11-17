// The eligibility rate table: the shape both APIs serve (camelCase,
// single-letter lowercase columns; see EligibilityRatesModel). The citizen
// estimator computes against it; the admin rates editor edits it.

/**
 * One family-size row of monthly income limits by client type A-I. Couple
 * columns hold 0 at family size 1; the admin webclient's lib/rateColumns.ts
 * names each letter.
 */
export interface EligibilityRateRow {
  familySize: number;
  a: number;
  b: number;
  c: number;
  d: number;
  e: number;
  f: number;
  g: number;
  h: number;
  i: number;
}

/** An income-limit column letter. */
export type IncomeColumn = Exclude<keyof EligibilityRateRow, "familySize">;

/** The asset ceilings by category A-D. */
export interface EligibilityAssetLimits {
  a: number;
  b: number;
  c: number;
  d: number;
}

/** An asset-limit column letter. */
export type AssetColumn = keyof EligibilityAssetLimits;

/** The rate table, as of its effective date. */
export interface EligibilityRates {
  effectiveDate: string;
  incomeRows: EligibilityRateRow[];
  assetLimits: EligibilityAssetLimits;
}
