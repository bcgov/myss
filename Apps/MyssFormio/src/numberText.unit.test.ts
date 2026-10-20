import { describe, expect, it } from "vitest";

import {
  cleanNumberText,
  formatNumberValue,
  parseNumberText,
} from "./numberText";

describe("cleanNumberText", () => {
  it("accepts digits and a decimal point up to the limit", () => {
    expect(cleanNumberText("1250", 2)).toBe("1250");
    expect(cleanNumberText("1250.5", 2)).toBe("1250.5");
    expect(cleanNumberText("1250.55", 2)).toBe("1250.55");
  });

  it("refuses a keystroke the field cannot hold", () => {
    expect(cleanNumberText("1250.555", 2)).toBeNull();
    expect(cleanNumberText("12a", 2)).toBeNull();
    expect(cleanNumberText("1.2.3", 2)).toBeNull();
    expect(cleanNumberText("1.5", 0)).toBeNull();
  });

  it("drops grouping commas, spaces and a dollar sign", () => {
    expect(cleanNumberText("$1,250.00", 2)).toBe("1250.00");
    expect(cleanNumberText(" 3 000 ", 0)).toBe("3000");
  });

  it("allows a text on its way to a number", () => {
    expect(cleanNumberText("", 2)).toBe("");
    expect(cleanNumberText("-", 2)).toBe("-");
    expect(cleanNumberText("12.", 2)).toBe("12.");
  });
});

describe("parseNumberText", () => {
  it("reads the number a tidied text stands for", () => {
    expect(parseNumberText("1250.5")).toBe(1250.5);
    expect(parseNumberText("12.")).toBe(12);
    expect(parseNumberText("-.5")).toBe(-0.5);
  });

  it("holds no number until a digit is typed", () => {
    expect(parseNumberText("")).toBeNull();
    expect(parseNumberText("-")).toBeNull();
    expect(parseNumberText(".")).toBeNull();
  });
});

describe("formatNumberValue", () => {
  it("shows a stored number with at most the allowed decimals", () => {
    expect(formatNumberValue(1250.5, 2, false)).toBe("1250.5");
    expect(formatNumberValue(0, 2, false)).toBe("0");
    expect(formatNumberValue(3, 0, false)).toBe("3");
  });

  it("groups thousands when the form asks for it", () => {
    expect(formatNumberValue(1250000, 2, true)).toBe("1,250,000");
  });

  it("reads a numeric string as its number and shows nothing for no value", () => {
    expect(formatNumberValue("42", 0, false)).toBe("42");
    expect(formatNumberValue("", 2, false)).toBe("");
    expect(formatNumberValue(null, 2, false)).toBe("");
    expect(formatNumberValue(Number.NaN, 2, false)).toBe("");
  });
});
