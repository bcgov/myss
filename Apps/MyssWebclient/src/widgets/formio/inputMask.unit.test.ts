import { describe, expect, it } from "vitest";

import { applyInputMask, isNumericMask } from "./inputMask";

describe("applyInputMask", () => {
  it("formats digits typed one at a time under a phone mask", () => {
    const mask = "(999) 999-9999";
    expect(applyInputMask("2", mask)).toBe("(2");
    expect(applyInputMask("250", mask)).toBe("(250");
    expect(applyInputMask("2505", mask)).toBe("(250) 5");
    expect(applyInputMask("2505550199", mask)).toBe("(250) 555-0199");
  });

  it("leaves an already formatted value as it is", () => {
    expect(applyInputMask("(250) 555-0199", "(999) 999-9999")).toBe(
      "(250) 555-0199",
    );
    expect(applyInputMask("046-454-286", "999-999-999")).toBe("046-454-286");
  });

  it("drops characters that fit no slot and stops at the mask's end", () => {
    expect(applyInputMask("25a0", "9999")).toBe("250");
    expect(applyInputMask("12345", "9999")).toBe("1234");
  });

  it("treats an optional group as a slot that may stay empty", () => {
    expect(applyInputMask("3", "9[9]")).toBe("3");
    expect(applyInputMask("31", "9[9]")).toBe("31");
  });

  it("accepts letters and alphanumerics where the mask asks for them", () => {
    expect(applyInputMask("v8v1x4", "a9a 9a9")).toBe("v8v 1x4");
    expect(applyInputMask("ab12", "****")).toBe("ab12");
  });

  it("returns the raw value when there is no mask", () => {
    expect(applyInputMask("anything", undefined)).toBe("anything");
    expect(applyInputMask("anything", "")).toBe("anything");
  });
});

describe("isNumericMask", () => {
  it("is true only when every slot is a digit", () => {
    expect(isNumericMask("(999) 999-9999")).toBe(true);
    expect(isNumericMask("9[9]")).toBe(true);
    expect(isNumericMask("a9a 9a9")).toBe(false);
    expect(isNumericMask("")).toBe(false);
    expect(isNumericMask(undefined)).toBe(false);
  });
});
