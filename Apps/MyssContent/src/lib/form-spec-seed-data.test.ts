import { describe, expect, it } from "vitest";

import {
  POC_FORM_SPEC_ID,
  POC_FORM_SPEC_TITLE,
  seededFormSpecs,
  testFormSpecV1,
  testFormSpecV2,
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
  readonly conditional?: { readonly when?: unknown };
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

describe("seeded form specs", () => {
  it("seeds contiguous versions starting at 1", () => {
    const versions = seededFormSpecs.map((seeded) => seeded.version);
    expect(versions).toEqual(versions.map((_, index) => index + 1));
  });

  it("exposes a single logical form id and title", () => {
    expect(POC_FORM_SPEC_ID).toBe("poc-test-form");
    expect(POC_FORM_SPEC_TITLE).toBe("POC test form");
  });

  it.each(seededFormSpecs.map((seeded) => [seeded.version, seeded.spec] as const))(
    "v%i is a Form.io form with at least one component",
    (_version, spec) => {
      expect(isRecord(spec) && spec.display).toBe("form");
      expect(componentsOf(spec).length).toBeGreaterThan(0);
    },
  );

  it.each(seededFormSpecs.map((seeded) => [seeded.version, seeded.spec] as const))(
    "v%i gives every component a unique key",
    (_version, spec) => {
      const keys = keysOf(spec);
      expect(keys.length).toBe(componentsOf(spec).length);
      expect(new Set(keys).size).toBe(keys.length);
    },
  );

  it.each(seededFormSpecs.map((seeded) => [seeded.version, seeded.spec] as const))(
    "v%i only references fields that exist in its conditionals",
    (_version, spec) => {
      const keys = new Set(keysOf(spec));
      const referenced = componentsOf(spec)
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
});
