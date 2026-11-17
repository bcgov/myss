import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setAccessToken } from "@myss/web-common/auth";

import {
  FormLoadError,
  getDraft,
  getFormVersion,
  isVersionTaken,
  listForms,
  publishForm,
  saveDraft,
  SpecRejectedError,
  VERSION_TAKEN,
  type FormSummary,
} from "@/api/forms";

// Unit tests for the admin form-editor API client. fetch is mocked, so these
// assert the seam between the UI and MyssAdminApi: the right URL + method, the auth
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

describe("getFormVersion", () => {
  it("GETs /v1/forms/{id}/versions/{version} and unwraps the payload", async () => {
    const version = {
      formSpecId: "bc-bus-pass",
      version: 3,
      title: "BC Bus Pass",
      spec: { components: [] },
    };
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: version }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await getFormVersion("bc-bus-pass", 3);

    const [url, init] = firstCall(fetchMock);
    expect(url).toMatch(/\/v1\/forms\/bc-bus-pass\/versions\/3$/);
    expect(init.method ?? "GET").toBe("GET");
    expect(authHeaderOf(init)).toBe("Bearer test-token");
    expect(result).toEqual(version);
  });

  it("encodes the form ID in the URL", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { payload: {} }));
    vi.stubGlobal("fetch", fetchMock);

    await getFormVersion("a/b", 1);

    expect(firstCall(fetchMock)[0]).toMatch(/\/v1\/forms\/a%2Fb\/versions\/1$/);
  });

  it("throws a FormLoadError carrying the status (e.g. unknown version → 404)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(404, {})));

    const error = await getFormVersion("bc-bus-pass", 99).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(FormLoadError);
    expect((error as FormLoadError).status).toBe(404);
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

  it("sends newVersion only when it is given", async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(() =>
        Promise.resolve(jsonResponse(200, { payload: {} })),
      );
    vi.stubGlobal("fetch", fetchMock);

    await saveDraft("new-form", { spec: { components: [] }, newVersion: 1 });
    await saveDraft("new-form", { spec: { components: [] } });

    const bodies = fetchMock.mock.calls.map(
      ([, init]) => JSON.parse((init as RequestInit).body as string) as object,
    );
    expect(bodies[0]).toMatchObject({ newVersion: 1 });
    expect(bodies[1]).not.toHaveProperty("newVersion");
  });
});

describe("isVersionTaken", () => {
  it("is true for a 409 carrying the version-taken keyword", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse(409, {
          payload: [
            {
              field: "newVersion",
              keyword: VERSION_TAKEN,
              message: 'A form with the ID "new-form" already exists.',
            },
          ],
        }),
      ),
    );

    const error = await saveDraft("new-form", {
      spec: { components: [] },
      newVersion: 1,
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(SpecRejectedError);
    expect((error as SpecRejectedError).status).toBe(409);
    expect(isVersionTaken(error)).toBe(true);
  });

  it("is false for a refusal with another keyword, and for other errors", () => {
    const refused = new SpecRejectedError(422, [
      {
        field: "spec",
        keyword: "FORMSPEC.COMPONENT_KEY.DUPLICATE",
        message: "Duplicate.",
      },
    ]);

    expect(isVersionTaken(refused)).toBe(false);
    expect(isVersionTaken(new Error("boom"))).toBe(false);
    expect(isVersionTaken(undefined)).toBe(false);
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
