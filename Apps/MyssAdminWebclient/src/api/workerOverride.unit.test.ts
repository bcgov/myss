import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setAccessToken } from "@myss/web-common/auth";
import {
  ApplicationRequestError,
  SubmissionRejectedError,
} from "@myss/web-common/api";

import {
  getWorkerOverride,
  listWorkerOverrideDrafts,
  saveWorkerOverrideAnswers,
  startWorkerOverride,
  submitWorkerOverride,
  WORKER_OVERRIDE_KEYWORDS,
  type WorkerOverrideApplicationPayload,
} from "@/api/workerOverride";

// Unit tests for the worker override API client (MYSS-321): the right URL and
// method, the auth header, the body shape, and each refusal as the typed
// error the widget branches on (422 -> SubmissionRejectedError with the field
// errors; 404/409 -> ApplicationRequestError with keyword and version).

const ID = "11111111-2222-3333-4444-555555555555";

const application: WorkerOverrideApplicationPayload = {
  id: ID,
  referenceNumber: "IA-11111111",
  status: "DRAFT",
  version: 1,
  formSpecId: "worker-override-afa",
  formSpecVersion: 1,
  createdAt: "2026-11-03T10:00:00Z",
  updatedAt: "2026-11-03T10:00:00Z",
  submittedAt: null,
  answers: {},
  spec: null,
  accountId: null,
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

function header(init: RequestInit, name: string): string | undefined {
  return (init.headers as Record<string, string> | undefined)?.[name];
}

beforeEach(() => {
  setAccessToken("test-token");
});

afterEach(() => {
  vi.unstubAllGlobals();
  setAccessToken(undefined);
});

describe("startWorkerOverride", () => {
  it("POSTs to /v1/intake/worker-override/applications with the auth header", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(201, { payload: application }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await startWorkerOverride();

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/intake\/worker-override\/applications$/);
    expect(init.method).toBe("POST");
    expect(header(init, "Authorization")).toBe("Bearer test-token");
    expect(result).toEqual(application);
  });

  it("surfaces an unpublished form with its keyword", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(404, { keyword: WORKER_OVERRIDE_KEYWORDS.specNotPublished }),
      ),
    );

    const error = await startWorkerOverride().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect((error as ApplicationRequestError).keyword).toBe(
      WORKER_OVERRIDE_KEYWORDS.specNotPublished,
    );
  });
});

describe("listWorkerOverrideDrafts", () => {
  it("GETs /drafts with the auth header and unwraps the payload", async () => {
    const drafts = [
      {
        id: ID,
        referenceNumber: "IA-11111111",
        clientName: "Ada Lovelace",
        startedBy: "MWORKER",
        createdAt: "2026-11-03T10:00:00Z",
        updatedAt: "2026-11-03T10:05:00Z",
      },
    ];
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: drafts }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await listWorkerOverrideDrafts();

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/intake\/worker-override\/applications\/drafts$/);
    expect(init.method ?? "GET").toBe("GET");
    expect(header(init, "Authorization")).toBe("Bearer test-token");
    expect(result).toEqual(drafts);
  });

  it("surfaces a refusal as an ApplicationRequestError", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(403, {})));

    const error = await listWorkerOverrideDrafts().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect((error as ApplicationRequestError).status).toBe(403);
  });
});

describe("getWorkerOverride", () => {
  it("GETs one by id and unwraps the payload", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: application }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await getWorkerOverride(ID);

    const [url] = firstCall(fetchMock);
    expect(url).toMatch(
      new RegExp(`/v1/intake/worker-override/applications/${ID}$`),
    );
    expect(result.id).toBe(ID);
  });
});

describe("saveWorkerOverrideAnswers", () => {
  it("PUTs the version and answers to /answers", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(200, { payload: { ...application, version: 2 } }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const result = await saveWorkerOverrideAnswers(ID, {
      version: 1,
      answers: { firstName: "Ada" },
    });

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(
      new RegExp(`/v1/intake/worker-override/applications/${ID}/answers$`),
    );
    expect(init.method).toBe("PUT");
    expect(header(init, "Content-Type")).toBe("application/json");
    expect(JSON.parse(String(init.body))).toEqual({
      version: 1,
      answers: { firstName: "Ada" },
    });
    expect(result.version).toBe(2);
  });

  it("surfaces a stale version as a conflict carrying the current one", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(409, {
          keyword: WORKER_OVERRIDE_KEYWORDS.conflict,
          currentVersion: 3,
        }),
      ),
    );

    const error = await saveWorkerOverrideAnswers(ID, {
      version: 1,
      answers: {},
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    const conflict = error as ApplicationRequestError;
    expect(conflict.isConflict).toBe(true);
    expect(conflict.currentVersion).toBe(3);
  });
});

describe("submitWorkerOverride", () => {
  it("POSTs the version and answers to /submit", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(200, {
        payload: { ...application, status: "SUBMITTED", accountId: "acc" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const result = await submitWorkerOverride(ID, {
      version: 2,
      answers: { firstName: "Ada" },
    });

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(
      new RegExp(`/v1/intake/worker-override/applications/${ID}/submit$`),
    );
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body)).version).toBe(2);
    expect(result.status).toBe("SUBMITTED");
    expect(result.accountId).toBe("acc");
  });

  it("keeps every field error of a 422", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(422, {
          payload: [
            { field: "sin", keyword: "IDA.SIN.INVALID_CHECKSUM", message: "Bad SIN." },
            { field: "phone", keyword: "IDA.PHONE.INVALID_FORMAT", message: "Bad phone." },
          ],
        }),
      ),
    );

    const error = await submitWorkerOverride(ID, {
      version: 1,
      answers: {},
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(SubmissionRejectedError);
    expect((error as SubmissionRejectedError).errors.map((e) => e.field)).toEqual([
      "sin",
      "phone",
    ]);
  });
});
