import { describe, expect, it } from "vitest";

import {
  ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
  ELIGIBILITY_ESTIMATOR_FORM_SPEC_TITLE,
  eligibilityEstimatorSpecV1,
  eligibilityEstimatorSpecV2,
  seededForms,
  type Json,
} from "./form-spec-seed-data";

/**
 * The eligibility estimator spec is served from Strapi and rendered by MyssApi;
 * the citizen never sees this file. But two properties of the spec are load-
 * bearing and fail SILENTLY if they drift, so they are pinned here:
 *
 *   1. The spouse fields use an ADVANCED conditional (`conditional.json`) so they
 *      reveal for `married` OR `marriagelike`. A simple `conditional.when` would
 *      only ever match one value, silently hiding the spouse fields for the
 *      other partnered status.
 *   2. The spouse fields carry NO `validate.required`. MyssApi's FormSpecValidator
 *      only exempts SIMPLE-conditional fields from the server-side required check,
 *      so a required advanced-conditional field would reject a single applicant
 *      who (correctly) never saw it.
 *
 * The component keys are also a contract with the frontend mapper
 * (`mapAnswersToEstimate` in Apps/MyssWebclient/src/api/eligibility.ts).
 */

interface Component {
  readonly key?: unknown;
  readonly type?: unknown;
  readonly input?: unknown;
  readonly values?: unknown;
  readonly validate?: { readonly required?: unknown; readonly min?: unknown };
  readonly conditional?: { readonly when?: unknown; readonly json?: unknown };
}

