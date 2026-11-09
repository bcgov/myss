import { afterEach, describe, expect, it, vi } from "vitest";

import { getFormMessage } from "@/api/formMessages";
import { API_URL } from "@/constants";

const texts = {
  heading: "Bus Pass Request is complete",
  title: "Request submitted",
  body: "Your reference number is {referenceNumber}.",
};

function answer(status: number, body?: unknown) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(body === undefined ? "" : JSON.stringify(body), {
      status,
      headers: { "Content-Type": "application/json" },
    }),
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("getFormMessage", () => {
  it("reads the message's route without an auth header and returns its three texts", async () => {
    const fetchMock = answer(200, { payload: { ...texts, changedBy: "JSMITH" } });

    await expect(getFormMessage("bc-bus-pass", "confirmation")).resolves.toEqual(texts);

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toBe(`${API_URL}/v1/forms/bc-bus-pass/messages/confirmation`);
    expect(new Headers(init?.headers).has("Authorization")).toBe(false);
  });

  it("escapes the form ID and the key in the path", async () => {
    const fetchMock = answer(404);

    await getFormMessage("a/b", "c?d");

    expect(String(fetchMock.mock.calls[0][0])).toBe(
      `${API_URL}/v1/forms/a%2Fb/messages/c%3Fd`,
    );
  });

  it("returns null when no message is published", async () => {
    answer(404);

    await expect(getFormMessage("bc-bus-pass", "confirmation")).resolves.toBeNull();
  });

  it("throws when the content engine cannot be read", async () => {
    answer(502, { title: "Content engine unavailable" });

    await expect(getFormMessage("bc-bus-pass", "confirmation")).rejects.toThrow("502");
  });

  it.each([
    ["a blank text", { ...texts, title: "  " }],
    ["a missing text", { heading: texts.heading, body: texts.body }],
    ["a text that is not a string", { ...texts, body: 42 }],
    ["no payload", undefined],
  ])("throws on a payload with %s", async (_case, payload) => {
    answer(200, { payload });

    await expect(getFormMessage("bc-bus-pass", "confirmation")).rejects.toThrow("malformed");
  });
});
