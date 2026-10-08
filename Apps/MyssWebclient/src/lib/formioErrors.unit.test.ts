import { describe, expect, it } from "vitest";

import { toValidationErrors } from "@/lib/formioErrors";

describe("toValidationErrors", () => {
  it("maps Form.io's errors to the API's field, keyword and message shape", () => {
    expect(
      toValidationErrors([
        {
          message: "First name is required.",
          ruleName: "required",
          context: { path: "firstName" },
          component: { key: "firstName" },
        },
        {
          message: "That Social Insurance Number is not valid.",
          ruleName: "sin",
          keyword: "IDA.SIN.INVALID_CHECKSUM",
          path: "sin",
        },
      ]),
    ).toEqual([
      {
        field: "firstName",
        keyword: "FORM.CLIENT.REQUIRED",
        message: "First name is required.",
      },
      {
        field: "sin",
        keyword: "IDA.SIN.INVALID_CHECKSUM",
        message: "That Social Insurance Number is not valid.",
      },
    ]);
  });

  it("drops server errors Form.io is re-showing, and entries with no field or message", () => {
    expect(
      toValidationErrors([
        { message: "From the API", path: "sin", fromServer: true },
        { message: "", path: "firstName" },
        { message: "No field at all" },
        "not an object",
        null,
      ]),
    ).toEqual([]);
  });

  it("reports one entry per field and message", () => {
    expect(
      toValidationErrors([
        { message: "Required", path: "a", ruleName: "required" },
        { message: "Required", path: "a", ruleName: "required" },
        { message: "Too short", path: "a", ruleName: "minLength" },
      ]),
    ).toHaveLength(2);
  });

  it("reads a single error or nothing", () => {
    expect(toValidationErrors({ message: "One", path: "a" })).toHaveLength(1);
    expect(toValidationErrors(undefined)).toEqual([]);
    expect(toValidationErrors(false)).toEqual([]);
  });
});
