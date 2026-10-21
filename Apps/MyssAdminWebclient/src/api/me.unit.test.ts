import { describe, it, expect, vi, afterEach } from "vitest";

import { API_URL } from "@/constants";
import { fetchMe } from "@/api/me";

// fetchMe's parsing and errors are tested in @myss/web-common; this pins the
// API it reads.
afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchMe", () => {
  it("reads GET /v1/auth/me from this app's API", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          payload: { isAuthenticated: true, subject: "u1", roles: [] },
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    const me = await fetchMe();

    expect(me.subject).toBe("u1");
    expect(String(fetchMock.mock.calls[0][0])).toBe(`${API_URL}/v1/auth/me`);
  });
});
