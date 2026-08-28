import { describe, expect, it } from "vitest";

import {
  FormSpecRule,
  assertVersionSequence,
  canonicalJson,
  checkImmutability,
  evaluateCreate,
  evaluateUpdate,
  findPublishedSibling,
  formatViolations,
  isNewDocument,
  parseSpec,
  validateFormSpec,
  type FormSpecRow,
} from "./form-spec-rules";

import { seededFormSpecs, testFormSpecV1 } from "./form-spec-seed-data";

const keywords = (violations: { keyword: string }[]) => violations.map((v) => v.keyword);

const minimalSpec = {
  display: "form",
  components: [{ type: "textfield", key: "firstName", label: "First name", input: true }],
};

/**
 * The fixtures below mirror event payloads MEASURED on Strapi 5.51 (2026-08-17),
 * not invented shapes. `documentId: null` on a genuine create and a populated
 * `documentId` on the publish-create are the real discriminator.
 */
const DOC = "l575dmudtfaimhkwnwxqmn1b";

const draftRow = (over: Partial<FormSpecRow> = {}): FormSpecRow => ({
  id: 7,
  documentId: DOC,
  formSpecId: "poc-test-form",
  version: 3,
  publishedAt: null,
  spec: minimalSpec,
  ...over,
});

const publishedRow = (over: Partial<FormSpecRow> = {}): FormSpecRow => ({
  id: 8,
  documentId: DOC,
  formSpecId: "poc-test-form",
  version: 3,
  publishedAt: "2026-08-17T22:00:14.188Z",
  spec: minimalSpec,
  ...over,
});

describe("parseSpec", () => {
  it("accepts the object form sent by the Document Service", () => {
    const result = parseSpec(minimalSpec);
    expect(result.ok).toBe(true);
  });

  it("accepts the string form sent by the admin panel", () => {
    const result = parseSpec(JSON.stringify(minimalSpec));
    expect(result.ok && result.value.display).toBe("form");
  });

  it("reports invalid JSON rather than throwing", () => {
    const result = parseSpec("{ not json");
    expect(result.ok).toBe(false);
    expect(!result.ok && result.violation.keyword).toBe(FormSpecRule.SpecUnparseable);
  });

  it("rejects a JSON array and a bare scalar", () => {
    expect(parseSpec("[]").ok).toBe(false);
    expect(parseSpec(42).ok).toBe(false);
  });
});