function isRecord(value: Json): value is { [key: string]: Json } {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function componentsOf(spec: Json): Component[] {
  if (!isRecord(spec)) throw new Error("spec is not an object");
  const components = spec.components;
  if (!Array.isArray(components)) throw new Error("spec.components is not an array");
  return components as Component[];
}

function keysOf(spec: Json): string[] {
  return componentsOf(spec)
    .map((component) => component.key)
    .filter((key): key is string => typeof key === "string");
}

function componentByKey(spec: Json, key: string): Component {
  const component = componentsOf(spec).find((candidate) => candidate.key === key);
  if (!component) throw new Error(`no component with key "${key}"`);
  return component;
}

/** The applicant/partner financial fields that reveal only for a couple. */
const PARTNER_FIELDS = [
  "partnerPwd",
  "partnerMonthlyIncome",
  "partnerVehicleValueMinusTransportation",
  "partnerVehicleValue",
  "partnerAssetValue",
] as const;

/** The number inputs that must never accept a negative value. */
const MONEY_FIELDS = [
  "dependentChildren",
  "monthlyIncome",
  "vehicleValueMinusTransportation",
  "vehicleValue",
  "assetValue",
  "partnerMonthlyIncome",
  "partnerVehicleValueMinusTransportation",
  "partnerVehicleValue",
  "partnerAssetValue",
] as const;

describe("eligibility estimator seed", () => {
  const spec = eligibilityEstimatorSpecV1;

  it("is registered in seededForms with published v1 and v2", () => {
    const estimator = seededForms.find(
      (form) => form.formSpecId === ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
    );
    expect(estimator).toBeDefined();
    expect(estimator?.title).toBe(ELIGIBILITY_ESTIMATOR_FORM_SPEC_TITLE);
    expect(estimator?.versions).toEqual([
      { version: 1, spec: eligibilityEstimatorSpecV1 },
      { version: 2, spec: eligibilityEstimatorSpecV2 },
    ]);
  });

  it("is a Form.io form with unique component keys", () => {
    expect(isRecord(spec) && spec.display).toBe("form");
    const keys = keysOf(spec);
    expect(keys.length).toBe(componentsOf(spec).length);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it("collects exactly the keys the frontend mapper expects", () => {
    expect(new Set(keysOf(spec))).toEqual(
      new Set([
        "relationshipStatus",
        "dependentChildren",
        "pwd",
        "partnerPwd",
        "monthlyIncome",
        "vehicleValueMinusTransportation",
        "vehicleValue",
        "assetValue",
        "partnerMonthlyIncome",
        "partnerVehicleValueMinusTransportation",
        "partnerVehicleValue",
        "partnerAssetValue",
        "submit",
      ]),
    );
  });

  it("offers the six production relationship options", () => {
    const relationship = componentByKey(spec, "relationshipStatus");
    const values = (relationship.values as Array<{ value: string }>).map(
      (option) => option.value,
    );
    expect(values).toEqual([
      "single",
      "married",
      "marriagelike",
      "divorced",
      "separated",
      "widowed",
    ]);
    expect(relationship.validate?.required).toBe(true);
  });

  it("requires the applicant relationship and PWD answers", () => {
    expect(componentByKey(spec, "relationshipStatus").validate?.required).toBe(true);
    expect(componentByKey(spec, "pwd").validate?.required).toBe(true);
  });

  it("reveals every spouse field with an ADVANCED (json-logic) conditional", () => {
    for (const key of PARTNER_FIELDS) {
      const field = componentByKey(spec, key);
      // Advanced conditional: `conditional.json`, never the simple `when` string.
      expect(field.conditional?.when).toBeUndefined();
      expect(field.conditional?.json).toEqual({
        in: [{ var: "relationshipStatus" }, ["married", "marriagelike"]],
      });
    }
  });

  it("never marks a spouse field server-side required", () => {
    for (const key of PARTNER_FIELDS) {
      expect(componentByKey(spec, key).validate?.required).toBeUndefined();
    }
  });

  it("uses string 'true'/'false' values for the yes/no radios", () => {
    for (const key of ["pwd", "partnerPwd"]) {
      const radio = componentByKey(spec, key);
      expect((radio.values as Array<{ value: string }>).map((o) => o.value)).toEqual([
        "true",
        "false",
      ]);
    }
  });

  it("floors every money/count input at zero", () => {
    for (const key of MONEY_FIELDS) {
      expect(componentByKey(spec, key).validate?.min).toBe(0);
    }
  });

  it("ends with a submit button labelled for the estimate", () => {
    const submit = componentByKey(spec, "submit");
    expect(submit.type).toBe("button");
  });
});

describe("eligibility estimator seed — v2 (pre-check + 0826 relabels)", () => {
  const v1 = eligibilityEstimatorSpecV1;
  const v2 = eligibilityEstimatorSpecV2;

  const PRE_CHECK_FIELDS = ["residesInBc", "hasEligibleStatus"] as const;

  /** 0826 label rewrites (keys unchanged) — see MYSS-169-0826-Seed-Label-Edits.md. */
  const RELABELS: Readonly<Record<string, string>> = {
    vehicleValueMinusTransportation:
      "What is the value of your primary vehicle minus any amount owing?",
    vehicleValue:
      "What is the value of all your additional vehicles minus any amount owing?",
    assetValue:
      "What is the total value of your assets not listed above (property, investments, cash or savings)?",
    partnerVehicleValueMinusTransportation:
      "What is the value of your spouse's primary vehicle minus any amount owing?",
    partnerVehicleValue:
      "What is the value of all your spouse's additional vehicles minus any amount owing?",
    partnerAssetValue:
      "What is the total value of your spouse's assets not listed above (property, investments, cash or savings)?",
  };

  it("adds the two pre-check questions at the very top, in order", () => {
    expect(keysOf(v2).slice(0, 2)).toEqual(["residesInBc", "hasEligibleStatus"]);
  });

  it("makes each pre-check a required yes/no radio with a tooltip, not conditional", () => {
    for (const key of PRE_CHECK_FIELDS) {
      const field = componentByKey(v2, key);
      expect(field.type).toBe("radio");
      expect(field.validate?.required).toBe(true);
      expect(field.conditional).toBeUndefined();
      expect((field.values as Array<{ value: string }>).map((o) => o.value)).toEqual([
        "true",
        "false",
      ]);
      const tooltip = (field as { tooltip?: unknown }).tooltip;
      expect(typeof tooltip).toBe("string");
      expect((tooltip as string).length).toBeGreaterThan(0);
    }
  });

  it("preserves every v1 key unchanged and adds ONLY the two pre-check keys", () => {
    const v1Keys = new Set(keysOf(v1));
    const v2Keys = new Set(keysOf(v2));
    for (const key of v1Keys) expect(v2Keys.has(key)).toBe(true);
    const added = [...v2Keys].filter((key) => !v1Keys.has(key));
    expect(new Set(added)).toEqual(new Set(["residesInBc", "hasEligibleStatus"]));
  });

  it("applies the 0826 label rewrites (labels only, keys intact)", () => {
    for (const [key, label] of Object.entries(RELABELS)) {
      expect((componentByKey(v2, key) as { label?: unknown }).label).toBe(label);
    }
  });

  it("keeps partnerPwd and the labels 0826 left unchanged", () => {
    expect((componentByKey(v2, "partnerPwd") as { label?: unknown }).label).toBe(
      "Does your spouse plan to apply for the Persons with Disabilities (PWD) designation?",
    );
    expect((componentByKey(v2, "monthlyIncome") as { label?: unknown }).label).toBe(
      "Your Monthly Income",
    );
    expect(
      (componentByKey(v2, "partnerMonthlyIncome") as { label?: unknown }).label,
    ).toBe("Spouse's Monthly Income");
  });

  it("carries v1 invariants forward: spouse fields advanced-conditional & not required", () => {
    for (const key of PARTNER_FIELDS) {
      const field = componentByKey(v2, key);
      expect(field.conditional?.when).toBeUndefined();
      expect(field.conditional?.json).toEqual({
        in: [{ var: "relationshipStatus" }, ["married", "marriagelike"]],
      });
      expect(field.validate?.required).toBeUndefined();
    }
  });

  it("keeps every money/count input floored at zero in v2", () => {
    for (const key of MONEY_FIELDS) {
      expect(componentByKey(v2, key).validate?.min).toBe(0);
    }
  });

  it("has unique component keys", () => {
    const keys = keysOf(v2);
    expect(new Set(keys).size).toBe(keys.length);
  });
});
