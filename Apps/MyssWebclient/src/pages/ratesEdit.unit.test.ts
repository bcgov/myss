import { describe, expect, it } from "vitest";

import type { EligibilityRates } from "@/api/eligibility";
import {
  applyDraft,
  BLANK_MESSAGE,
  cellLabel,
  editableCellIds,
  hasChanges,
  inlineMessage,
  INVALID_MESSAGE,
  isCompleteTable,
  isEditableCell,
  TOO_LARGE_MESSAGE,
  parseAmount,
  ratesDraft,
  validateDraft,
} from "./ratesEdit";

// The seeded table.
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

describe("parseAmount", () => {
  it.each([
    ["0", 0],
    ["1060", 1060],
    ["1535.5", 1535.5],
    ["1535.50", 1535.5],
    [" 1060 ", 1060],
  ])("accepts %j", (text, expected) => {
    expect(parseAmount(text)).toBe(expected);
  });

  it("accepts up to 9 whole-dollar digits", () => {
    expect(parseAmount("999999999.99")).toBe(999999999.99);
    expect(parseAmount("1000000000")).toBeNull();
  });

  it.each(["aaa", "", "   ", "-5", "1,060", "$1060", "1e3", "1535.555", "12abc", ".5", "5."])(
    "refuses %j",
    (text) => {
      expect(parseAmount(text)).toBeNull();
    },
  );
});

describe("ratesDraft", () => {
  it("holds 57 income cells and 4 asset cells, with no N/A cells", () => {
    const draft = ratesDraft(RATES);
    const ids = Object.keys(draft);

    expect(ids.filter((id) => id.startsWith("incomeRows."))).toHaveLength(57);
    expect(ids.filter((id) => id.startsWith("assetLimits."))).toHaveLength(4);
    expect(draft["incomeRows.1.a"]).toBeUndefined();
    expect(draft["incomeRows.1.e"]).toBe("1360");
    expect(draft["incomeRows.2.i"]).toBe("2590.5");
    expect(draft["assetLimits.b"]).toBe("10000");
  });

  it("lists its cells in table order", () => {
    const ids = editableCellIds();

    expect(ids.slice(0, 3)).toEqual(["incomeRows.1.b", "incomeRows.1.e", "incomeRows.1.g"]);
    expect(ids[3]).toBe("incomeRows.2.a");
    expect(ids.slice(-4)).toEqual(["assetLimits.a", "assetLimits.b", "assetLimits.c", "assetLimits.d"]);
  });
});

describe("validateDraft", () => {
  it("accepts the unedited draft", () => {
    expect(validateDraft(ratesDraft(RATES))).toEqual({});
  });

  it("asks for an amount in a blank cell and refuses anything else that is not one", () => {
    const draft = {
      ...ratesDraft(RATES),
      "incomeRows.2.b": "  ",
      "incomeRows.3.c": "aaa",
      "assetLimits.c": "1e3",
    };

    expect(validateDraft(draft)).toEqual({
      "incomeRows.2.b": BLANK_MESSAGE,
      "incomeRows.3.c": INVALID_MESSAGE,
      "assetLimits.c": INVALID_MESSAGE,
    });
  });

  it("names an amount that is only too long", () => {
    const draft = { ...ratesDraft(RATES), "assetLimits.d": "12345678901234567890" };

    expect(validateDraft(draft)).toEqual({ "assetLimits.d": TOO_LARGE_MESSAGE });
  });

  it("treats a missing cell as blank", () => {
    const draft: Record<string, string> = { ...ratesDraft(RATES) };
    delete draft["incomeRows.7.i"];

    expect(validateDraft(draft)).toEqual({ "incomeRows.7.i": BLANK_MESSAGE });
  });
});