describe("validateFormSpec", () => {
  it("passes both seeded specs", () => {
    for (const { spec } of seededFormSpecs) {
      expect(validateFormSpec(spec)).toEqual([]);
    }
  });

  it("passes a seeded spec that has been serialised, as the admin panel sends it", () => {
    expect(validateFormSpec(JSON.stringify(testFormSpecV1))).toEqual([]);
  });

  it("rejects a missing or empty components array", () => {
    expect(keywords(validateFormSpec({ display: "form" }))).toEqual([
      FormSpecRule.ComponentsMissing,
    ]);
    expect(keywords(validateFormSpec({ display: "form", components: [] }))).toEqual([
      FormSpecRule.ComponentsEmpty,
    ]);
  });

  it("rejects duplicate keys, naming the offender once", () => {
    const violations = validateFormSpec({
      components: [
        { key: "firstName" },
        { key: "firstName" },
        { key: "firstName" },
        { key: "lastName" },
      ],
    });
    expect(keywords(violations)).toEqual([FormSpecRule.ComponentKeyDuplicate]);
    expect(violations[0].message).toContain("firstName");
  });

  it("rejects a component with a missing or blank key", () => {
    expect(keywords(validateFormSpec({ components: [{ type: "textfield" }] }))).toEqual([
      FormSpecRule.ComponentKeyMissing,
    ]);
    expect(keywords(validateFormSpec({ components: [{ key: "   " }] }))).toEqual([
      FormSpecRule.ComponentKeyMissing,
    ]);
  });

  it("rejects a conditional pointing at a field that does not exist", () => {
    const violations = validateFormSpec({
      components: [
        { key: "relationship" },
        { key: "spouseName", conditional: { show: true, when: "maritalStatus", eq: "couple" } },
      ],
    });
    expect(keywords(violations)).toEqual([FormSpecRule.ConditionalUnknownField]);
    expect(violations[0].message).toContain("maritalStatus");
  });

  it("accepts a conditional pointing at a field nested inside a panel", () => {
    // A rule that only walked the top level would wrongly reject this.
    expect(
      validateFormSpec({
        components: [
          { key: "panel", type: "panel", components: [{ key: "relationship" }] },
          { key: "spouseName", conditional: { when: "relationship", eq: "couple" } },
        ],
      }),
    ).toEqual([]);
  });

  it("finds duplicates across nesting levels", () => {
    expect(
      keywords(
        validateFormSpec({
          components: [
            { key: "panel", type: "panel", components: [{ key: "firstName" }] },
            { key: "firstName" },
          ],
        }),
      ),
    ).toEqual([FormSpecRule.ComponentKeyDuplicate]);
  });

  it("walks columns and table rows", () => {
    expect(
      validateFormSpec({
        components: [
          { key: "cols", columns: [{ components: [{ key: "inColumn" }] }] },
          { key: "grid", rows: [[{ components: [{ key: "inCell" }] }]] },
          { key: "dependent", conditional: { when: "inCell" } },
        ],
      }),
    ).toEqual([]);
  });
});

describe("isNewDocument", () => {
  it("is true for a genuine create and false for the publish-create", () => {
    // MEASURED: admin draft-save and the seeder's first create both send null.
    expect(isNewDocument({ documentId: null, version: 3 })).toBe(true);
    expect(isNewDocument({ version: 3 })).toBe(true);
    expect(isNewDocument({ documentId: DOC, version: 3 })).toBe(false);
  });
});

describe("assertVersionSequence", () => {
  it("requires version 1 for the first entry of a form", () => {
    expect(assertVersionSequence({ formSpecId: "new-form", version: 1 }, [])).toEqual([]);
    expect(
      keywords(assertVersionSequence({ formSpecId: "new-form", version: 2 }, [])),
    ).toEqual([FormSpecRule.VersionNotNext]);
  });

  it("requires max + 1, counting draft and published rows of the same version once", () => {
    const siblings = [
      draftRow({ id: 1, version: 1, documentId: "a" }),
      publishedRow({ id: 2, version: 1, documentId: "a" }),
      draftRow({ id: 3, version: 2, documentId: "b" }),
      publishedRow({ id: 4, version: 2, documentId: "b" }),
    ];
    expect(assertVersionSequence({ formSpecId: "poc-test-form", version: 3 }, siblings)).toEqual(
      [],
    );
    expect(
      keywords(assertVersionSequence({ formSpecId: "poc-test-form", version: 2 }, siblings)),
    ).toEqual([FormSpecRule.VersionNotNext]);
    expect(
      keywords(assertVersionSequence({ formSpecId: "poc-test-form", version: 4 }, siblings)),
    ).toEqual([FormSpecRule.VersionNotNext]);
  });

  it("rejects a non-integer, zero or negative version", () => {
    for (const version of [0, -1, 1.5, Number.NaN]) {
      expect(keywords(assertVersionSequence({ version }, []))).toEqual([
        FormSpecRule.VersionNotNext,
      ]);
    }
  });

  it("says what the version should have been", () => {
    const violations = assertVersionSequence({ formSpecId: "poc-test-form", version: 9 }, [
      publishedRow({ version: 2 }),
    ]);
    expect(violations[0].message).toContain("must be 3");
    expect(violations[0].message).toContain("Received 9");
  });
});

