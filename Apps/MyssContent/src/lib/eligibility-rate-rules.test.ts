import { describe, expect, it } from "vitest";

import {
  RateRule,
  evaluateRateCreate,
  evaluateRateUpdate,
  findDuplicateDate,
  formatViolations,
  validateAssetLimits,
  validateIncomeRows,
  type RateTableRow,
} from "./eligibility-rate-rules";

const keywords = (violations: { keyword: string }[]) => violations.map((v) => v.keyword);

type Row = Record<string, unknown>;

const nineColumnRows: Row[] = [
  { familySize: 1, a: 0, b: 1060, c: 0, d: 0, e: 1360, f: 0, g: 1535.5, h: 0, i: 0 },
  { familySize: 2, a: 1650, b: 1405, c: 2200, d: 1950, e: 1705, f: 2290.5, g: 1880.5, h: 2766, i: 2590.5 },
  { familySize: 3, a: 1845, b: 1500, c: 2395, d: 2145, e: 1800, f: 2485.5, g: 1975.5, h: 2961, i: 2785.5 },
  { familySize: 4, a: 1895, b: 1550, c: 2445, d: 2195, e: 1850, f: 2535.5, g: 2025.5, h: 3011, i: 2835.5 },
  { familySize: 5, a: 1945, b: 1600, c: 2495, d: 2245, e: 1900, f: 2585.5, g: 2075.5, h: 3061, i: 2885.5 },
  { familySize: 6, a: 1995, b: 1650, c: 2545, d: 2295, e: 1950, f: 2635.5, g: 2125.5, h: 3111, i: 2935.5 },
  { familySize: 7, a: 2045, b: 1700, c: 2595, d: 2345, e: 2000, f: 2685.5, g: 2175.5, h: 3161, i: 2985.5 },
];

// The table as it was stored before client types C, D, E and I were added.
const fiveColumnRows: Row[] = [
  { familySize: 1, a: 0, b: 1060, c: 0, d: 1535.5, e: 0 },
  { familySize: 2, a: 1650, b: 1405, c: 2290.5, d: 1880.5, e: 2766 },
  { familySize: 3, a: 1845, b: 1500, c: 2485.5, d: 1975.5, e: 2961 },
  { familySize: 4, a: 1895, b: 1550, c: 2535.5, d: 2025.5, e: 3011 },
  { familySize: 5, a: 1945, b: 1600, c: 2585.5, d: 2075.5, e: 3061 },
  { familySize: 6, a: 1995, b: 1650, c: 2635.5, d: 2125.5, e: 3111 },
  { familySize: 7, a: 2045, b: 1700, c: 2685.5, d: 2175.5, e: 3161 },
];

const assetLimits = { a: 5000, b: 10000, c: 100000, d: 200000 };

/** The nine-column rows with one row's columns patched (a column set to undefined is removed). */
function withRow(familySize: number, patch: Row): Row[] {
  return nineColumnRows.map((row) => {
    if (row.familySize !== familySize) return row;
    const next: Row = { ...row, ...patch };
    for (const [key, value] of Object.entries(patch)) {
      if (value === undefined) delete next[key];
    }
    return next;
  });
}

const DOC = "rate-doc-current";
const OTHER_DOC = "rate-doc-other";
const DATE = "2026-10-02";

const draftRow = (documentId: string): RateTableRow => ({ id: 1, documentId, publishedAt: null });
const publishedRow = (documentId: string): RateTableRow => ({
  id: 2,
  documentId,
  publishedAt: "2026-10-02T17:00:00.000Z",
});

