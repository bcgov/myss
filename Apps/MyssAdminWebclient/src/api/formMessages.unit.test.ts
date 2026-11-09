import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setAccessToken } from "@myss/web-common/auth";

import {
  FormMessageLoadError,
  FormMessageRejectedError,
  getFormMessage,
  listFormMessages,
  saveFormMessage,
  type FormMessageDetail,
  type SaveFormMessageInput,
} from "@/api/formMessages";

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function stubFetch(response: Response) {
  const fetchMock = vi.fn().mockResolvedValue(response);
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function call(fetchMock: ReturnType<typeof stubFetch>) {
  return fetchMock.mock.calls[0] as [string, RequestInit];
}

const definition = {
  formSpecId: "bc-bus-pass",
  messageKey: "confirmation",
  name: "Confirmation message",
  description: "Shown after a request is submitted successfully.",
  placeholders: [
    {
      name: "referenceNumber",
      description: "the applicant's reference number",
      sample: "1-0123456789",
      required: true,
    },
  ],
};

const detail: FormMessageDetail = { definition, current: null, history: [] };

const input: SaveFormMessageInput = {
  basedOn: "entry-1",
  heading: "Done",
  title: "Request submitted",
  body: "Your number is {referenceNumber}.",
};

beforeEach(() => {
  setAccessToken("test-token");
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("listFormMessages", () => {
  it("reads the list from the admin API with the admin's token", async () => {
    const summaries = [{ definition, lastChangedAt: null, lastChangedBy: null }];
    const fetchMock = stubFetch(jsonResponse(200, { payload: summaries }));

    await expect(listFormMessages()).resolves.toEqual(summaries);

    const [url, init] = call(fetchMock);
    expect(url).toMatch(/\/v1\/form-messages$/);
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer test-token");
  });

  it.each([403, 502])("throws a load error carrying a %i", async (status) => {
    stubFetch(jsonResponse(status, {}));

    const error = await listFormMessages().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(FormMessageLoadError);
    expect((error as FormMessageLoadError).status).toBe(status);
  });
});

describe("getFormMessage", () => {
  it("reads one message, escaping the form ID and the key", async () => {
    const fetchMock = stubFetch(jsonResponse(200, { payload: detail }));

    await expect(getFormMessage("a/b", "c?d")).resolves.toEqual(detail);

    const [url, init] = call(fetchMock);
    expect(url).toMatch(/\/v1\/forms\/a%2Fb\/messages\/c%3Fd$/);
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer test-token");
  });

  it("throws a load error carrying a 404", async () => {
    stubFetch(new Response("", { status: 404 }));

    const error = await getFormMessage("bc-bus-pass", "intro").catch((e: unknown) => e);

    expect(error).toBeInstanceOf(FormMessageLoadError);
    expect((error as FormMessageLoadError).status).toBe(404);
  });
});

describe("saveFormMessage", () => {
  it("posts the texts and the entry the edit started from, and returns the saved message", async () => {
    const fetchMock = stubFetch(jsonResponse(200, { payload: detail }));

    await expect(saveFormMessage("bc-bus-pass", "confirmation", input)).resolves.toEqual(detail);

    const [url, init] = call(fetchMock);
    expect(url).toMatch(/\/v1\/forms\/bc-bus-pass\/messages\/confirmation$/);
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer test-token");
    expect((init.headers as Record<string, string>)["Content-Type"]).toBe("application/json");
    expect(JSON.parse(init.body as string)).toEqual(input);
  });

  it.each([
    [422, "FORM_MESSAGE.TEXT.REQUIRED"],
    [409, "FORM_MESSAGE.CHANGED"],
  ])("surfaces a %i with its errors", async (status, keyword) => {
    const errors = [{ field: "body", keyword, message: "Refused." }];
    stubFetch(jsonResponse(status, { payload: errors }));

    const error = await saveFormMessage("bc-bus-pass", "confirmation", input).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(FormMessageRejectedError);
    expect((error as FormMessageRejectedError).status).toBe(status);
    expect((error as FormMessageRejectedError).errors).toEqual(errors);
  });

  it.each([401, 403, 502])("surfaces a %i with no errors", async (status) => {
    stubFetch(new Response("Refused", { status }));

    const error = await saveFormMessage("bc-bus-pass", "confirmation", input).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(FormMessageRejectedError);
    expect((error as FormMessageRejectedError).status).toBe(status);
    expect((error as FormMessageRejectedError).errors).toEqual([]);
  });
});
