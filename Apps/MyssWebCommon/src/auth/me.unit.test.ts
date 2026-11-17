import { describe, it, expect, vi, afterEach } from "vitest";

import {
  fetchMe,
  HttpError,
  ME_QUERY_KEY,
  meQueryKey,
  shouldRetryMe,
  type MePayloadBase,
} from "./me";

const API_URL = "http://localhost:5000";

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchMe", () => {
  it("unwraps the payload envelope", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(200, {
          payload: {
            isAuthenticated: true,
            subject: "u1",
            roles: ["CLIENT"],
            bceidGuid: "guid-1",
            idirUsername: null,
          },
          datetimeRequested: "2026-08-31T00:00:00Z",
        }),
      ),
    );

    const me = await fetchMe<MePayloadBase>(API_URL);

    expect(me.subject).toBe("u1");
    expect(me.roles).toEqual(["CLIENT"]);
    expect(me.bceidGuid).toBe("guid-1");
  });

  it("calls GET /v1/auth/me", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(200, {
        payload: { isAuthenticated: true, subject: "u1", roles: [] },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    await fetchMe<MePayloadBase>(API_URL);

    expect(String(fetchMock.mock.calls[0][0])).toBe(
      "http://localhost:5000/v1/auth/me",
    );
  });

  it("throws on a non-ok response so callers never see half a session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(401, {})));

    await expect(fetchMe<MePayloadBase>(API_URL)).rejects.toThrow(/401/);
  });

  // The retry policy (useMe's shouldRetryMe) needs the status to tell a
  // non-transient 4xx from a transient failure.
  it("throws an HttpError carrying the HTTP status", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(403, {})));

    await expect(fetchMe<MePayloadBase>(API_URL)).rejects.toMatchObject({
      name: "HttpError",
      status: 403,
    });
  });
});

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
