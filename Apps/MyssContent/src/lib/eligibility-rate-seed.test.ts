import { describe, expect, it } from "vitest";

import { evaluateRateCreate } from "./eligibility-rate-rules";
import {
  ELIGIBILITY_RATE_EFFECTIVE_DATE,
  eligibilityRateOctober2026,
  seededRates,
} from "./eligibility-rate-seed-data";

/**
 * The rate table is served from Strapi and read by MyssApi; its numbers MUST stay
 * identical to MyssApi's compiled fallback (`FddRateData`), so they are pinned
 * here. Client types A-I are listed in eligibility-rate-seed-data.ts.
 */
describe("eligibility rate seed", () => {
  const table = eligibilityRateOctober2026;

  it("seeds exactly one dated rate table", () => {
    expect(seededRates).toEqual([table]);
    expect(table.effectiveDate).toBe(ELIGIBILITY_RATE_EFFECTIVE_DATE);
    expect(table.effectiveDate).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  it("holds the nine-type income limits", () => {
    expect(table.incomeRows).toEqual([
      { familySize: 1, a: 0, b: 1060, c: 0, d: 0, e: 1360, f: 0, g: 1535.5, h: 0, i: 0 },
      { familySize: 2, a: 1650, b: 1405, c: 2200, d: 1950, e: 1705, f: 2290.5, g: 1880.5, h: 2766, i: 2590.5 },
      { familySize: 3, a: 1845, b: 1500, c: 2395, d: 2145, e: 1800, f: 2485.5, g: 1975.5, h: 2961, i: 2785.5 },
      { familySize: 4, a: 1895, b: 1550, c: 2445, d: 2195, e: 1850, f: 2535.5, g: 2025.5, h: 3011, i: 2835.5 },
      { familySize: 5, a: 1945, b: 1600, c: 2495, d: 2245, e: 1900, f: 2585.5, g: 2075.5, h: 3061, i: 2885.5 },
      { familySize: 6, a: 1995, b: 1650, c: 2545, d: 2295, e: 1950, f: 2635.5, g: 2125.5, h: 3111, i: 2935.5 },
      { familySize: 7, a: 2045, b: 1700, c: 2595, d: 2345, e: 2000, f: 2685.5, g: 2175.5, h: 3161, i: 2985.5 },
    ]);
  });

  it("keeps the asset ceilings A $5k / B $10k / C $100k / D $200k", () => {
    expect(table.assetLimits).toEqual({ a: 5000, b: 10000, c: 100000, d: 200000 });
  });

  it("passes the eligibility-rate value rules, so the lifecycle accepts the seeded values", () => {
    for (const seeded of seededRates) {
      expect(evaluateRateCreate({ ...seeded, documentId: null }, [])).toEqual([]);
    }
  });
});
