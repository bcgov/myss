import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setAccessToken } from "@myss/web-common/auth";
import { ApplicationRequestError } from "@myss/web-common/api";

import {
  CLIENT_SEARCH_KEYWORDS,
  searchClients,
  searchQuery,
} from "@/api/clientSearch";

// Unit tests for the client search API client (MYSS-275): the query string
// carries only the fields filled in, the auth header goes with it, and a
// refusal is the typed error the widget branches on.

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

beforeEach(() => {
  setAccessToken("test-token");
});

afterEach(() => {
  vi.unstubAllGlobals();
  setAccessToken(undefined);
});

describe("searchQuery", () => {
  it("leaves out blank fields and trims the rest", () => {
    expect(
      searchQuery({ firstName: " Ada ", lastName: "", sin: "   ", bceidUsername: "ADA" }),
    ).toBe("firstName=Ada&bceidUsername=ADA");
  });
});

describe("searchClients", () => {
  it("GETs /v1/support/clients with the criteria and the auth header", async () => {
    const payload = { clients: [], hasMore: false };
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await searchClients({ lastName: "Love lace" });

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toMatch(/\/v1\/support\/clients\?lastName=Love\+lace$/);
    expect((init.headers as Record<string, string>).Authorization).toBe(
      "Bearer test-token",
    );
    expect(result).toEqual(payload);
  });

  it("throws the refusal's keyword on a 400", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(400, { keyword: CLIENT_SEARCH_KEYWORDS.sinInvalid }),
      ),
    );

    const error = await searchClients({ sin: "833" }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect((error as ApplicationRequestError).status).toBe(400);
    expect((error as ApplicationRequestError).keyword).toBe(
      CLIENT_SEARCH_KEYWORDS.sinInvalid,
    );
  });
});
