import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ApplicationRequestError } from "@/api/intake";
import {
  getReviewApplication,
  listReviewQueue,
  performReviewAction,
  REVIEW_KEYWORDS,
  type ReviewApplicationPayload,
  type ReviewApplicationSummaryPayload,
} from "@/api/review";
import { setAccessToken } from "@/auth/accessToken";

// Unit tests for the review API client: the right URL and method, the auth
// header, the body shape, and that each refusal surfaces as the typed error
// the widgets branch on (409 -> ApplicationRequestError with keyword and
// currentVersion).

const ID = "11111111-2222-3333-4444-555555555555";

const summary: ReviewApplicationSummaryPayload = {
  id: ID,
  referenceNumber: "IA-11111111",
  status: "SUBMITTED",
  submittedAt: "2026-09-28T11:00:00Z",
  streamVersion: 1,
};

const application: ReviewApplicationPayload = {
  ...summary,
  answers: { firstName: "Ada", lastName: "Lovelace" },
  formSpecId: "income-assistance-poc",
  formSpecVersion: 1,
  spec: null,
  availableActions: ["Review"],
};

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** The [url, init] of the first fetch call. */
function firstCall(fetchMock: ReturnType<typeof vi.fn>): [string, RequestInit] {
  const [url, init] = fetchMock.mock.calls[0] as [unknown, RequestInit];
  return [String(url), init ?? {}];
}

function authHeaderOf(init: RequestInit): string | undefined {
  return (init.headers as Record<string, string> | undefined)?.Authorization;
}

beforeEach(() => {
  setAccessToken("test-token");
});

afterEach(() => {
  vi.unstubAllGlobals();
  setAccessToken(undefined);
});

describe("listReviewQueue", () => {
  it("GETs /v1/intake/review/applications with the auth header and unwraps the payload", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: [summary] }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await listReviewQueue();

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/intake\/review\/applications$/);
    expect(init.method ?? "GET").toBe("GET");
    expect(authHeaderOf(init)).toBe("Bearer test-token");
    expect(result).toEqual([summary]);
  });

  it("surfaces a forbidden caller with its status", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(jsonResponse(403, { status: 403 })),
    );

    const error = await listReviewQueue().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect((error as ApplicationRequestError).status).toBe(403);
  });
});

describe("getReviewApplication", () => {
  it("GETs one by id and unwraps the payload", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: application }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await getReviewApplication(ID);

    const [url] = firstCall(fetchMock);
    expect(url).toMatch(new RegExp(`/v1/intake/review/applications/${ID}$`));
    expect(result.availableActions).toEqual(["Review"]);
  });

  it("reports a draft or unknown id as a 404", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(jsonResponse(404, { status: 404 })),
    );

    const error = await getReviewApplication(ID).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect((error as ApplicationRequestError).status).toBe(404);
  });
});

describe("performReviewAction", () => {
  it.each([
    ["review", "UNDER_REVIEW"],
    ["accept", "ACCEPTED"],
    ["deny", "DENIED"],
  ] as const)(
    "POSTs the stream version to /%s and returns the moved application",
    async (route, status) => {
      const fetchMock = vi.fn().mockResolvedValue(
        jsonResponse(200, {
          payload: {
            ...application,
            status,
            streamVersion: 2,
            spec: undefined,
          },
        }),
      );
      vi.stubGlobal("fetch", fetchMock);

      const result = await performReviewAction(ID, route, 1);

      const [url, init] = firstCall(fetchMock);
      expect(url).toMatch(
        new RegExp(`/v1/intake/review/applications/${ID}/${route}$`),
      );
      expect(init.method).toBe("POST");
      expect(
        (init.headers as Record<string, string> | undefined)?.["Content-Type"],
      ).toBe("application/json");
      expect(JSON.parse(String(init.body))).toEqual({ streamVersion: 1 });
      expect(result.status).toBe(status);
      expect(result.streamVersion).toBe(2);
    },
  );

  it("surfaces a stale stream version as a conflict carrying the current one", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(409, {
          status: 409,
          title: "The application changed; reload and retry.",
          keyword: REVIEW_KEYWORDS.eventConflict,
          currentVersion: 3,
        }),
      ),
    );

    const error = await performReviewAction(ID, "accept", 1).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(ApplicationRequestError);
    const conflict = error as ApplicationRequestError;
    expect(conflict.isConflict).toBe(true);
    expect(conflict.keyword).toBe(REVIEW_KEYWORDS.eventConflict);
    expect(conflict.currentVersion).toBe(3);
  });

  it("surfaces an action the state does not allow with its keyword", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(409, {
          status: 409,
          keyword: REVIEW_KEYWORDS.notAllowed,
          currentVersion: 1,
        }),
      ),
    );

    const error = await performReviewAction(ID, "accept", 1).catch(
      (e: unknown) => e,
    );

    expect((error as ApplicationRequestError).keyword).toBe(
      REVIEW_KEYWORDS.notAllowed,
    );
  });
});