describe("validateIncomeRows", () => {
  it("accepts a complete nine-column table", () => {
    expect(validateIncomeRows(nineColumnRows)).toEqual([]);
  });

  it("accepts the JSON string the admin panel sends", () => {
    expect(validateIncomeRows(JSON.stringify(nineColumnRows))).toEqual([]);
  });

  it("refuses the five-column table, naming each missing column once", () => {
    const violations = validateIncomeRows(fiveColumnRows);
    // Its family-size-1 single-PWD amount sits in D, which is now a couple column.
    expect(keywords(violations)).toEqual([
      RateRule.ColumnMissing,
      RateRule.ColumnMissing,
      RateRule.ColumnMissing,
      RateRule.ColumnMissing,
      RateRule.NotApplicableNotZero,
    ]);
    expect(violations[0].message).toBe(
      "Column F is missing for family size 1, family size 2, family size 3, family size 4, " +
        "family size 5, family size 6, family size 7.",
    );
    expect(formatViolations(violations)).toContain("Column I is missing");
  });

  it("refuses a row missing one column", () => {
    const violations = validateIncomeRows(withRow(4, { i: undefined }));
    expect(keywords(violations)).toEqual([RateRule.ColumnMissing]);
    expect(violations[0].message).toBe("Column I is missing for family size 4.");
  });

  it("refuses an unknown column", () => {
    const violations = validateIncomeRows(withRow(2, { j: 100 }));
    expect(keywords(violations)).toEqual([RateRule.ColumnUnknown]);
    expect(violations[0].message).toContain('"j" for family size 2');
  });

  it("refuses a value in a couple column at family size 1", () => {
    const violations = validateIncomeRows(withRow(1, { a: 1650, c: 1 }));
    expect(keywords(violations)).toEqual([
      RateRule.NotApplicableNotZero,
      RateRule.NotApplicableNotZero,
    ]);
    expect(violations[0].message).toContain("Family size 1, column A must be 0");
  });

  it("allows 0 in a single column at family size 1", () => {
    expect(validateIncomeRows(withRow(1, { b: 0, e: 0, g: 0 }))).toEqual([]);
  });

  it.each([
    ["aaa", RateRule.AmountNotANumber],
    ["1060", RateRule.AmountNotANumber],
    [null, RateRule.AmountNotANumber],
    [-5, RateRule.AmountNegative],
    [1535.555, RateRule.AmountTooPrecise],
    [0.1 + 0.2, RateRule.AmountTooPrecise],
    [1060.000000001, RateRule.AmountTooPrecise],
    [1e-9, RateRule.AmountTooPrecise],
  ])("refuses %j as an amount", (value, keyword) => {
    const violations = validateIncomeRows(withRow(2, { b: value }));
    expect(keywords(violations)).toEqual([keyword]);
    expect(violations[0].message).toContain("Family size 2, column B must");
  });

  it("names the typed value in the message", () => {
    const [problem] = validateIncomeRows(withRow(1, { b: "aaa" }));
    expect(problem.message).toBe('Family size 1, column B must be a number, not the text "aaa".');
  });

  it("names an infinite value rather than calling it null", () => {
    const text = JSON.stringify(withRow(2, { b: 999 })).replace('"b":999', '"b":1e400');
    const [problem] = validateIncomeRows(text);
    expect(problem.keyword).toBe(RateRule.AmountNotANumber);
    expect(problem.message).toBe("Family size 2, column B must be a number, not Infinity.");
  });

  it.each([0, 0.29, 1535.5, 1535.55, 100000.01, 146311612.02])("accepts %s as an amount", (value) => {
    expect(validateIncomeRows(withRow(3, { b: value }))).toEqual([]);
  });

  it("reports an invalid value in a couple column at family size 1 once", () => {
    expect(keywords(validateIncomeRows(withRow(1, { a: "x" })))).toEqual([
      RateRule.AmountNotANumber,
    ]);
  });

  it("labels a row by position when its family size is not a number", () => {
    const violations = validateIncomeRows(withRow(2, { familySize: "2", i: undefined }));
    expect(violations.find((v) => v.keyword === RateRule.ColumnMissing)?.message).toBe(
      "Column I is missing for row 2.",
    );
  });

  it("treats own properties named like built-ins as unknown columns", () => {
    expect(keywords(validateIncomeRows(withRow(3, { constructor: 1 })))).toEqual([
      RateRule.ColumnUnknown,
    ]);
    const text = JSON.stringify(nineColumnRows).replace(
      '{"familySize":2,',
      '{"familySize":2,"__proto__":1,',
    );
    expect(keywords(validateIncomeRows(text))).toEqual([RateRule.ColumnUnknown]);
  });

  it("refuses six rows", () => {
    const violations = validateIncomeRows(nineColumnRows.slice(0, 6));
    expect(keywords(violations)).toEqual([RateRule.FamilySizesInvalid]);
  });

  it("refuses a duplicated family size", () => {
    const rows = nineColumnRows.map((row) => (row.familySize === 7 ? { ...row, familySize: 6 } : row));
    expect(keywords(validateIncomeRows(rows))).toContain(RateRule.FamilySizesInvalid);
  });

  it("refuses family size 8 and a family size written as text", () => {
    expect(keywords(validateIncomeRows(withRow(7, { familySize: 8 })))).toContain(
      RateRule.FamilySizesInvalid,
    );
    expect(keywords(validateIncomeRows(withRow(2, { familySize: "2" })))).toContain(
      RateRule.FamilySizesInvalid,
    );
  });

  it("refuses a value that is not a list of rows", () => {
    expect(keywords(validateIncomeRows({ familySize: 1 }))).toEqual([RateRule.ShapeInvalid]);
    expect(keywords(validateIncomeRows(null))).toEqual([RateRule.ShapeInvalid]);
    expect(keywords(validateIncomeRows([...nineColumnRows.slice(0, 6), "row"]))).toEqual([
      RateRule.ShapeInvalid,
    ]);
  });

  it("reports invalid JSON rather than throwing", () => {
    expect(keywords(validateIncomeRows("{ not json"))).toEqual([RateRule.JsonUnparseable]);
  });
});

