import { describe, expect, it } from "vitest";

import { jsonEqual } from "./json-equal";

describe("jsonEqual", () => {
  it("treats objects as equal regardless of key order", () => {
    expect(jsonEqual({ a: 1, b: 2 }, { b: 2, a: 1 })).toBe(true);
    expect(jsonEqual({ a: { x: 1, y: 2 } }, { a: { y: 2, x: 1 } })).toBe(true);
  });

  it("keeps array order significant", () => {
    expect(jsonEqual([1, 2, 3], [1, 2, 3])).toBe(true);
    expect(jsonEqual([1, 2, 3], [3, 2, 1])).toBe(false);
  });

  it("compares nested form-spec-shaped structures deeply", () => {
    const a = { components: [{ key: "x", validate: { min: 0 } }] };
    const b = { components: [{ validate: { min: 0 }, key: "x" }] };
    expect(jsonEqual(a, b)).toBe(true);

    const changed = { components: [{ key: "x", validate: { min: 1 } }] };
    expect(jsonEqual(a, changed)).toBe(false);
  });

  it("distinguishes primitives, null, and type mismatches", () => {
    expect(jsonEqual(1, 1)).toBe(true);
    expect(jsonEqual("a", "a")).toBe(true);
    expect(jsonEqual(null, null)).toBe(true);
    expect(jsonEqual(0, false)).toBe(false);
    expect(jsonEqual(null, {})).toBe(false);
  });
});
