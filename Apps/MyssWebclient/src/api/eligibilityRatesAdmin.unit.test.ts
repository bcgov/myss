import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { RatesRejectedError, saveEstimatorRates } from "@/api/eligibilityRatesAdmin";
import type { SaveEstimatorRatesInput } from "@/api/eligibilityRatesAdmin";
import { setAccessToken } from "@/auth/accessToken";

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

const input: SaveEstimatorRatesInput = {
  incomeRows: [{ familySize: 1, a: 0, b: 1060, c: 0, d: 0, e: 1360, f: 0, g: 1535.5, h: 0, i: 0 }],
  assetLimits: { a: 5000, b: 10000, c: 100000, d: 200000 },
};

beforeEach(() => {
  setAccessToken("test-token");
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("saveEstimatorRates", () => {
  it("posts the table with the admin's token and returns the saved table", async () => {
    const saved = { effectiveDate: "2026-10-06", ...input };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, { payload: saved }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await saveEstimatorRates(input);

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toMatch(/\/v1\/EligibilityRates$/);
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer test-token");
    expect(JSON.parse(init.body as string)).toEqual(input);
    expect(result).toEqual(saved);
  });

  it("surfaces a 422 as RatesRejectedError with the errors", async () => {
    const errors = [{ field: "incomeRows.2.b", keyword: "RATES.AMOUNT.NEGATIVE", message: "Too low." }];
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(422, { payload: errors })));

    const error = await saveEstimatorRates(input).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(RatesRejectedError);
    expect((error as RatesRejectedError).status).toBe(422);
    expect((error as RatesRejectedError).errors).toEqual(errors);
  });

  it("surfaces any other failure as RatesRejectedError with no errors", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Bad Gateway", { status: 502 })));

    const error = await saveEstimatorRates(input).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(RatesRejectedError);
    expect((error as RatesRejectedError).status).toBe(502);
    expect((error as RatesRejectedError).errors).toEqual([]);
  });
});