describe("validateAssetLimits", () => {
  it("accepts the four limits, as an object or as a JSON string", () => {
    expect(validateAssetLimits(assetLimits)).toEqual([]);
    expect(validateAssetLimits(JSON.stringify(assetLimits))).toEqual([]);
  });

  it("refuses a missing and an unknown column", () => {
    expect(keywords(validateAssetLimits({ a: 5000, b: 10000, c: 100000 }))).toEqual([
      RateRule.ColumnMissing,
    ]);
    expect(keywords(validateAssetLimits({ ...assetLimits, e: 1 }))).toEqual([
      RateRule.ColumnUnknown,
    ]);
  });

  it("refuses invalid amounts and names the column", () => {
    const [problem] = validateAssetLimits({ ...assetLimits, b: "aaa" });
    expect(problem.keyword).toBe(RateRule.AmountNotANumber);
    expect(problem.message).toBe('Asset limit B must be a number, not the text "aaa".');
    expect(keywords(validateAssetLimits({ ...assetLimits, c: -1 }))).toEqual([
      RateRule.AmountNegative,
    ]);
    expect(keywords(validateAssetLimits({ ...assetLimits, a: 5000.001 }))).toEqual([
      RateRule.AmountTooPrecise,
    ]);
  });

  it("refuses a value that is not an object", () => {
    expect(keywords(validateAssetLimits([5000]))).toEqual([RateRule.ShapeInvalid]);
    expect(keywords(validateAssetLimits(null))).toEqual([RateRule.ShapeInvalid]);
    expect(keywords(validateAssetLimits("{ not json"))).toEqual([RateRule.JsonUnparseable]);
  });
});

describe("findDuplicateDate", () => {
  it("is a duplicate when another document has the same date", () => {
    const violations = findDuplicateDate({ documentId: null, effectiveDate: DATE }, [
      publishedRow(OTHER_DOC),
    ]);
    expect(keywords(violations)).toEqual([RateRule.EffectiveDateDuplicate]);
    expect(violations[0].message).toContain(`A rate table dated ${DATE} already exists`);
  });

  it("ignores the rows of the same document", () => {
    expect(
      findDuplicateDate({ documentId: DOC, effectiveDate: DATE }, [draftRow(DOC), publishedRow(DOC)]),
    ).toEqual([]);
  });

  it("checks nothing without an effective date", () => {
    expect(findDuplicateDate({ documentId: null }, [publishedRow(OTHER_DOC)])).toEqual([]);
  });
});

