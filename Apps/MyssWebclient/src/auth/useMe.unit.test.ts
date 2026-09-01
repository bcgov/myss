import { describe, it, expect } from "vitest";

import { HttpError } from "@/api/me";

import { ME_QUERY_KEY, meQueryKey, shouldRetryMe } from "./useMe";

describe("shouldRetryMe", () => {
  it("never retries a 4xx — an expired or misconfigured token is not transient", () => {
    expect(shouldRetryMe(0, new HttpError(401, "unauthorized"))).toBe(false);
    expect(shouldRetryMe(0, new HttpError(403, "forbidden"))).toBe(false);
  });

  it("retries network errors and 5xx, at most twice", () => {
    expect(shouldRetryMe(0, new TypeError("failed to fetch"))).toBe(true);
    expect(shouldRetryMe(1, new HttpError(500, "boom"))).toBe(true);
    expect(shouldRetryMe(2, new TypeError("failed to fetch"))).toBe(false);
    expect(shouldRetryMe(2, new HttpError(503, "unavailable"))).toBe(false);
  });
});

describe("meQueryKey", () => {
  it("scopes the key by subject so cached roles can never cross users", () => {
    expect(meQueryKey("u1")).toEqual(["auth", "me", "u1"]);
    expect(meQueryKey("u1")).not.toEqual(meQueryKey("u2"));
  });

  it("keeps ME_QUERY_KEY as the invalidation prefix", () => {
    expect(meQueryKey("u1").slice(0, ME_QUERY_KEY.length)).toEqual([
      ...ME_QUERY_KEY,
    ]);
  });

  it("falls back to a fixed slot when the subject is missing", () => {
    expect(meQueryKey(undefined)).toEqual(["auth", "me", "anonymous"]);
  });
});