describe("canonicalJson", () => {
  it("ignores key order", () => {
    expect(canonicalJson({ a: 1, b: 2 })).toBe(canonicalJson({ b: 2, a: 1 }));
  });

  it("treats the string and object forms of the same spec as equal", () => {
    expect(canonicalJson(JSON.stringify(minimalSpec))).toBe(canonicalJson(minimalSpec));
  });

  it("does not ignore array order", () => {
    expect(canonicalJson({ components: [{ key: "a" }, { key: "b" }] })).not.toBe(
      canonicalJson({ components: [{ key: "b" }, { key: "a" }] }),
    );
  });
});

describe("findPublishedSibling", () => {
  const siblings = [draftRow(), publishedRow(), publishedRow({ id: 99, documentId: "other" })];

  it("finds the published row of this document only", () => {
    expect(findPublishedSibling(DOC, siblings)?.id).toBe(8);
    expect(findPublishedSibling("other", siblings)?.id).toBe(99);
  });

  it("returns undefined for a document that has never been published", () => {
    expect(findPublishedSibling(DOC, [draftRow()])).toBeUndefined();
    expect(findPublishedSibling(null, siblings)).toBeUndefined();
    expect(findPublishedSibling(undefined, siblings)).toBeUndefined();
  });
});

describe("checkImmutability", () => {
  it("allows a title change", () => {
    expect(checkImmutability({ title: "Renamed", spec: minimalSpec }, publishedRow())).toEqual([]);
  });

  it("allows the publish flow, where the draft matches what is published", () => {
    // MEASURED: publishing fires beforeUpdate on the draft carrying the whole
    // entry. Those values equal the published ones, so this must pass.
    expect(
      checkImmutability(
        { documentId: DOC, formSpecId: "poc-test-form", version: 3, spec: minimalSpec },
        publishedRow(),
      ),
    ).toEqual([]);
  });

  it("allows a reserialised spec that differs only in key order", () => {
    expect(
      checkImmutability(
        { spec: { components: minimalSpec.components, display: "form" } },
        publishedRow(),
      ),
    ).toEqual([]);
  });

  it("rejects a spec change and names the next version", () => {
    const violations = checkImmutability(
      { spec: { display: "form", components: [{ key: "changed" }] } },
      publishedRow(),
    );
    expect(keywords(violations)).toEqual([FormSpecRule.PublishedImmutable]);
    expect(violations[0].message).toContain("version 4");
  });

  it("rejects changing the version or the form id of a published entry", () => {
    expect(keywords(checkImmutability({ version: 4 }, publishedRow()))).toEqual([
      FormSpecRule.PublishedImmutable,
    ]);
    expect(keywords(checkImmutability({ formSpecId: "other-form" }, publishedRow()))).toEqual([
      FormSpecRule.PublishedImmutable,
    ]);
  });
});