describe("evaluateRateCreate", () => {
  const table = { effectiveDate: DATE, incomeRows: nineColumnRows, assetLimits };

  it("accepts a new table with a free date", () => {
    expect(evaluateRateCreate({ ...table, documentId: null }, [])).toEqual([]);
  });

  it("refuses a new table whose date another table already has", () => {
    expect(
      keywords(evaluateRateCreate({ ...table, documentId: null }, [draftRow(OTHER_DOC)])),
    ).toEqual([RateRule.EffectiveDateDuplicate]);
  });

  it("treats a create without a documentId as a new table", () => {
    expect(keywords(evaluateRateCreate(table, [draftRow(OTHER_DOC)]))).toEqual([
      RateRule.EffectiveDateDuplicate,
    ]);
  });

  it("accepts the published copy a publish creates for the same document", () => {
    expect(evaluateRateCreate({ ...table, documentId: DOC }, [draftRow(DOC)])).toEqual([]);
  });

  it("reports value problems and a duplicate together", () => {
    const data = { ...table, documentId: null, incomeRows: withRow(1, { b: "aaa" }) };
    expect(keywords(evaluateRateCreate(data, [publishedRow(OTHER_DOC)]))).toEqual([
      RateRule.AmountNotANumber,
      RateRule.EffectiveDateDuplicate,
    ]);
  });

  it("checks only the fields present", () => {
    expect(evaluateRateCreate({}, [])).toEqual([]);
  });
});

describe("evaluateRateUpdate", () => {
  it("accepts a status-only publish", () => {
    expect(evaluateRateUpdate({ documentId: DOC }, [])).toEqual([]);
  });

  it("refuses an edit that breaks the rules", () => {
    expect(keywords(evaluateRateUpdate({ documentId: DOC, assetLimits: { ...assetLimits, b: -1 } }, []))).toEqual(
      [RateRule.AmountNegative],
    );
  });

  it("accepts an edit that keeps the table's own date", () => {
    const data = { documentId: DOC, effectiveDate: DATE, incomeRows: nineColumnRows, assetLimits };
    expect(evaluateRateUpdate(data, [draftRow(DOC), publishedRow(DOC)])).toEqual([]);
  });

  it("refuses moving a table onto another table's date", () => {
    const data = { documentId: DOC, effectiveDate: DATE };
    expect(keywords(evaluateRateUpdate(data, [draftRow(DOC), draftRow(OTHER_DOC)]))).toEqual([
      RateRule.EffectiveDateDuplicate,
    ]);
  });

  it("skips the duplicate check when the update carries no documentId", () => {
    expect(evaluateRateUpdate({ effectiveDate: DATE }, [draftRow(OTHER_DOC)])).toEqual([]);
  });
});

describe("formatViolations", () => {
  it("joins the messages", () => {
    const violations = validateIncomeRows(withRow(2, { b: -1, c: -2 }));
    expect(formatViolations(violations)).toBe(
      "Family size 2, column B must be 0 or more, not -1. " +
        "Family size 2, column C must be 0 or more, not -2.",
    );
  });

  it("lists at most ten problems and counts the rest", () => {
    const quoted = nineColumnRows.map((row) =>
      Object.fromEntries(Object.entries(row).map(([k, v]) => [k, k === "familySize" ? v : String(v)])),
    );
    const violations = validateIncomeRows(quoted);
    expect(violations).toHaveLength(63);
    const message = formatViolations(violations);
    expect(message.match(/must be a number/g)).toHaveLength(10);
    expect(message.endsWith("…and 53 more problems.")).toBe(true);
  });
});
