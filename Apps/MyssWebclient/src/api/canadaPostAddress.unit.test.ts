import { afterEach, describe, expect, it, vi } from "vitest";

import {
  CanadaPostAddressError,
  findCanadaPostAddresses,
  retrieveCanadaPostAddress,
} from "@/api/canadaPostAddress";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("findCanadaPostAddresses", () => {
  it("does not call Canada Post for an empty search", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      findCanadaPostAddresses({ apiKey: "test-key", searchTerm: "  " }),
    ).resolves.toEqual([]);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("fails before calling Canada Post when the browser key is blank", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      findCanadaPostAddresses({ apiKey: "  ", searchTerm: "501" }),
    ).rejects.toThrow("address suggestions are not configured");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("searches Canadian English addresses with a maximum of seven suggestions", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          Items: [
            {
              Id: "CAN|123",
              Text: "501 Belleville St",
              Description: "Victoria, BC, V8V 1X4",
              Next: "Retrieve",
            },
          ],
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      findCanadaPostAddresses({
        apiKey: "test-key",
        searchTerm: "501 Belleville",
      }),
    ).resolves.toEqual([
      {
        id: "CAN|123",
        text: "501 Belleville St",
        description: "Victoria, BC, V8V 1X4",
        next: "Retrieve",
      },
    ]);

    const url = new URL(String(fetchMock.mock.calls[0][0]));
    expect(url.protocol).toBe("https:");
    expect(url.searchParams.get("Key")).toBe("test-key");
    expect(url.searchParams.get("SearchTerm")).toBe("501 Belleville");
    expect(url.searchParams.get("Country")).toBe("CAN");
    expect(url.searchParams.get("LanguagePreference")).toBe("en");
    expect(url.searchParams.get("MaxSuggestions")).toBe("7");
  });

  it("passes a selected container as LastId for a follow-up search", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ Items: [] })));
    vi.stubGlobal("fetch", fetchMock);

    await findCanadaPostAddresses({
      apiKey: "test-key",
      searchTerm: "501 Belleville",
      lastId: "CAN|building",
    });

    const url = new URL(String(fetchMock.mock.calls[0][0]));
    expect(url.searchParams.get("LastId")).toBe("CAN|building");
  });

  it("turns an HTTP-200 Error item into an explicit service error", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            Items: [
              {
                Error: "8",
                Description: "Key daily limit exceeded",
                Cause: "The daily limit was reached.",
                Resolution: "Increase the configured limit.",
              },
            ],
          }),
        ),
      ),
    );

    const error = await findCanadaPostAddresses({
      apiKey: "test-key",
      searchTerm: "501",
    }).catch((reason: unknown) => reason);

    expect(error).toBeInstanceOf(CanadaPostAddressError);
    expect(error).toMatchObject({
      message: "Key daily limit exceeded",
      code: "8",
      causeDescription: "The daily limit was reached.",
      resolution: "Increase the configured limit.",
    });
  });

  it("reports an unsuccessful HTTP response", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(null, { status: 503 })),
    );

    await expect(
      findCanadaPostAddresses({ apiKey: "test-key", searchTerm: "501" }),
    ).rejects.toThrow("address search failed (503)");
  });

  it.each([
    ["invalid JSON", new Response("not json"), "returned invalid JSON"],
    [
      "invalid response",
      new Response(JSON.stringify({ Results: [] })),
      "returned an invalid response",
    ],
  ])("reports an %s body", async (_case, response, expected) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response));

    await expect(
      findCanadaPostAddresses({ apiKey: "test-key", searchTerm: "501" }),
    ).rejects.toThrow(expected);
  });

  it("rejects an unsupported provider next step", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            Items: [{ Id: "CAN|123", Text: "501 Belleville", Next: "Other" }],
          }),
        ),
      ),
    );

    await expect(
      findCanadaPostAddresses({ apiKey: "test-key", searchTerm: "501" }),
    ).rejects.toThrow("unsupported next step: Other");
  });
});

describe("retrieveCanadaPostAddress", () => {
  it("uses the ENG result and maps the complete Canada Post address", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          Items: [
            {
              Language: "FRE",
              DomesticId: "fre-mak",
              SubBuilding: "APP 12",
              Line1: "12-501 RUE BELLEVILLE",
              Line2: "",
              City: "VICTORIA",
              ProvinceCode: "BC",
              PostalCode: "V8V 1X4",
            },
            {
              Language: "ENG",
              DomesticId: "eng-mak",
              SubBuilding: "UNIT 12",
              Line1: "12-501 BELLEVILLE ST",
              Line2: "REAR ENTRANCE",
              City: "VICTORIA",
              ProvinceCode: "BC",
              PostalCode: "V8V 1X4",
            },
          ],
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      retrieveCanadaPostAddress({
        apiKey: "test-key",
        id: "CAN|123",
      }),
    ).resolves.toEqual({
      line1: "12-501 BELLEVILLE ST",
      line2: "REAR ENTRANCE",
      city: "VICTORIA",
      provinceCode: "BC",
      postalCode: "V8V 1X4",
    });

    const url = new URL(String(fetchMock.mock.calls[0][0]));
    expect(url.protocol).toBe("https:");
    expect(url.searchParams.get("Key")).toBe("test-key");
    expect(url.searchParams.get("Id")).toBe("CAN|123");
  });

  it("rejects a retrieve response without an English item", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            Items: [
              {
                Language: "FRE",
                DomesticId: "fre-mak",
                Line1: "501 RUE BELLEVILLE",
                City: "VICTORIA",
                ProvinceCode: "BC",
                PostalCode: "V8V 1X4",
              },
            ],
          }),
        ),
      ),
    );

    await expect(
      retrieveCanadaPostAddress({
        apiKey: "test-key",
        id: "CAN|123",
      }),
    ).rejects.toThrow("Canada Post did not return an English address.");
  });
});
