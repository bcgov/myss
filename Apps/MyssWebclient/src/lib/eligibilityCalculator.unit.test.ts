import { describe, expect, it } from "vitest";

import type {
  EligibilityRates,
  EligibilityRequest,
  HouseholdType,
} from "@/api/eligibility";
import {
  assetLimitCategory,
  calculateEstimate,
  classifyClientType,
  familySize,
  INELIGIBLE_ASSETS,
  INELIGIBLE_INCOME,
} from "@/lib/eligibilityCalculator";

// The MYSS-25 August-2023 rate table, matching the parked C# FddRateData.August2023
// and the Strapi seed (document/MYSS-25-vs-169-EE-Values-Diff.md). These vectors
// are ported verbatim from Apps/MyssApi.Tests/EligibilityCalculatorTests.cs and
// MUST match it exactly.
const RATES: EligibilityRates = {
  effectiveDate: "2023-08-01",
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

function request(
  overrides: Partial<EligibilityRequest> = {},
): EligibilityRequest {
  return {
    relationshipStatus: "Single",
    dependants: 0,
    applicantPwd: false,
    spousePwd: false,
    monthlyIncome: 0,
    spouseMonthlyIncome: 0,
    primaryVehicleValue: 0,
    otherVehicleValue: 0,
    otherAssetValue: 0,
    ...overrides,
  };
}

describe("classifyClientType (BR-D9-04, MYSS-25)", () => {
  const cases: Array<[HouseholdType, number, boolean, boolean, string]> = [
    ["Couple", 0, true, true, "E"], // both PWD
    ["Couple", 0, true, false, "C"], // applicant PWD only
    ["Couple", 0, false, true, "C"], // spouse PWD only
    ["Couple", 0, false, false, "A"], // neither
    ["Couple", 2, false, false, "A"], // couple, neither PWD => A (deps irrelevant)
    ["Single", 1, false, false, "B"], // single, not PWD => B (deps irrelevant)
    ["Single", 1, true, false, "D"], // single, PWD => D (deps irrelevant)
    ["Single", 0, true, false, "D"], // single, PWD => D
    ["Single", 0, false, false, "B"], // single, not PWD => B
  ];

  it.each(cases)(
    "%s deps=%i applicantPwd=%s spousePwd=%s -> %s",
    (relationshipStatus, dependants, applicantPwd, spousePwd, expected) => {
      const result = classifyClientType(
        request({ relationshipStatus, dependants, applicantPwd, spousePwd }),
      );
      expect(result).toBe(expected);
    },
  );
});

describe("assetLimitCategory + asset gate (BR-D9-06 / BR-D9-07)", () => {
  const cases: Array<[HouseholdType, number, boolean, boolean, number]> = [
    ["Single", 0, false, false, 5000], // category A
    ["Single", 1, false, false, 10000], // category B (dependant)
    ["Couple", 0, false, false, 10000], // category B (couple)
    ["Couple", 0, true, false, 100000], // category C (one PWD)
    ["Single", 0, true, false, 100000], // category C (single PWD)
    ["Couple", 0, true, true, 200000], // category D (both PWD)
  ];

  it.each(cases)(
    "%s deps=%i applicantPwd=%s spousePwd=%s -> limit %i (equal passes, +0.01 fails)",
    (relationshipStatus, dependants, applicantPwd, spousePwd, limit) => {
      const atLimit = calculateEstimate(
        request({
          relationshipStatus,
          dependants,
          applicantPwd,
          spousePwd,
          otherAssetValue: limit,
        }),
        RATES,
      );
      const overLimit = calculateEstimate(
        request({
          relationshipStatus,
          dependants,
          applicantPwd,
          spousePwd,
          otherAssetValue: limit + 0.01,
        }),
        RATES,
      );

      expect(atLimit.eligible).toBe(true);
      expect(overLimit.eligible).toBe(false);
      expect(overLimit.ineligibilityReasonKeyword).toBe(INELIGIBLE_ASSETS);
    },
  );

  it("sums all three asset fields for the gate", () => {
    // Single, category A ($5,000): 2000 + 2000 + 1500 = 5500 > 5000.
    const result = calculateEstimate(
      request({
        primaryVehicleValue: 2000,
        otherVehicleValue: 2000,
        otherAssetValue: 1500,
      }),
      RATES,
    );
    expect(result.eligible).toBe(false);
    expect(result.ineligibilityReasonKeyword).toBe(INELIGIBLE_ASSETS);
    expect(result.estimatedAmount).toBe(0);
    expect(result.totalAssets).toBe(5500);
  });

  it("checks the asset gate before income (asset reason wins)", () => {
    const result = calculateEstimate(
      request({ monthlyIncome: 9000, otherAssetValue: 9000 }),
      RATES,
    );
    expect(result.ineligibilityReasonKeyword).toBe(INELIGIBLE_ASSETS);
  });
});

describe("benefit = income limit - total income (BR-D9-08)", () => {
  it("is ineligible when income equals the limit", () => {
    // Single, type B, size 1 => limit 1060.00; benefit 0 => ineligible.
    const result = calculateEstimate(request({ monthlyIncome: 1060 }), RATES);
    expect(result.eligible).toBe(false);
    expect(result.ineligibilityReasonKeyword).toBe(INELIGIBLE_INCOME);
    expect(result.estimatedAmount).toBe(0);
  });

  it("includes spouse income in the total", () => {
    // Couple, no PWD, no kids => type A, size 2 => 1650; 1650 - (600+400) = 650.
    const result = calculateEstimate(
      request({
        relationshipStatus: "Couple",
        monthlyIncome: 600,
        spouseMonthlyIncome: 400,
      }),
      RATES,
    );
    expect(result.eligible).toBe(true);
    expect(result.estimatedAmount).toBe(650);
    expect(result.monthlyIncome).toBe(1000);
  });

  it("keeps two-decimal precision (integer cents, not JS floats)", () => {
    // Single PWD => type D, size 1 => 1535.50; minus 100.55 => 1434.95.
    const result = calculateEstimate(
      request({ applicantPwd: true, monthlyIncome: 100.55 }),
      RATES,
    );
    expect(result.estimatedAmount).toBe(1434.95);
  });
});

describe("family size cap (BR-D9-03 / OQ-D9-02)", () => {
  it("caps family size at 7 and flags the clamp", () => {
    // Single + 10 dependants => family size 11, clamped to the size-7 row.
    // Single with dependants => type B; size 7 type B = 1700.00.
    const large = calculateEstimate(request({ dependants: 10 }), RATES);
    const seven = calculateEstimate(request({ dependants: 6 }), RATES);

    expect(large.estimatedAmount).toBe(1700);
    expect(large.estimatedAmount).toBe(seven.estimatedAmount);
    expect(familySize(request({ dependants: 10 }))).toBe(11);
    expect(large.familySize).toBe(11);
    expect(large.familySizeClamped).toBe(true);
    expect(seven.familySizeClamped).toBe(false);
  });

  it("counts two adults for a couple", () => {
    // Couple + 1 child => family size 3, type A => 1845.00.
    const result = calculateEstimate(
      request({ relationshipStatus: "Couple", dependants: 1 }),
      RATES,
    );
    expect(result.estimatedAmount).toBe(1845);
    expect(result.familySize).toBe(3);
  });
});

describe("MYSS-25 sanity vectors (must match parked dotnet test exactly)", () => {
  it("single / no kids / no PWD => type B, $1060.00", () => {
    const result = calculateEstimate(request(), RATES);
    expect(result.eligible).toBe(true);
    expect(result.estimatedAmount).toBe(1060);
    expect(result.clientType).toBe("B");
    expect(result.ineligibilityReasonKeyword).toBeNull();
  });

  it("single / PWD => type D, $1535.50", () => {
    const result = calculateEstimate(request({ applicantPwd: true }), RATES);
    expect(result.eligible).toBe(true);
    expect(result.estimatedAmount).toBe(1535.5);
    expect(result.clientType).toBe("D");
  });

  it("single / assets $6,000 => ineligible ASSETS, type B", () => {
    const result = calculateEstimate(request({ otherAssetValue: 6000 }), RATES);
    expect(result.eligible).toBe(false);
    expect(result.ineligibilityReasonKeyword).toBe(INELIGIBLE_ASSETS);
    expect(result.estimatedAmount).toBe(0);
    expect(result.clientType).toBe("B");
  });

  it("single / income $2,000 => ineligible INCOME", () => {
    const result = calculateEstimate(request({ monthlyIncome: 2000 }), RATES);
    expect(result.eligible).toBe(false);
    expect(result.ineligibilityReasonKeyword).toBe(INELIGIBLE_INCOME);
    expect(result.estimatedAmount).toBe(0);
  });

  it("couple / both PWD / one child => type E, family size 3, $2961.00", () => {
    const result = calculateEstimate(
      request({
        relationshipStatus: "Couple",
        dependants: 1,
        applicantPwd: true,
        spousePwd: true,
      }),
      RATES,
    );
    expect(result.eligible).toBe(true);
    expect(result.clientType).toBe("E");
    expect(result.familySize).toBe(3);
    expect(result.estimatedAmount).toBe(2961);
  });
});

describe("result echoes the household inputs", () => {
  it("echoes size, household type, total income and total assets (single)", () => {
    const result = calculateEstimate(request(), RATES);
    expect(result.familySize).toBe(1);
    expect(result.householdType).toBe("Single");
    expect(result.monthlyIncome).toBe(0);
    expect(result.totalAssets).toBe(0);
  });

  it("echoes the couple inputs (total income summed)", () => {
    const result = calculateEstimate(
      request({
        relationshipStatus: "Couple",
        monthlyIncome: 600,
        spouseMonthlyIncome: 400,
        otherAssetValue: 1500,
      }),
      RATES,
    );
    expect(result.familySize).toBe(2);
    expect(result.householdType).toBe("Couple");
    expect(result.monthlyIncome).toBe(1000);
    expect(result.totalAssets).toBe(1500);
  });
});

describe("assetLimitCategory (standalone, separate from income type)", () => {
  it("maps both-PWD -> D, either-PWD -> C, couple/deps -> B, else A", () => {
    expect(assetLimitCategory(request())).toBe("A");
    expect(assetLimitCategory(request({ dependants: 1 }))).toBe("B");
    expect(assetLimitCategory(request({ relationshipStatus: "Couple" }))).toBe(
      "B",
    );
    expect(assetLimitCategory(request({ applicantPwd: true }))).toBe("C");
    expect(
      assetLimitCategory(
        request({
          relationshipStatus: "Couple",
          applicantPwd: true,
          spousePwd: true,
        }),
      ),
    ).toBe("D");
  });
});
