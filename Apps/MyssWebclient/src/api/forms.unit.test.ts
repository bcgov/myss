import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  getDraft,
  listForms,
  publishForm,
  saveDraft,
  SpecRejectedError,
  type FormSummary,
} from "@/api/forms";
import { setAccessToken } from "@/auth/accessToken";

// Unit tests for the admin form-editor API client. fetch is mocked, so these
// assert the seam between the UI and MyssApi: the right URL + method, the auth
// header, and that a 422 surfaces as a typed SpecRejectedError carrying the
// parsed error list. The endpoints themselves are exercised end-to-end against a
// running stack (the Bruno collection and the admin editor's browser tests).

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

describe("listForms", () => {
  it("GETs /v1/forms with the auth header and unwraps the payload", async () => {
    const forms: FormSummary[] = [
      {
        formSpecId: "eligibility-estimator",
        title: "Eligibility estimator",
        versions: [
          { version: 1, isPublished: true },
          { version: 2, isPublished: false },
        ],
      },
    ];
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: forms }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await listForms();

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/forms$/);
    expect(init.method ?? "GET").toBe("GET");
    expect(authHeaderOf(init)).toBe("Bearer test-token");
    expect(result).toEqual(forms);
  });

  it("throws on a non-ok response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(502, {})));
    await expect(listForms()).rejects.toThrow(/502/);
  });
});

describe("getDraft", () => {
  it("GETs /v1/forms/{id}/draft and unwraps the payload", async () => {
    const draft = {
      formSpecId: "eligibility-estimator",
      version: 3,
      title: "Eligibility estimator",
      spec: { components: [] },
    };
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: draft }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await getDraft("eligibility-estimator");

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/forms\/eligibility-estimator\/draft$/);
    expect(init.method ?? "GET").toBe("GET");
    expect(authHeaderOf(init)).toBe("Bearer test-token");
    expect(result).toEqual(draft);
  });

  it("throws on a non-ok response (e.g. unknown id → 404)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(404, {})));
    await expect(getDraft("nope")).rejects.toThrow(/404/);
  });
});

describe("saveDraft", () => {
  it("PUTs /v1/forms/{id}/draft with the spec+title body and unwraps the payload", async () => {
    const saved = {
      formSpecId: "eligibility-estimator",
      version: 3,
      title: "New title",
      spec: { components: [{ key: "a", type: "textfield", input: true }] },
    };
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: saved }));
    vi.stubGlobal("fetch", fetchMock);

    const input = { spec: saved.spec, title: "New title" };
    const result = await saveDraft("eligibility-estimator", input);

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/forms\/eligibility-estimator\/draft$/);
    expect(init.method).toBe("PUT");
    expect(authHeaderOf(init)).toBe("Bearer test-token");
    expect((init.headers as Record<string, string>)["Content-Type"]).toBe(
      "application/json",
    );
    expect(JSON.parse(init.body as string)).toEqual(input);
    expect(result).toEqual(saved);
  });

  it("surfaces a 422 as SpecRejectedError with the parsed error list", async () => {
    const errors = [
      {
        field: "components",
        keyword: "FORMSPEC.COMPONENT_KEY.DUPLICATE",
        message: 'Duplicate component key "dupe".',
      },
    ];
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(jsonResponse(422, { payload: errors })),
    );

    const error = await saveDraft("eligibility-estimator", {
      spec: { components: [] },
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(SpecRejectedError);
    const rejected = error as SpecRejectedError;
    expect(rejected.status).toBe(422);
    expect(rejected.errors).toEqual(errors);
  });

  it("surfaces a non-422 failure as SpecRejectedError with no errors", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(502, {})));

    const error = await saveDraft("eligibility-estimator", {
      spec: { components: [] },
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(SpecRejectedError);
    expect((error as SpecRejectedError).status).toBe(502);
    expect((error as SpecRejectedError).errors).toEqual([]);
  });
});

describe("publishForm", () => {
  it("POSTs /v1/forms/{id}/publish with the auth header and unwraps the version", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: { version: 4 } }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await publishForm("eligibility-estimator");

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/forms\/eligibility-estimator\/publish$/);
    expect(init.method).toBe("POST");
    expect(authHeaderOf(init)).toBe("Bearer test-token");
    expect(result).toEqual({ version: 4 });
  });

  it("surfaces a 422 as SpecRejectedError with the parsed error list", async () => {
    const errors = [
      {
        field: "version",
        keyword: "FORMSPEC.VERSION.SEQUENCE",
        message: "Version must be the next in sequence.",
      },
    ];
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(jsonResponse(422, { payload: errors })),
    );

    const error = await publishForm("eligibility-estimator").catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(SpecRejectedError);
    expect((error as SpecRejectedError).errors).toEqual(errors);
  });

  it("surfaces a non-422 failure as SpecRejectedError with no errors", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(502, {})));

    const error = await publishForm("eligibility-estimator").catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(SpecRejectedError);
    expect((error as SpecRejectedError).status).toBe(502);
    expect((error as SpecRejectedError).errors).toEqual([]);
  });
});
