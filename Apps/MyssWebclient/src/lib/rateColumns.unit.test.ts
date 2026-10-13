import { describe, expect, it } from "vitest";

import {
  ASSET_COLUMNS,
  COUPLE_COLUMNS,
  FAMILY_SIZES,
  INCOME_COLUMN_GROUPS,
  INCOME_COLUMNS,
  isNotApplicable,
} from "@/lib/rateColumns";

describe("rate columns", () => {
  it("lists the nine income letters in display order", () => {
    expect(INCOME_COLUMNS.map((column) => column.letter)).toEqual([
      "a", "b", "c", "d", "e", "f", "g", "h", "i",
    ]);
  });

  it("marks a, c, d, f, h and i as couple columns", () => {
    expect(COUPLE_COLUMNS).toEqual(["a", "c", "d", "f", "h", "i"]);
  });

  it("groups the income columns as 2, 3 and 4 adjacent columns", () => {
    expect(INCOME_COLUMN_GROUPS).toEqual([
      { group: "Income Assistance", span: 2 },
      { group: "Income Assistance 65+", span: 3 },
      { group: "Disability Assistance", span: 4 },
    ]);
  });

  it("covers family sizes 1 to 7", () => {
    expect(FAMILY_SIZES).toEqual([1, 2, 3, 4, 5, 6, 7]);
  });

  it("lists the four asset letters", () => {
    expect(ASSET_COLUMNS).toEqual(["a", "b", "c", "d"]);
  });

  it("treats only couple columns at family size 1 as not applicable", () => {
    expect(isNotApplicable(1, "a")).toBe(true);
    expect(isNotApplicable(1, "i")).toBe(true);
    expect(isNotApplicable(1, "b")).toBe(false);
    expect(isNotApplicable(1, "e")).toBe(false);
    expect(isNotApplicable(2, "a")).toBe(false);
  });
});
