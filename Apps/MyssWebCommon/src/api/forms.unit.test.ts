import { describe, expect, it } from "vitest";

import { readValidationErrors } from "./forms";

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("readValidationErrors", () => {
  it("returns every well-formed error in the payload", async () => {
    const errors = await readValidationErrors(
      jsonResponse(422, {
        payload: [
          {
            field: "sin",
            keyword: "IDA.SIN.INVALID_CHECKSUM",
            message: "Check the SIN.",
          },
          {
            field: "dob",
            keyword: "IDA.DOB.FUTURE",
            message: "Date is in the future.",
          },
        ],
      }),
    );

    expect(errors.map((error) => error.keyword)).toEqual([
      "IDA.SIN.INVALID_CHECKSUM",
      "IDA.DOB.FUTURE",
    ]);
  });

  it("drops entries that are not the expected shape", async () => {
    const errors = await readValidationErrors(
      jsonResponse(422, {
        payload: [
          {
            field: "sin",
            keyword: "IDA.SIN.INVALID_CHECKSUM",
            message: "Check the SIN.",
          },
          { field: "sin" },
          "not an error",
          null,
        ],
      }),
    );

    expect(errors).toHaveLength(1);
  });

  it("returns nothing when the payload is not a list", async () => {
    expect(
      await readValidationErrors(jsonResponse(400, { payload: {} })),
    ).toEqual([]);
    expect(await readValidationErrors(jsonResponse(400, null))).toEqual([]);
  });

  it("returns nothing when the body is not JSON", async () => {
    expect(
      await readValidationErrors(new Response("Bad gateway", { status: 502 })),
    ).toEqual([]);
  });
});