describe("applyDraft", () => {
  it("builds the complete table, with N/A cells as 0", () => {
    const body = applyDraft(ratesDraft(RATES));

    expect(body.incomeRows).toEqual(RATES.incomeRows);
    expect(body.assetLimits).toEqual(RATES.assetLimits);
  });

  it("changes only the edited income cell, leaving the asset limits as they were", () => {
    const body = applyDraft({ ...ratesDraft(RATES), "incomeRows.2.e": "1710" });

    expect(body.incomeRows[1].e).toBe(1710);
    expect(body.incomeRows[1].d).toBe(1950);
    expect(body.assetLimits).toEqual(RATES.assetLimits);
  });

  it("changes only the edited asset limit, leaving the income rows as they were", () => {
    const body = applyDraft({ ...ratesDraft(RATES), "assetLimits.b": "8000" });

    expect(body.assetLimits).toEqual({ a: 5000, b: 8000, c: 100000, d: 200000 });
    expect(body.incomeRows).toEqual(RATES.incomeRows);
  });

  it("refuses a draft that did not pass validation", () => {
    expect(() => applyDraft({ ...ratesDraft(RATES), "incomeRows.2.b": "aaa" })).toThrow(
      "incomeRows.2.b",
    );
  });
});

describe("hasChanges", () => {
  it("is false for the unedited draft and for the same amounts written differently", () => {
    expect(hasChanges(RATES, ratesDraft(RATES))).toBe(false);
    expect(
      hasChanges(RATES, { ...ratesDraft(RATES), "incomeRows.1.b": "1060.00", "incomeRows.1.g": " 1535.50 " }),
    ).toBe(false);
  });

  it("is true for a different amount or text that is not an amount", () => {
    expect(hasChanges(RATES, { ...ratesDraft(RATES), "assetLimits.d": "200001" })).toBe(true);
    expect(hasChanges(RATES, { ...ratesDraft(RATES), "incomeRows.4.a": "aaa" })).toBe(true);
  });
});

describe("inlineMessage", () => {
  it("gives a short text for each problem, and a general one for anything else", () => {
    expect(inlineMessage(BLANK_MESSAGE)).toBe("Enter an amount.");
    expect(inlineMessage(INVALID_MESSAGE)).toBe("Use digits, up to 2 decimals.");
    expect(inlineMessage(TOO_LARGE_MESSAGE)).toBe("Too large.");
    expect(inlineMessage("Family size 2, column B must be 0 or more, not -1.")).toBe("Check this amount.");
  });
});

describe("isCompleteTable", () => {
  it("needs the rows 1 to 7, once each, in order", () => {
    expect(isCompleteTable(RATES)).toBe(true);
    expect(isCompleteTable({ ...RATES, incomeRows: RATES.incomeRows.slice(0, 6) })).toBe(false);
    expect(isCompleteTable({ ...RATES, incomeRows: [...RATES.incomeRows].reverse() })).toBe(false);
    expect(
      isCompleteTable({ ...RATES, incomeRows: [...RATES.incomeRows.slice(0, 6), { ...RATES.incomeRows[6], familySize: 8 }] }),
    ).toBe(false);
  });
});

describe("isEditableCell", () => {
  it("knows the editable cells and nothing else", () => {
    expect(isEditableCell("incomeRows.2.a")).toBe(true);
    expect(isEditableCell("assetLimits.d")).toBe(true);
    expect(isEditableCell("incomeRows.1.a")).toBe(false); // N/A
    expect(isEditableCell("incomeRows.8.b")).toBe(false);
    expect(isEditableCell("incomeRows")).toBe(false);
    expect(isEditableCell("rates")).toBe(false);
  });
});

describe("cellLabel", () => {
  it("names income cells by family size, category and group", () => {
    expect(cellLabel("incomeRows.3.c")).toBe("Family size 3, category C (Income Assistance 65+)");
    expect(cellLabel("incomeRows.1.b")).toBe("Family size 1, category B (Income Assistance)");
    expect(cellLabel("incomeRows.2.i")).toBe("Family size 2, category I (Disability Assistance)");
  });

  it("names asset cells by category", () => {
    expect(cellLabel("assetLimits.b")).toBe("Asset limit, category B");
  });

  it("returns any other field name as it is", () => {
    expect(cellLabel("rates")).toBe("rates");
    expect(cellLabel("incomeRows")).toBe("incomeRows");
  });
});
