import { describe, it, expect, vi, afterEach } from "vitest";

import { fetchMe } from "@/api/me";

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

    const me = await fetchMe();

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

    await fetchMe();

    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/v1\/auth\/me$/);
  });

  it("throws on a non-ok response so callers never see half a session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(401, {})));

    await expect(fetchMe()).rejects.toThrow(/401/);
  });

  // The retry policy (useMe's shouldRetryMe) needs the status to tell a
  // non-transient 4xx from a transient failure.
  it("throws an HttpError carrying the HTTP status", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(403, {})));

    await expect(fetchMe()).rejects.toMatchObject({
      name: "HttpError",
      status: 403,
    });
  });
});