describe("evaluateCreate", () => {
  it("accepts a first draft of a brand new form", () => {
    expect(
      evaluateCreate({ documentId: null, formSpecId: "new-form", version: 1, spec: minimalSpec }, []),
    ).toEqual([]);
  });

  it("rejects a new draft whose version skips ahead", () => {
    expect(
      keywords(
        evaluateCreate(
          { documentId: null, formSpecId: "poc-test-form", version: 5, spec: minimalSpec },
          [publishedRow({ version: 2 })],
        ),
      ),
    ).toEqual([FormSpecRule.VersionNotNext]);
  });

  it("REGRESSION: does not version-check the publish-create", () => {
    // The whole point. Publishing fires beforeCreate with the documentId set
    // while the draft row already holds that version. Version-checking here
    // rejects a valid publish — and, because the seeder calls
    // documents().create({status:"published"}), fails the boot on a fresh DB.
    expect(
      evaluateCreate(
        {
          documentId: DOC,
          formSpecId: "poc-test-form",
          version: 3,
          publishedAt: "2026-08-17T22:00:14.188Z",
          spec: minimalSpec,
        },
        [draftRow({ version: 3 })],
      ),
    ).toEqual([]);
  });

  it("REGRESSION: accepts the seeder's two-fire create-as-published sequence", () => {
    // MEASURED on a fresh seed of poc-test-form v2: fire 1 documentId null,
    // fire 2 documentId set. Both must pass with v1 already present.
    const existing = [
      draftRow({ id: 1, version: 1, documentId: "v1doc" }),
      publishedRow({ id: 2, version: 1, documentId: "v1doc" }),
    ];
    const fireOne = { documentId: null, formSpecId: "poc-test-form", version: 2, spec: minimalSpec };
    expect(evaluateCreate(fireOne, existing)).toEqual([]);

    const afterFireOne = [...existing, draftRow({ id: 10, version: 2, documentId: "v2doc" })];
    const fireTwo = {
      documentId: "v2doc",
      formSpecId: "poc-test-form",
      version: 2,
      publishedAt: "2026-08-17T22:09:00.686Z",
      spec: minimalSpec,
    };
    expect(evaluateCreate(fireTwo, afterFireOne)).toEqual([]);
  });

  it("still validates the spec on a publish-create", () => {
    expect(
      keywords(
        evaluateCreate({ documentId: DOC, version: 3, spec: { components: [] } }, [
          draftRow({ version: 3 }),
        ]),
      ),
    ).toEqual([FormSpecRule.ComponentsEmpty]);
  });

  it("reports a bad spec and a bad version together", () => {
    const violations = evaluateCreate(
      { documentId: null, formSpecId: "poc-test-form", version: 7, spec: "{ not json" },
      [publishedRow({ version: 1 })],
    );
    expect(keywords(violations)).toEqual([
      FormSpecRule.SpecUnparseable,
      FormSpecRule.VersionNotNext,
    ]);
  });
});

describe("evaluateUpdate", () => {
  it("allows editing a draft that has never been published", () => {
    expect(
      evaluateUpdate({ documentId: DOC, version: 3, spec: minimalSpec }, [draftRow()]),
    ).toEqual([]);
  });

  it("REGRESSION: allows the beforeUpdate that publishing fires on the draft", () => {
    // MEASURED: publish fires beforeUpdate on the draft first, carrying the
    // whole entry. Blocking it would make Publish impossible.
    expect(
      evaluateUpdate(
        {
          documentId: DOC,
          formSpecId: "poc-test-form",
          version: 3,
          publishedAt: null,
          spec: minimalSpec,
        },
        [draftRow(), publishedRow()],
      ),
    ).toEqual([]);
  });

  it("rejects changing the spec of a document whose version is published", () => {
    expect(
      keywords(
        evaluateUpdate({ documentId: DOC, version: 3, spec: { components: [{ key: "new" }] } }, [
          draftRow(),
          publishedRow(),
        ]),
      ),
    ).toEqual([FormSpecRule.PublishedImmutable]);
  });

  it("allows renaming the title of a published document", () => {
    expect(
      evaluateUpdate({ documentId: DOC, title: "Renamed", spec: minimalSpec }, [
        draftRow(),
        publishedRow(),
      ]),
    ).toEqual([]);
  });

  it("rejects an unparseable spec regardless of publish state", () => {
    expect(keywords(evaluateUpdate({ documentId: DOC, spec: "{{{" }, [draftRow()]))).toEqual([
      FormSpecRule.SpecUnparseable,
    ]);
  });
});

describe("formatViolations", () => {
  it("joins messages into one admin-readable line", () => {
    const violations = evaluateCreate(
      { documentId: null, formSpecId: "f", version: 9, spec: { components: [] } },
      [],
    );
    const text = formatViolations(violations);
    expect(text).toContain("no components");
    expect(text).toContain("must be 1");
  });

  it("is empty when nothing is wrong", () => {
    expect(formatViolations([])).toBe("");
  });
});
