import { describe, expect, it } from "vitest";

import {
  mapAnswersToEstimate,
  missingRequiredCoupleAnswers,
  screenPreCheck,
  type EligibilityRequest,
} from "@/api/eligibility";

// Pure mapper + pre-check gate (Step 4). No network — the fetches are exercised
// against the running stack in the browser test (Step 7) and Bruno (Step 9).

describe("mapAnswersToEstimate", () => {
  it("collapses the six relationship values to Single/Couple", () => {
    const couple = ["married", "marriagelike"];
    const single = ["single", "divorced", "separated", "widowed"];

    for (const value of couple) {
      expect(
        mapAnswersToEstimate({ relationshipStatus: value }).relationshipStatus,
      ).toBe("Couple");
    }
    for (const value of single) {
      expect(
        mapAnswersToEstimate({ relationshipStatus: value }).relationshipStatus,
      ).toBe("Single");
    }
    // Missing / unknown defaults to Single.
    expect(mapAnswersToEstimate({}).relationshipStatus).toBe("Single");
  });

  it("forces every spouse field to 0/false for a Single household", () => {
    const request = mapAnswersToEstimate({
      relationshipStatus: "single",
      partnerPwd: "true",
      partnerMonthlyIncome: 5000,
      partnerVehicleValueMinusTransportation: 4000,
      partnerVehicleValue: 3000,
      partnerAssetValue: 2000,
      // applicant values that should still flow through:
      pwd: "true",
      monthlyIncome: 100,
      vehicleValueMinusTransportation: 10,
      vehicleValue: 20,
      assetValue: 30,
    });

    expect(request.spousePwd).toBe(false);
    expect(request.spouseMonthlyIncome).toBe(0);
    // Spouse assets are NOT summed in for a Single household.
    expect(request.primaryVehicleValue).toBe(10);
    expect(request.otherVehicleValue).toBe(20);
    expect(request.otherAssetValue).toBe(30);
    // Applicant PWD still honoured.
    expect(request.applicantPwd).toBe(true);
  });

  it("coerces yes/no radios ('true'/'false') to booleans", () => {
    expect(mapAnswersToEstimate({ pwd: "true" }).applicantPwd).toBe(true);
    expect(mapAnswersToEstimate({ pwd: "false" }).applicantPwd).toBe(false);
    expect(mapAnswersToEstimate({ pwd: true }).applicantPwd).toBe(true);
    // Anything else is No/false.
    expect(mapAnswersToEstimate({ pwd: undefined }).applicantPwd).toBe(false);
    expect(
      mapAnswersToEstimate({ relationshipStatus: "married", partnerPwd: "true" })
        .spousePwd,
    ).toBe(true);
  });

  it("clamps negative and blank money answers to 0", () => {
    const request = mapAnswersToEstimate({
      monthlyIncome: -50,
      vehicleValueMinusTransportation: "",
      vehicleValue: "not a number",
      assetValue: null,
    });
    expect(request.monthlyIncome).toBe(0);
    expect(request.primaryVehicleValue).toBe(0);
    expect(request.otherVehicleValue).toBe(0);
    expect(request.otherAssetValue).toBe(0);
  });

  it("accepts numeric strings and truncates dependants to an integer", () => {
    expect(mapAnswersToEstimate({ dependentChildren: "3" }).dependants).toBe(3);
    expect(mapAnswersToEstimate({ dependentChildren: 2.9 }).dependants).toBe(2);
    expect(mapAnswersToEstimate({ dependentChildren: -1 }).dependants).toBe(0);
    expect(mapAnswersToEstimate({ dependentChildren: "" }).dependants).toBe(0);
    // A money string is parsed too.
    expect(mapAnswersToEstimate({ monthlyIncome: "1234.56" }).monthlyIncome).toBe(
      1234.56,
    );
  });

  it("sums applicant + spouse into each of the three combined asset fields (couple)", () => {
    const request: EligibilityRequest = mapAnswersToEstimate({
      relationshipStatus: "married",
      vehicleValueMinusTransportation: 1000,
      partnerVehicleValueMinusTransportation: 500,
      vehicleValue: 200,
      partnerVehicleValue: 300,
      assetValue: 40,
      partnerAssetValue: 60,
      monthlyIncome: 600,
      partnerMonthlyIncome: 400,
    });

    expect(request.primaryVehicleValue).toBe(1500);
    expect(request.otherVehicleValue).toBe(500);
    expect(request.otherAssetValue).toBe(100);
    expect(request.monthlyIncome).toBe(600);
    expect(request.spouseMonthlyIncome).toBe(400);
  });
});

describe("screenPreCheck", () => {
  it("passes only when both residency and status are Yes", () => {
    const pre = screenPreCheck({
      residesInBc: "true",
      hasEligibleStatus: "true",
    });
    expect(pre.passed).toBe(true);
    expect(pre.residesInBc).toBe(true);
    expect(pre.hasEligibleStatus).toBe(true);
  });

  it("fails when residency is No", () => {
    const pre = screenPreCheck({
      residesInBc: "false",
      hasEligibleStatus: "true",
    });
    expect(pre.passed).toBe(false);
  });

  it("fails when status is No", () => {
    const pre = screenPreCheck({
      residesInBc: "true",
      hasEligibleStatus: "false",
    });
    expect(pre.passed).toBe(false);
  });

  it("short-circuits before an EligibilityRequest is built when a pre-check is No", () => {
    const answers = {
      residesInBc: "false",
      hasEligibleStatus: "true",
      relationshipStatus: "single",
      monthlyIncome: 100,
    };
    const pre = screenPreCheck(answers);
    expect(pre.passed).toBe(false);

    // The page builds no request on a failed screen — modelled here.
    const request = pre.passed ? mapAnswersToEstimate(answers) : null;
    expect(request).toBeNull();
  });
});

describe("missingRequiredCoupleAnswers", () => {
  it("requires partnerPwd for a couple who left it unanswered", () => {
    for (const status of ["married", "marriagelike"]) {
      expect(missingRequiredCoupleAnswers({ relationshipStatus: status })).toEqual([
        "partnerPwd",
      ]);
      // Empty string / null are "unanswered" too, not "No".
      expect(
        missingRequiredCoupleAnswers({ relationshipStatus: status, partnerPwd: "" }),
      ).toEqual(["partnerPwd"]);
      expect(
        missingRequiredCoupleAnswers({ relationshipStatus: status, partnerPwd: null }),
      ).toEqual(["partnerPwd"]);
    }
  });

  it("is satisfied once a couple answers partnerPwd either way", () => {
    for (const value of ["true", "false", true, false]) {
      expect(
        missingRequiredCoupleAnswers({
          relationshipStatus: "married",
          partnerPwd: value,
        }),
      ).toEqual([]);
    }
  });

  it("never requires spouse answers from a single applicant", () => {
    for (const status of ["single", "divorced", "separated", "widowed", undefined]) {
      expect(missingRequiredCoupleAnswers({ relationshipStatus: status })).toEqual([]);
    }
  });
});
