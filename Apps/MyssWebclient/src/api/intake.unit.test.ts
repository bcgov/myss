import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { SubmissionRejectedError } from "@/api/forms";
import {
  ApplicationRequestError,
  createApplication,
  getApplication,
  INTAKE_KEYWORDS,
  listApplications,
  saveAnswers,
  submitApplication,
  type ApplicationPayload,
} from "@/api/intake";
import { setAccessToken } from "@/auth/accessToken";

// Unit tests for the intake API client. fetch is mocked, so these assert the
// seam between the UI and MyssApi: the right URL + method, the auth header,
// the body shape, and that each refusal surfaces as the typed error the
// widgets branch on (422 -> SubmissionRejectedError with every field error;
// 409/403/404 -> ApplicationRequestError with keyword and currentVersion).

const ID = "11111111-2222-3333-4444-555555555555";

const draft: ApplicationPayload = {
  id: ID,
  referenceNumber: "IA-11111111",
  status: "DRAFT",
  version: 1,
  formSpecId: "income-assistance-poc",
  formSpecVersion: 1,
  answers: { firstName: "Ada" },
  createdAt: "2026-09-28T10:00:00Z",
  updatedAt: "2026-09-28T10:00:00Z",
  submittedAt: null,
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

describe("createApplication", () => {
  it("POSTs /v1/intake/applications with the auth header and unwraps the payload", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(201, { payload: draft }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await createApplication();

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/intake\/applications$/);
    expect(init.method).toBe("POST");
    expect(authHeaderOf(init)).toBe("Bearer test-token");
    expect(result).toEqual(draft);
  });

  it("surfaces a missing profile as a 403 carrying the keyword", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(403, {
          status: 403,
          title: "A registered profile is required.",
          detail: "Register before starting an application.",
          keyword: INTAKE_KEYWORDS.profileRequired,
        }),
      ),
    );

    const error = await createApplication().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    const refusal = error as ApplicationRequestError;
    expect(refusal.status).toBe(403);
    expect(refusal.keyword).toBe(INTAKE_KEYWORDS.profileRequired);
    expect(refusal.message).toBe("Register before starting an application.");
    expect(refusal.isConflict).toBe(false);
  });
});

describe("listApplications and getApplication", () => {
  it("GETs the list and unwraps the payload", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: [draft] }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await listApplications();

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/intake\/applications$/);
    expect(init.method ?? "GET").toBe("GET");
    expect(authHeaderOf(init)).toBe("Bearer test-token");
    expect(result).toEqual([draft]);
  });

  it("GETs one by id and reports a 404 with its status", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(404, { status: 404 }));
    vi.stubGlobal("fetch", fetchMock);

    const error = await getApplication(ID).catch((e: unknown) => e);

    const [url] = firstCall(fetchMock);
    expect(url).toMatch(new RegExp(`/v1/intake/applications/${ID}$`));
    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect((error as ApplicationRequestError).status).toBe(404);
  });
});

describe("saveAnswers", () => {
  it("PUTs the version and answers as JSON", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        jsonResponse(200, { payload: { ...draft, version: 2 } }),
      );
    vi.stubGlobal("fetch", fetchMock);

    const result = await saveAnswers(ID, {
      version: 1,
      answers: { firstName: "Ada", lastName: "Lovelace" },
    });

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(new RegExp(`/v1/intake/applications/${ID}/answers$`));
    expect(init.method).toBe("PUT");
    expect(
      (init.headers as Record<string, string> | undefined)?.["Content-Type"],
    ).toBe("application/json");
    expect(JSON.parse(String(init.body))).toEqual({
      version: 1,
      answers: { firstName: "Ada", lastName: "Lovelace" },
    });
    expect(result.version).toBe(2);
  });

  it("surfaces a stale version as a conflict carrying the current version", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(409, {
          status: 409,
          title: "The application changed; reload and retry.",
          keyword: INTAKE_KEYWORDS.conflict,
          currentVersion: 3,
        }),
      ),
    );

    const error = await saveAnswers(ID, { version: 1, answers: {} }).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(ApplicationRequestError);
    const conflict = error as ApplicationRequestError;
    expect(conflict.isConflict).toBe(true);
    expect(conflict.keyword).toBe(INTAKE_KEYWORDS.conflict);
    expect(conflict.currentVersion).toBe(3);
  });

  it("tolerates a refusal that is not a problem-details body", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response("<html>Bad gateway</html>", { status: 502 }),
        ),
    );

    const error = await saveAnswers(ID, { version: 1, answers: {} }).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect((error as ApplicationRequestError).status).toBe(502);
    expect((error as ApplicationRequestError).keyword).toBeUndefined();
  });
});

describe("submitApplication", () => {
  it("POSTs the version and answers and returns the submitted application", async () => {
    const submitted: ApplicationPayload = {
      ...draft,
      status: "SUBMITTED",
      submittedAt: "2026-09-28T11:00:00Z",
    };
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: submitted }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await submitApplication(ID, {
      version: 1,
      answers: { firstName: "Ada", lastName: "Lovelace" },
    });

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(new RegExp(`/v1/intake/applications/${ID}/submit$`));
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body))).toEqual({
      version: 1,
      answers: { firstName: "Ada", lastName: "Lovelace" },
    });
    expect(result.status).toBe("SUBMITTED");
  });

  it("surfaces a 422 as SubmissionRejectedError carrying every field error", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(422, {
          payload: [
            {
              field: "lastName",
              keyword: "FORM.FIELD.REQUIRED",
              message: "Last name is required",
            },
          ],
        }),
      ),
    );

    const error = await submitApplication(ID, {
      version: 1,
      answers: { firstName: "Ada" },
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(SubmissionRejectedError);
    expect((error as SubmissionRejectedError).errors).toEqual([
      {
        field: "lastName",
        keyword: "FORM.FIELD.REQUIRED",
        message: "Last name is required",
      },
    ]);
  });
});
