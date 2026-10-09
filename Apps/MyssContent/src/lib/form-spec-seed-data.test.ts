import { describe, expect, it } from "vitest";

import {
  BUS_PASS_FORM_SPEC_ID,
  BUS_PASS_FORM_SPEC_TITLE,
  busPassFormSpecV1,
  busPassFormSpecV2,
  busPassFormSpecV3,
  busPassFormSpecV4,
  busPassFormSpecV5,
  busPassFormSpecV6,
  busPassFormSpecV7,
  ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
  ELIGIBILITY_ESTIMATOR_FORM_SPEC_TITLE,
  INCOME_ASSISTANCE_FORM_SPEC_ID,
  INCOME_ASSISTANCE_FORM_SPEC_TITLE,
  incomeAssistanceFormSpecV1,
  POC_FORM_SPEC_ID,
  POC_FORM_SPEC_TITLE,
  REGISTRATION_FORM_SPEC_ID,
  REGISTRATION_FORM_SPEC_TITLE,
  registrationFormSpecV2,
  registrationFormSpecV3,
  registrationFormSpecV4,
  registrationFormSpecV5,
  seededForms,
  seededFormSpecs,
  testFormSpecV1,
  testFormSpecV2,
  testFormSpecV3,
  type Json,
} from "./form-spec-seed-data";
import { validateFormSpec } from "./form-spec-rules";

/**
 * These tests assert the invariants that the Phase 0.3 publish-time lifecycle
 * hook will enforce on every form spec, applied here to the specs this app
 * ships itself. Seed data that the hook would reject is a contradiction worth
 * catching in CI rather than on someone's first `docker compose up`.
 *
 * The shape helpers below are deliberately local to this file. The reusable
 * validator is 0.3's job; duplicating a few lines here keeps 0.2 to a test
 * harness and nothing more.
 */

interface Component {
  readonly key?: unknown;
  readonly html?: unknown;
  readonly label?: unknown;
  readonly type?: unknown;
  readonly values?: unknown;
  readonly inputMask?: unknown;
  readonly placeholder?: unknown;
  readonly validateOn?: unknown;
  readonly conditional?: { readonly when?: unknown };
  readonly properties?: {
    readonly myssValidator?: unknown;
    readonly myssPrefill?: unknown;
    readonly myssPrefillLock?: unknown;
  };
  readonly validate?: {
    readonly customMessage?: unknown;
    readonly pattern?: unknown;
    readonly required?: unknown;
  };
  readonly components?: unknown;
  readonly columns?: unknown;
  readonly addressFields?: unknown;
}

