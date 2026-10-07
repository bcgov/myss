import { describe, expect, it } from "vitest";

import { errorMessageFor, parseCatalogue } from "@/api/errorMessages";

// Pure helpers. No network: the fetch is exercised against the stubbed API in
// the bus pass form's browser test.

describe("parseCatalogue", () => {
  it("keeps string-to-string entries and drops everything else", () => {
    expect(
      parseCatalogue({
        "BUSPASS.SUBMIT.REJECTED": "Declined.",
        "BLANK.ROW": "   ",
        "NOT.A.STRING": 42,
        "AN.OBJECT": { message: "nested" },
        "A.NULL": null,
      }),
    ).toEqual({ "BUSPASS.SUBMIT.REJECTED": "Declined." });
  });

  it("reads anything that is not an object as an empty catalogue", () => {
    expect(parseCatalogue(undefined)).toEqual({});
    expect(parseCatalogue(null)).toEqual({});
    expect(parseCatalogue("text")).toEqual({});
    expect(parseCatalogue(["BUSPASS.SUBMIT.REJECTED"])).toEqual({});
  });
});

describe("errorMessageFor", () => {
  const catalogue = { "BUSPASS.SUBMIT.REJECTED": "Authored wording." };

  it("returns the catalogue's wording for a keyword it has", () => {
    expect(
      errorMessageFor(catalogue, "BUSPASS.SUBMIT.REJECTED", "fallback"),
    ).toBe("Authored wording.");
  });

  it("falls back when the keyword has no row", () => {
    expect(
      errorMessageFor(catalogue, "BUSPASS.SUBMIT.RATE_LIMITED", "fallback"),
    ).toBe("fallback");
  });

  it("falls back when the catalogue has not loaded", () => {
    expect(
      errorMessageFor(undefined, "BUSPASS.SUBMIT.REJECTED", "fallback"),
    ).toBe("fallback");
  });

  it("falls back when there is no keyword to look up", () => {
    expect(errorMessageFor(catalogue, null, "fallback")).toBe("fallback");
    expect(errorMessageFor(catalogue, undefined, "fallback")).toBe("fallback");
    expect(errorMessageFor(catalogue, "", "fallback")).toBe("fallback");
  });
});
