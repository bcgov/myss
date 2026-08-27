import { describe, expect, it } from "vitest";

import {
  ELIGIBILITY_RATE_EFFECTIVE_DATE,
  eligibilityRateAugust2023,
  seededRates,
} from "./eligibility-rate-seed-data";

/**
 * The rate table is served from Strapi and read by MyssApi; the citizen never
 * sees this file. But its numbers are load-bearing: they MUST stay identical to
 * MyssApi's compiled fallback (`FddRateData.August2023`) and reproduce the
 * MYSS-25 vectors, so the key values are pinned here. Letter meanings (MYSS-25):
 * A = couple neither PWD, B = single not PWD, C = couple either PWD,
 * D = single PWD, E = couple both PWD.
 */
describe("eligibility rate seed", () => {
  const table = eligibilityRateAugust2023;
  const byFamilySize = (n: number) =>
    table.incomeRows.find((row) => row.familySize === n)!;

  it("seeds exactly one dated rate table, effective 2023-08-01", () => {
    expect(seededRates).toEqual([table]);
    expect(table.effectiveDate).toBe(ELIGIBILITY_RATE_EFFECTIVE_DATE);
    expect(table.effectiveDate).not.toBe("");
  });

  it("has seven contiguous family-size rows, 1 through 7", () => {
    expect(table.incomeRows.map((row) => row.familySize)).toEqual([1, 2, 3, 4, 5, 6, 7]);
  });

  it("reassigns the family-size-1 row to the single types B and D (MYSS-25)", () => {
    expect(byFamilySize(1)).toEqual({ familySize: 1, a: 0, b: 1060, c: 0, d: 1535.5, e: 0 });
  });

  it("carries the MYSS-25 column-C (+165) and column-E (+113.50) values", () => {
    // fs3: C 2320.50 -> 2485.50, E 2847.50 -> 2961.00
    expect(byFamilySize(3).c).toBe(2485.5);
    expect(byFamilySize(3).e).toBe(2961);
    // fs7 (cap): C 2520.50 -> 2685.50, E 3047.50 -> 3161.00
    expect(byFamilySize(7).c).toBe(2685.5);
    expect(byFamilySize(7).e).toBe(3161);
    // A/B/D at sizes 2-7 are unchanged (spot-check fs2)
    expect(byFamilySize(2).a).toBe(1650);
    expect(byFamilySize(2).b).toBe(1405);
    expect(byFamilySize(2).d).toBe(1880.5);
  });

  it("keeps the asset ceilings A $5k / B $10k / C $100k / D $200k", () => {
    expect(table.assetLimits).toEqual({ a: 5000, b: 10000, c: 100000, d: 200000 });
  });

  it("never lets an income or asset limit go negative", () => {
    for (const row of table.incomeRows) {
      for (const v of [row.a, row.b, row.c, row.d, row.e]) {
        expect(v).toBeGreaterThanOrEqual(0);
      }
    }
    for (const v of Object.values(table.assetLimits)) {
      expect(v).toBeGreaterThanOrEqual(0);
    }
  });
});
