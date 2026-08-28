import { describe, expect, it } from "vitest";

import {
  ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
  ELIGIBILITY_ESTIMATOR_FORM_SPEC_TITLE,
  POC_FORM_SPEC_ID,
  POC_FORM_SPEC_TITLE,
  seededForms,
  seededFormSpecs,
  testFormSpecV1,
  testFormSpecV2,
  testFormSpecV3,
  type Json,
} from "./form-spec-seed-data";

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
  readonly type?: unknown;
  readonly conditional?: { readonly when?: unknown };
  readonly properties?: { readonly myssValidator?: unknown };
  readonly components?: unknown;
  readonly columns?: unknown;
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
  it("seeds the POC form and the eligibility estimator", () => {
    const ids = seededForms.map((form) => form.formSpecId);
    expect(ids).toContain(POC_FORM_SPEC_ID);
    expect(ids).toContain(ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID);
    // Every seeded form id is distinct — the bootstrap hook keys on it.
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("keeps the POC form's versions as the existing seededFormSpecs list", () => {
    const poc = seededForms.find(
      (form) => form.formSpecId === POC_FORM_SPEC_ID,
    );
    expect(poc?.title).toBe(POC_FORM_SPEC_TITLE);
    expect(poc?.versions).toBe(seededFormSpecs);
  });

  it("seeds the eligibility estimator with v1 and v2", () => {
    const estimator = seededForms.find(
      (form) => form.formSpecId === ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
    );
    expect(estimator?.title).toBe(ELIGIBILITY_ESTIMATOR_FORM_SPEC_TITLE);
    expect(estimator?.versions.map((v) => v.version)).toEqual([1, 2]);
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
