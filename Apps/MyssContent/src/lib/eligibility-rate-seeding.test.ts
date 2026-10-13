import { describe, expect, it } from "vitest";

import { eligibilityRateOctober2026 as seed } from "./eligibility-rate-seed-data";
import { seedRateAction } from "./eligibility-rate-seeding";

const stored = { incomeRows: seed.incomeRows, assetLimits: seed.assetLimits };

const edited = {
  incomeRows: seed.incomeRows.map((row) => (row.familySize === 2 ? { ...row, e: 1715 } : row)),
  assetLimits: seed.assetLimits,
};

describe("seedRateAction", () => {
  it("creates the table when no document has the seeded date", () => {
    expect(seedRateAction(seed, null, null)).toBe("create");
  });

  it("keeps a published table that equals the seed", () => {
    expect(seedRateAction(seed, stored, stored)).toBe("keep");
  });

  it("ignores key order, which Strapi's JSON columns do not preserve", () => {
    const reordered = {
      incomeRows: seed.incomeRows.map((row) => Object.fromEntries(Object.entries(row).reverse())),
      assetLimits: { d: 200000, c: 100000, b: 10000, a: 5000 },
    };
    expect(seedRateAction(seed, reordered, reordered)).toBe("keep");
  });

  it("keeps an edited published table rather than putting the seed back", () => {
    expect(seedRateAction(seed, edited, edited)).toBe("keep-changed");
  });

  it("compares the published version, not an unpublished draft edit", () => {
    expect(seedRateAction(seed, edited, stored)).toBe("keep");
  });

  it("reports a table that exists only as a draft", () => {
    expect(seedRateAction(seed, stored, null)).toBe("keep-draft-only");
  });
});
