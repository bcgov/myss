import { describe, expect, it } from "vitest";

import {
  findDuplicateKeys,
  findUnknownConditionalTargets,
} from "@/widgets/formio/specRules";

// Pure structural checks mirroring MyssContent's form-spec-rules.ts. The nested
// cases are the ones a naive top-level loop misses — the real estimator spec
// nests components inside panels, columns and a bcgov accordion.

describe("findDuplicateKeys", () => {
  it("returns nothing for a clean spec", () => {
    const spec = {
      components: [
        { key: "firstName", type: "textfield" },
        { key: "lastName", type: "textfield" },
      ],
    };
    expect(findDuplicateKeys(spec)).toEqual([]);
  });

  it("flags a duplicate key at the top level", () => {
    const spec = {
      components: [
        { key: "dupe", type: "textfield" },
        { key: "other", type: "textfield" },
        { key: "dupe", type: "number" },
      ],
    };
    expect(findDuplicateKeys(spec)).toEqual(["dupe"]);
  });

  it("finds a duplicate nested inside a panel", () => {
    const spec = {
      components: [
        { key: "email", type: "textfield" },
        {
          key: "about",
          type: "panel",
          components: [{ key: "email", type: "textfield" }],
        },
      ],
    };
    expect(findDuplicateKeys(spec)).toEqual(["email"]);
  });

  it("finds duplicates nested in columns and table cells", () => {
    const spec = {
      components: [
        { key: "a", type: "textfield" },
        {
          type: "columns",
          key: "cols",
          columns: [{ components: [{ key: "a", type: "textfield" }] }],
        },
        {
          type: "table",
          key: "grid",
          rows: [[{ components: [{ key: "b", type: "textfield" }] }]],
        },
        {
          type: "panel",
          key: "p",
          components: [{ key: "b", type: "number" }],
        },
      ],
    };
    expect(findDuplicateKeys(spec)).toEqual(["a", "b"]);
  });

  it("ignores components with a missing or blank key (that is a different rule)", () => {
    const spec = {
      components: [
        { type: "textfield" },
        { key: "   ", type: "textfield" },
        { key: "real", type: "textfield" },
      ],
    };
    expect(findDuplicateKeys(spec)).toEqual([]);
  });

  it("returns multiple duplicates sorted, regardless of discovery order", () => {
    const spec = {
      components: [
        { key: "zebra", type: "textfield" },
        { key: "alpha", type: "textfield" },
        { key: "zebra", type: "number" },
        { key: "alpha", type: "number" },
      ],
    };
    // "zebra" is seen first, but the result is sorted; each key appears once.
    expect(findDuplicateKeys(spec)).toEqual(["alpha", "zebra"]);
  });

  it("tolerates a malformed spec without throwing", () => {
    expect(findDuplicateKeys(undefined)).toEqual([]);
    expect(findDuplicateKeys({})).toEqual([]);
    expect(findDuplicateKeys({ components: "nope" })).toEqual([]);
  });
});

describe("findUnknownConditionalTargets", () => {
  it("returns nothing when every conditional.when names a real key", () => {
    const spec = {
      components: [
        { key: "hasEligibleStatus", type: "radio" },
        {
          key: "relationshipStatus",
          type: "radio",
          conditional: { show: true, when: "hasEligibleStatus", eq: "true" },
        },
      ],
    };
    expect(findUnknownConditionalTargets(spec)).toEqual([]);
  });

  it("flags a conditional.when pointing at a key that does not exist", () => {
    const spec = {
      components: [
        { key: "a", type: "radio" },
        {
          key: "b",
          type: "textfield",
          conditional: { show: true, when: "missingKey", eq: "x" },
        },
      ],
    };
    expect(findUnknownConditionalTargets(spec)).toEqual(["missingKey"]);
  });

  it("resolves a target defined inside a nested container", () => {
    const spec = {
      components: [
        {
          type: "panel",
          key: "p",
          components: [{ key: "trigger", type: "radio" }],
        },
        {
          key: "dependent",
          type: "textfield",
          conditional: { show: true, when: "trigger", eq: "yes" },
        },
      ],
    };
    expect(findUnknownConditionalTargets(spec)).toEqual([]);
  });

  it("ignores advanced json-logic conditionals, which carry no `when`", () => {
    const spec = {
      components: [
        { key: "residesInBc", type: "radio" },
        {
          key: "gated",
          type: "textfield",
          conditional: {
            json: { in: [{ var: "data.residesInBc" }, ["true", "false"]] },
          },
        },
      ],
    };
    expect(findUnknownConditionalTargets(spec)).toEqual([]);
  });

  it("ignores an empty-string `when`", () => {
    const spec = {
      components: [
        { key: "a", type: "radio" },
        { key: "b", type: "textfield", conditional: { show: true, when: "", eq: "x" } },
      ],
    };
    expect(findUnknownConditionalTargets(spec)).toEqual([]);
  });

  it("returns multiple unknown targets sorted and deduped", () => {
    const spec = {
      components: [
        { key: "a", type: "radio", conditional: { show: true, when: "zzz", eq: "1" } },
        { key: "b", type: "radio", conditional: { show: true, when: "aaa", eq: "1" } },
        { key: "c", type: "radio", conditional: { show: true, when: "zzz", eq: "2" } },
      ],
    };
    expect(findUnknownConditionalTargets(spec)).toEqual(["aaa", "zzz"]);
  });

  it("tolerates a malformed spec without throwing", () => {
    expect(findUnknownConditionalTargets(undefined)).toEqual([]);
    expect(findUnknownConditionalTargets({})).toEqual([]);
    expect(findUnknownConditionalTargets({ components: "nope" })).toEqual([]);
  });
});

// A faithful slice of the live estimator spec: both conditional forms (simple
// `when` + advanced json-logic), a bcgov accordion, nested content. Neither rule
// must fire on a spec that is already published, or a designer would be blocked
// from saving a live form.
const estimatorV3Like = {
  display: "form",
  components: [
    { key: "residesInBc", type: "radio", input: true },
    {
      key: "hasEligibleStatus",
      type: "radio",
      input: true,
      conditional: { json: { in: [{ var: "data.residesInBc" }, ["true", "false"]] } },
    },
    {
      key: "statusHelp",
      type: "bcgovAccordion",
      input: false,
      conditional: { json: { in: [{ var: "data.residesInBc" }, ["true", "false"]] } },
    },
    {
      key: "relationshipStatus",
      type: "radio",
      input: true,
      conditional: { show: true, when: "hasEligibleStatus", eq: "true" },
    },
    {
      key: "assetsSectionHeading",
      type: "content",
      input: false,
      conditional: { show: true, when: "hasEligibleStatus", eq: "true" },
    },
    {
      key: "partnerPwd",
      type: "radio",
      input: true,
      conditional: {
        json: { in: [{ var: "data.relationshipStatus" }, ["married", "marriagelike"]] },
      },
    },
    {
      key: "submit",
      type: "button",
      input: true,
      conditional: { show: true, when: "hasEligibleStatus", eq: "true" },
    },
  ],
};

describe("the live estimator v3 spec shape", () => {
  it("produces no false positives from either rule", () => {
    expect(findDuplicateKeys(estimatorV3Like)).toEqual([]);
    expect(findUnknownConditionalTargets(estimatorV3Like)).toEqual([]);
  });
});
