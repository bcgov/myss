import { describe, expect, it } from "vitest";

import type {
  ClientType,
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

// The nine-type rate table, matching the Strapi seed (eligibility-rate-seed-data.ts)
// and MyssApi's compiled fallback (FddRateData).
const RATES: EligibilityRates = {
  effectiveDate: "2026-10-02",
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

function request(
  overrides: Partial<EligibilityRequest> = {},
): EligibilityRequest {
  return {
    relationshipStatus: "Single",
    dependants: 0,
    applicantPwd: false,
    spousePwd: false,
    applicantSenior: false,
    spouseSenior: false,
    monthlyIncome: 0,
    spouseMonthlyIncome: 0,
    primaryVehicleValue: 0,
    otherVehicleValue: 0,
    otherAssetValue: 0,
    ...overrides,
  };
}

/** An adult's answers to the PWD and 65+ questions. */
type Adult = "neither" | "65+" | "PWD" | "PWD and 65+";

const ANSWERS: Record<Adult, { pwd: boolean; senior: boolean }> = {
  neither: { pwd: false, senior: false },
  "65+": { pwd: false, senior: true },
  PWD: { pwd: true, senior: false },
  "PWD and 65+": { pwd: true, senior: true },
};

function applicant(adult: Adult): Partial<EligibilityRequest> {
  return { applicantPwd: ANSWERS[adult].pwd, applicantSenior: ANSWERS[adult].senior };
}

function spouse(adult: Adult): Partial<EligibilityRequest> {
  return { spousePwd: ANSWERS[adult].pwd, spouseSenior: ANSWERS[adult].senior };
}

function couple(kp: Adult, partner: Adult, dependants = 0): EligibilityRequest {
  return request({
    relationshipStatus: "Couple",
    dependants,
    ...applicant(kp),
    ...spouse(partner),
  });
}

describe("classifyClientType (BR-D9-04)", () => {
  it.each<[Adult, ClientType]>([
    ["neither", "B"],
    ["65+", "E"],
    ["PWD", "G"],
    ["PWD and 65+", "G"], // PWD takes priority over 65+
  ])("single, %s -> %s", (adult, expected) => {
    expect(classifyClientType(request(applicant(adult)))).toBe(expected);
  });

  it.each<[Adult, Adult, ClientType]>([
    ["neither", "neither", "A"],
    ["neither", "65+", "D"],
    ["65+", "65+", "C"],
    ["neither", "PWD", "F"],
    ["65+", "PWD", "I"],
    ["PWD", "PWD", "H"],
    // PWD takes priority over 65+ for each adult.
    ["PWD and 65+", "neither", "F"],
    ["PWD and 65+", "65+", "I"],
    ["PWD and 65+", "PWD", "H"],
    ["PWD and 65+", "PWD and 65+", "H"],
  ])("couple, %s and %s -> %s (either way round)", (kp, partner, expected) => {
    expect(classifyClientType(couple(kp, partner))).toBe(expected);
    expect(classifyClientType(couple(partner, kp))).toBe(expected);
  });

  it("ignores dependants", () => {
    expect(classifyClientType(couple("neither", "neither", 2))).toBe("A");
    expect(classifyClientType(request({ dependants: 1 }))).toBe("B");
    expect(classifyClientType(request({ dependants: 1, ...applicant("65+") }))).toBe("E");
    expect(classifyClientType(request({ dependants: 1, ...applicant("PWD") }))).toBe("G");
  });

  it("ignores spouse answers for a single", () => {
    expect(classifyClientType(request(spouse("PWD and 65+")))).toBe("B");
  });
});

describe("estimate for each client type at zero income", () => {
  it.each<[string, EligibilityRequest, ClientType, number]>([
    ["couple, neither", couple("neither", "neither"), "A", 1650],
    ["single, one child", request({ dependants: 1 }), "B", 1405],
    ["couple, both 65+", couple("65+", "65+"), "C", 2200],
    ["couple, one 65+", couple("65+", "neither"), "D", 1950],
    ["single 65+, one child", request({ dependants: 1, ...applicant("65+") }), "E", 1705],
    ["couple, one PWD", couple("PWD", "neither"), "F", 2290.5],
    ["single PWD, one child", request({ dependants: 1, ...applicant("PWD") }), "G", 1880.5],
    ["couple, both PWD", couple("PWD", "PWD"), "H", 2766],
    ["couple, one PWD and one 65+", couple("PWD", "65+"), "I", 2590.5],
    ["single 65+, no children", request(applicant("65+")), "E", 1360],
  ])("%s -> type %s, $%s", (_, household, clientType, amount) => {
    const result = calculateEstimate(household, RATES);
    expect(result.eligible).toBe(true);
    expect(result.clientType).toBe(clientType);
    expect(result.estimatedAmount).toBe(amount);
  });
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
    // Single PWD => type G, size 1 => 1535.50; minus 100.55 => 1434.95.
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

describe("sanity vectors", () => {
  it("single / no kids / no PWD => type B, $1060.00", () => {
    const result = calculateEstimate(request(), RATES);
    expect(result.eligible).toBe(true);
    expect(result.estimatedAmount).toBe(1060);
    expect(result.clientType).toBe("B");
    expect(result.ineligibilityReasonKeyword).toBeNull();
  });

  it("single / PWD => type G, $1535.50", () => {
    const result = calculateEstimate(request({ applicantPwd: true }), RATES);
    expect(result.eligible).toBe(true);
    expect(result.estimatedAmount).toBe(1535.5);
    expect(result.clientType).toBe("G");
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

  it("couple / both PWD / one child => type H, family size 3, $2961.00", () => {
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
    expect(result.clientType).toBe("H");
    expect(result.familySize).toBe(3);
    expect(result.estimatedAmount).toBe(2961);
  });

  it("couple / one PWD => type F, $2290.50", () => {
    const result = calculateEstimate(couple("neither", "PWD"), RATES);
    expect(result.clientType).toBe("F");
    expect(result.estimatedAmount).toBe(2290.5);
  });
});

describe("missing rate data", () => {
  it("throws for a client type whose column is missing, rather than estimating", () => {
    const withoutI = {
      ...RATES,
      incomeRows: RATES.incomeRows.map((row) => ({ ...row, i: undefined })),
    } as unknown as EligibilityRates;

    expect(() => calculateEstimate(couple("PWD", "65+"), withoutI)).toThrow(
      "No income limit for client type I at family size 2",
    );
  });

  it("throws for a family size with no row", () => {
    const withoutSize2 = {
      ...RATES,
      incomeRows: RATES.incomeRows.filter((row) => row.familySize !== 2),
    };

    expect(() => calculateEstimate(couple("neither", "neither"), withoutSize2)).toThrow(
      "No income-limit row for family size 2",
    );
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

  it("does not depend on age", () => {
    expect(assetLimitCategory(request(applicant("65+")))).toBe("A");
    expect(assetLimitCategory(couple("65+", "65+"))).toBe("B");
  });
});