function isRecord(value: Json): value is { [key: string]: Json } {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function componentsOf(spec: Json): Component[] {
  if (!isRecord(spec)) throw new Error("spec is not an object");
  const components = spec.components;
  if (!Array.isArray(components))
    throw new Error("spec.components is not an array");
  return components as Component[];
}

function allComponents(spec: Json): Component[] {
  const components = componentsOf(spec);
  const nested: Component[] = [];

  for (const component of components) {
    nested.push(component);
    if (Array.isArray(component.components)) {
      nested.push(...allComponents({ components: component.components }));
    }
    if (Array.isArray(component.columns)) {
      for (const column of component.columns) {
        if (isRecord(column) && Array.isArray(column.components)) {
          nested.push(...allComponents({ components: column.components }));
        }
      }
    }
  }

  return nested;
}

function keysOf(spec: Json): string[] {
  return allComponents(spec)
    .map((component) => component.key)
    .filter((key): key is string => typeof key === "string");
}

function componentByKey(spec: Json, key: string): Component {
  const component = allComponents(spec).find(
    (candidate) => candidate.key === key,
  );
  if (!component) throw new Error(`no component with key "${key}"`);
  return component;
}

describe("seeded form specs", () => {
  it("seeds contiguous versions starting at 1", () => {
    const versions = seededFormSpecs.map((seeded) => seeded.version);
    expect(versions).toEqual(versions.map((_, index) => index + 1));
  });

  it("exposes a single logical form id and title", () => {
    expect(POC_FORM_SPEC_ID).toBe("poc-test-form");
    expect(POC_FORM_SPEC_TITLE).toBe("POC test form");
  });

  it.each(
    seededFormSpecs.map((seeded) => [seeded.version, seeded.spec] as const),
  )("v%i is a Form.io form with at least one component", (_version, spec) => {
    expect(isRecord(spec) && spec.display).toBe("form");
    expect(componentsOf(spec).length).toBeGreaterThan(0);
  });

  it.each(
    seededFormSpecs.map((seeded) => [seeded.version, seeded.spec] as const),
  )("v%i gives every component a unique key", (_version, spec) => {
    const keys = keysOf(spec);
    expect(keys.length).toBe(allComponents(spec).length);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it.each(
    seededFormSpecs.map((seeded) => [seeded.version, seeded.spec] as const),
  )(
    "v%i only references fields that exist in its conditionals",
    (_version, spec) => {
      const keys = new Set(keysOf(spec));
      const referenced = allComponents(spec)
        .map((component) => component.conditional?.when)
        .filter((when): when is string => typeof when === "string");

      // Guards against a vacuous pass: the POC form is meant to demonstrate
      // conditional visibility, so there must be at least one conditional.
      expect(referenced.length).toBeGreaterThan(0);
      for (const when of referenced) {
        expect(keys).toContain(when);
      }
    },
  );

  it("adds a field in v2 without removing any from v1", () => {
    const v1 = keysOf(testFormSpecV1);
    const v2 = keysOf(testFormSpecV2);

    expect(v2).toEqual(expect.arrayContaining(v1));
    expect(v2.length).toBeGreaterThan(v1.length);
    expect(v2).toContain("contactEmail");
  });

  it("adds a field in v3 without removing any from v2", () => {
    const v2 = keysOf(testFormSpecV2);
    const v3 = keysOf(testFormSpecV3);

    expect(v3).toEqual(expect.arrayContaining(v2));
    expect(v3.length).toBeGreaterThan(v2.length);
    expect(v3).toContain("sin");
  });

  /**
   * The marker is the whole point of v3, and getting it wrong fails SILENTLY:
   * Form.io ignores unknown `properties` keys, so a misspelled marker renders a
   * perfectly good field whose answer never reaches the SIN rule. Nothing else
   * in this repo would catch that, hence an assertion on the literal strings.
   *
   * `"sin"` and the `myssValidator` key are a contract with the `RuleFor`
   * lookup in MyssApi/Services/FormSpecValidator.cs. Renaming either means
   * changing both sides together.
   */
  it("marks the v3 SIN field for the server-side SIN validator", () => {
    const sin = componentByKey(testFormSpecV3, "sin");

    // A plain textfield, deliberately: the marker route is what lets an author
    // add a validated field without the Phase 1 custom `sin` component.
    expect(sin.type).toBe("textfield");
    expect(sin.properties?.myssValidator).toBe("sin");
  });
});

describe("seeded forms collection", () => {
  it("seeds the POC form, income assistance, eligibility estimator and bus pass", () => {
    const ids = seededForms.map((form) => form.formSpecId);
    expect(ids).toContain(POC_FORM_SPEC_ID);
    expect(ids).toContain(INCOME_ASSISTANCE_FORM_SPEC_ID);
    expect(ids).toContain(ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID);
    expect(ids).toContain(BUS_PASS_FORM_SPEC_ID);
    // Every seeded form id is distinct — the bootstrap hook keys on it.
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("seeds the registration form with all five versions", () => {
    const registration = seededForms.find(
      (form) => form.formSpecId === REGISTRATION_FORM_SPEC_ID,
    );
    expect(registration?.title).toBe(REGISTRATION_FORM_SPEC_TITLE);
    expect(registration?.versions.map((v) => v.version)).toEqual([1, 2, 3, 4, 5]);
    expect(registration?.versions[1]?.spec).toBe(registrationFormSpecV2);
    expect(registration?.versions[2]?.spec).toBe(registrationFormSpecV3);
    expect(registration?.versions[3]?.spec).toBe(registrationFormSpecV4);
    expect(registration?.versions[4]?.spec).toBe(registrationFormSpecV5);
  });

  it("keeps registration v4 immutable and uses the shared phone rule in v5", () => {
    const oldPhone = componentByKey(registrationFormSpecV4, "phone");
    const phone = componentByKey(registrationFormSpecV5, "phone");

    expect(validateFormSpec(registrationFormSpecV5)).toEqual([]);
    expect(oldPhone.type).toBe("phoneNumber");
    expect(oldPhone.inputMask).toBe("999-999-9999");
    expect(oldPhone.properties?.myssValidator).toBeUndefined();
    expect(phone.type).toBe("textfield");
    expect(phone.inputMask).toBeUndefined();
    expect(phone.validate?.pattern).toBeUndefined();
    expect(phone.validate?.required).toBe(true);
    expect(phone.validate?.customMessage).toBe("Phone number is invalid");
    expect(phone.validateOn).toBe("blur");
    expect(phone.properties).toEqual({
      myssPrefill: "phoneNumber",
      myssPrefillLock: "true",
      myssValidator: "phone",
    });
  });

  it("requires registration consent", () => {
    const consent = componentByKey(registrationFormSpecV3, "consent");

    expect(consent.type).toBe("checkbox");
    expect(consent.label).toContain("https://myselfserve.gov.bc.ca/terms");
    expect(consent.label).toContain(
      "https://www2.gov.bc.ca/gov/content/home/privacy",
    );
  });

  it("marks registration SIN and email fields for validation", () => {
    const sin = componentByKey(registrationFormSpecV2, "sin");
    const email = componentByKey(registrationFormSpecV2, "email");

    expect(sin.type).toBe("textfield");
    expect(sin.properties?.myssValidator).toBe("sin");
    expect(email.type).toBe("email");
  });

  describe("registration v4", () => {
    it("passes the lifecycle's structural rules", () => {
      expect(validateFormSpec(registrationFormSpecV4)).toEqual([]);
    });

    it("keeps every v3 field and adds phone and gender", () => {
      const v3 = keysOf(registrationFormSpecV3).filter((k) => k !== "submit");
      const v4 = keysOf(registrationFormSpecV4);

      expect(v4).toEqual(expect.arrayContaining(v3));
      expect(v4).toContain("phone");
      expect(v4).toContain("gender");
    });

    // The webclient renders its own BC Gov buttons; a spec submit button
    // would be stripped anyway, so it should not be authored.
    it("has no submit button", () => {
      expect(
        allComponents(registrationFormSpecV4).some((c) => c.type === "button"),
      ).toBe(false);
    });

    // A contract with MyssApi: FormsService checks a submitted gender against
    // these option values, and the webclient maps the identity claim to them.
    it("offers the gender options as a BC Gov radio", () => {
      const gender = componentByKey(registrationFormSpecV4, "gender");

      expect(gender.type).toBe("bcgovRadio");
      expect(gender.values).toEqual([
        { label: "Man/Boy", value: "man" },
        { label: "Non-Binary", value: "nonBinary" },
        { label: "Woman/Girl", value: "woman" },
      ]);
    });

    // Misspelled markers fail silently (Form.io ignores unknown properties), so
    // assert the literal strings the webclient's prefill reads.
    it.each([
      ["firstName", "givenName", "true"],
      ["lastName", "familyName", "true"],
      ["email", "email", undefined],
      ["phone", "phoneNumber", "true"],
      ["dateOfBirth", "birthdate", "true"],
      ["gender", "gender", "true"],
    ])("prefills %s from %s (lock: %s)", (key, attribute, lock) => {
      const component = componentByKey(registrationFormSpecV4, key);

      expect(component.properties?.myssPrefill).toBe(attribute);
      expect(component.properties?.myssPrefillLock).toBe(lock);
    });

    it("keeps the SIN validator and the consent links", () => {
      const sin = componentByKey(registrationFormSpecV4, "sin");
      const consent = componentByKey(registrationFormSpecV4, "consent");

      expect(sin.properties?.myssValidator).toBe("sin");
      expect(componentByKey(registrationFormSpecV4, "sinHelp").type).toBe(
        "bcgovAccordion",
      );
      expect(consent.type).toBe("checkbox");
      expect(consent.validate?.required).toBe(true);
      expect(consent.label).toContain("https://myselfserve.gov.bc.ca/terms");
      expect(consent.label).toContain(
        "https://www2.gov.bc.ca/gov/content/home/privacy",
      );
    });
  });

  it("keeps the POC form's versions as the existing seededFormSpecs list", () => {
    const poc = seededForms.find(
      (form) => form.formSpecId === POC_FORM_SPEC_ID,
    );
    expect(poc?.title).toBe(POC_FORM_SPEC_TITLE);
    expect(poc?.versions).toBe(seededFormSpecs);
  });

  it("seeds the income assistance application with v1 only", () => {
    const incomeAssistance = seededForms.find(
      (form) => form.formSpecId === INCOME_ASSISTANCE_FORM_SPEC_ID,
    );
    expect(INCOME_ASSISTANCE_FORM_SPEC_ID).toBe("income-assistance-poc");
    expect(incomeAssistance?.title).toBe(INCOME_ASSISTANCE_FORM_SPEC_TITLE);
    expect(incomeAssistance?.versions).toEqual([
      { version: 1, spec: incomeAssistanceFormSpecV1 },
    ]);
  });

  /**
   * The keys and the required flags are a contract with MyssApi's intake
   * module and the webclient's application form: submit must fail with
   * FORM.FIELD.REQUIRED on firstName/lastName and nothing else, and a draft
   * save must be allowed with either missing.
   */
  it("asks income assistance applicants for first, middle and last name", () => {
    expect(keysOf(incomeAssistanceFormSpecV1)).toEqual([
      "firstName",
      "middleName",
      "lastName",
      "submit",
    ]);
    expect(
      componentByKey(incomeAssistanceFormSpecV1, "firstName").validate
        ?.required,
    ).toBe(true);
    expect(
      componentByKey(incomeAssistanceFormSpecV1, "middleName").validate,
    ).toBeUndefined();
    expect(
      componentByKey(incomeAssistanceFormSpecV1, "lastName").validate?.required,
    ).toBe(true);
    expect(componentByKey(incomeAssistanceFormSpecV1, "submit").type).toBe(
      "button",
    );
  });

  it("seeds the eligibility estimator with v1, v2, v3 and v4", () => {
    const estimator = seededForms.find(
      (form) => form.formSpecId === ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
    );
    expect(estimator?.title).toBe(ELIGIBILITY_ESTIMATOR_FORM_SPEC_TITLE);
    expect(estimator?.versions.map((v) => v.version)).toEqual([1, 2, 3, 4]);
  });

  it("seeds the bus pass with v1 through v7", () => {
    const busPass = seededForms.find(
      (form) => form.formSpecId === BUS_PASS_FORM_SPEC_ID,
    );
    expect(busPass?.title).toBe(BUS_PASS_FORM_SPEC_TITLE);
    expect(busPass?.versions).toEqual([
      { version: 1, spec: busPassFormSpecV1 },
      { version: 2, spec: busPassFormSpecV2 },
      { version: 3, spec: busPassFormSpecV3 },
      { version: 4, spec: busPassFormSpecV4 },
      { version: 5, spec: busPassFormSpecV5 },
      { version: 6, spec: busPassFormSpecV6 },
      { version: 7, spec: busPassFormSpecV7 },
    ]);
  });

  it("orders the bus pass v3 service choices as existing details then new", () => {
    const serviceRequest = componentByKey(
      busPassFormSpecV3,
      "serviceRequestType",
    );
    const values = serviceRequest.values as Array<{
      label: string;
      value: string;
      children?: Array<{ label: string; value: string }>;
    }>;

    expect(serviceRequest.type).toBe("bcgovRadio");
    expect(serviceRequest.label).toBe(
      "What type of service would you like to request?",
    );
    expect(serviceRequest.validate?.required).toBe(true);
    expect(values.map(({ value }) => value)).toEqual([
      "existing",
      "newApplication",
    ]);
    expect(values[0].children?.map(({ value }) => value)).toEqual([
      "addressUpdate",
      "replacement",
    ]);
  });

  it("keys bus pass v3 new-applicant fields from the canonical service value", () => {
    for (const key of [
      "newApplicantEligibilityInfo",
      "eligibilityAcknowledged",
      "eligibilityCategory",
    ]) {
      expect(componentByKey(busPassFormSpecV3, key).conditional).toEqual({
        eq: "newApplication",
        show: true,
        when: "serviceRequestType",
      });
    }
  });

  it("shows replacement cost information and requires cancellation acknowledgement in v4", () => {
    const replacementCostNote = componentByKey(
      busPassFormSpecV4,
      "replacementCostNote",
    );
    const acknowledgement = componentByKey(
      busPassFormSpecV4,
      "acknowledgedPassCancellation",
    );

    expect(replacementCostNote.type).toBe("content");
    expect(replacementCostNote.html).toBe(
      "<p><strong>Note: You will be billed $10 if this is your first replacement pass, $20 if this is your second replacement pass, and $50 for each additional replacement pass during your annual eligibility period.</strong></p>",
    );
    expect(replacementCostNote.conditional).toEqual({
      eq: "replacement",
      show: true,
      when: "serviceRequestType",
    });

    expect(acknowledgement.type).toBe("checkbox");
    expect(acknowledgement.label).toBe(
      "I acknowledge that my lost/stolen bus pass will be cancelled before a replacement application is mailed to me. Bus passes cannot be reactivated if found.",
    );
    expect(acknowledgement.validate).toEqual({
      required: true,
      customMessage: "Acknowledgement is required",
    });
    expect(acknowledgement.conditional).toEqual({
      eq: "replacement",
      show: true,
      when: "serviceRequestType",
    });
  });

  it("configures residential and mailing address autocomplete in v5", () => {
    const residential = componentByKey(busPassFormSpecV5, "streetAddress1");
    const mailing = componentByKey(busPassFormSpecV5, "mailingStreetAddress1");

    expect(residential.type).toBe("bcgovAddressAutocomplete");
    expect(residential.addressFields).toEqual({
      line2: "streetAddress2",
      city: "city",
      province: "province",
      postalCode: "postalCode",
    });

    expect(mailing.type).toBe("bcgovAddressAutocomplete");
    expect(mailing.addressFields).toEqual({
      line2: "mailingStreetAddress2",
      city: "mailingCity",
      province: "mailingProvince",
      postalCode: "mailingPostalCode",
    });
  });

  it("accepts Canada Post postal codes with or without the standard space in v5", () => {
    for (const key of ["postalCode", "mailingPostalCode"]) {
      const pattern = componentByKey(busPassFormSpecV5, key).validate?.pattern;
      expect(pattern).toBeTypeOf("string");
      expect(new RegExp(String(pattern)).test("V8V 1X4")).toBe(true);
      expect(new RegExp(String(pattern)).test("V8V1X4")).toBe(true);
      expect(new RegExp(String(pattern)).test("not a postal code")).toBe(false);
    }
  });

  it("allows one- or two-digit birth days in bus pass v2", () => {
    const birthDay = componentByKey(busPassFormSpecV2, "birthDay");

    expect(birthDay.inputMask).toBe("9[9]");
  });

  it("shows the expected phone number format in bus pass v2", () => {
    const phoneNumber = componentByKey(busPassFormSpecV2, "phoneNumber");

    expect(phoneNumber.inputMask).toBe("(999) 999-9999");
    expect(phoneNumber.placeholder).toBe("(999) 999-9999");
  });

  it("uses the shared phone rule without a second v7 pattern or mask", () => {
    const oldPhone = componentByKey(busPassFormSpecV6, "phoneNumber");
    const phone = componentByKey(busPassFormSpecV7, "phoneNumber");

    expect(oldPhone.validate?.pattern).toBeUndefined();
    expect(phone.properties?.myssValidator).toBe("phone");
    expect(phone.validateOn).toBe("blur");
    expect(phone.validate?.required).toBe(true);
    expect(phone.validate?.customMessage).toBe("Phone number is invalid");
    expect(phone.validate?.pattern).toBeUndefined();
    expect(phone.inputMask).toBeUndefined();
    expect(phone.placeholder).toBe(oldPhone.placeholder);
  });

  it("gives every seeded form at least one version, each a valid Form.io form", () => {
    for (const form of seededForms) {
      expect(form.versions.length).toBeGreaterThan(0);
      for (const { spec } of form.versions) {
        expect(isRecord(spec) && spec.display).toBe("form");
        const keys = keysOf(spec);
        expect(keys.length).toBeGreaterThan(0);
        expect(new Set(keys).size).toBe(keys.length);
      }
    }
  });
});

describe("bus pass seed — v2 (server-side SIN validation)", () => {
  it("preserves v1 and opts the v2 SIN field into the MyssApi validator", () => {
    const v1Sin = componentByKey(busPassFormSpecV1, "socialInsuranceNumber");
    const v2Sin = componentByKey(busPassFormSpecV2, "socialInsuranceNumber");

    expect(v1Sin.validate?.pattern).toBe("^[0-9]{9}$");
    expect(v1Sin.properties?.myssValidator).toBeUndefined();

    expect(v2Sin.type).toBe("textfield");
    expect(v2Sin.validate?.required).toBe(false);
    expect(v2Sin.validate?.pattern).toBeUndefined();
    expect(v2Sin.validate?.customMessage).toBeUndefined();
    expect(v2Sin.properties?.myssValidator).toBe("sin");
    expect(v2Sin.inputMask).toBeUndefined();
  });

  it("otherwise keeps the v1 component structure", () => {
    expect(keysOf(busPassFormSpecV2)).toEqual(keysOf(busPassFormSpecV1));
  });
});

describe("bus pass seed — v6 (named validation rules)", () => {
  const componentByKey = (spec: Json, key: string) => {
    const found: Record<string, unknown>[] = [];
    const walk = (nodes: unknown) => {
      if (!Array.isArray(nodes)) return;
      for (const node of nodes) {
        if (typeof node !== "object" || node === null) continue;
        const component = node as Record<string, unknown>;
        if (component.key === key) found.push(component);
        walk(component.components);
        if (Array.isArray(component.columns)) {
          for (const column of component.columns) {
            walk((column as Record<string, unknown>).components);
          }
        }
      }
    };
    walk((spec as Record<string, unknown>).components);
    return found[0] as Record<string, unknown> & {
      properties?: Record<string, unknown>;
      errors?: Record<string, unknown>;
      validate?: Record<string, unknown>;
    };
  };

  it("names the phone and postal code rules the API and the browser both run", () => {
    expect(componentByKey(busPassFormSpecV6, "phoneNumber").properties).toEqual(
      {
        myssValidator: "phone",
      },
    );
    for (const key of ["postalCode", "mailingPostalCode"]) {
      expect(componentByKey(busPassFormSpecV6, key).properties).toEqual({
        myssValidator: "postalCode",
      });
    }
  });

  it("names the email verification's partner instead of checking it in browser script", () => {
    const verification = componentByKey(busPassFormSpecV6, "emailVerification");
    expect(verification.properties).toEqual({ myssMatches: "email" });
    expect(verification.validate?.custom).toBeUndefined();
    expect(verification.errors).toEqual({
      matches: "The two email addresses do not match",
    });
  });

  it("checks the birth day against its month and year, with per-rule wording", () => {
    const birthDay = componentByKey(busPassFormSpecV6, "birthDay");
    expect(birthDay.properties).toEqual({
      myssValidator: "dateParts",
      myssDateParts: { month: "birthMonth", year: "birthYear" },
    });
    expect(birthDay.errors).toEqual({
      required: "A birth day is required",
      dateParts: "Enter a valid date of birth",
    });
    // The catch-all message is gone: it would have worded the date rule too.
    expect(birthDay.validate?.customMessage).toBeUndefined();
  });

  it("keeps the SIN rule from v5", () => {
    expect(
      componentByKey(busPassFormSpecV6, "socialInsuranceNumber").properties,
    ).toEqual({ myssValidator: "sin" });
  });
});
