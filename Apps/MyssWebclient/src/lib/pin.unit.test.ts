/// <reference types="node" />
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { afterEach, describe, expect, it } from "vitest";

import { setErrorCatalogue } from "@/lib/errorCatalogue";
import {
  checkNewPin,
  checkPin,
  PIN_DEFAULT_MESSAGES,
  PIN_KEYWORDS,
  pinMessage,
} from "./pin";

interface Vector {
  value: string;
  keyword?: string;
  note?: string;
}

// Read from disk rather than imported, for the reason validationRules.unit.test
// gives: the image build cannot import a file outside Apps/MyssWebclient.
function readShared<T>(file: string): T {
  const url = new URL(`../../../../Shared/validation/${file}`, import.meta.url);
  return JSON.parse(readFileSync(fileURLToPath(url), "utf8")) as T;
}

const vectors = readShared<{ pin: { valid: Vector[]; invalid: Vector[] } }>(
  "validation-vectors.json",
).pin;

const sharedMessages = readShared<{
  messages: { keyword: string; message: string }[];
}>("error-messages.json");

const FIELDS = { pin: "pin", confirmation: "pinConfirmation" };

// The same vectors as MyssApi.Tests/Domain/PinTests.cs, so the browser and the
// API agree on what a PIN is.
describe("checkPin, shared vectors", () => {
  it("has vectors to run", () => {
    expect(vectors.valid.length).toBeGreaterThan(0);
    expect(vectors.invalid.length).toBeGreaterThan(0);
  });

  it.each(vectors.valid.map((v) => [v.value, v.note ?? ""]))(
    "accepts %j (%s)",
    (value) => {
      expect(checkPin(value)).toBeNull();
    },
  );

  it.each(vectors.invalid.map((v) => [v.value, v.keyword ?? "", v.note ?? ""]))(
    "refuses %j with %s (%s)",
    (value, keyword) => {
      expect(checkPin(value)).toBe(keyword);
    },
  );
});

describe("checkNewPin", () => {
  it("passes a PIN typed twice the same", () => {
    expect(checkNewPin("4821", "4821", FIELDS)).toEqual([]);
  });

  it("reports the PIN's format on the PIN field, and nothing else", () => {
    expect(checkNewPin("48", "", FIELDS)).toEqual([
      {
        field: "pin",
        keyword: PIN_KEYWORDS.invalidFormat,
        message: "Enter a 4-digit PIN using numbers only.",
      },
    ]);
  });

  it("reports a mismatch on the confirmation field", () => {
    expect(checkNewPin("4821", "4812", FIELDS)).toEqual([
      {
        field: "pinConfirmation",
        keyword: PIN_KEYWORDS.mismatch,
        message: "The two PINs do not match.",
      },
    ]);
  });
});

describe("pinMessage", () => {
  afterEach(() => setErrorCatalogue(undefined));

  it("uses the catalogue's wording when it has a row", () => {
    setErrorCatalogue({ [PIN_KEYWORDS.mismatch]: "Authored mismatch wording" });
    expect(pinMessage(PIN_KEYWORDS.mismatch)).toBe("Authored mismatch wording");
  });

  it("falls back to the compiled wording", () => {
    expect(pinMessage(PIN_KEYWORDS.mismatch)).toBe("The two PINs do not match.");
  });
});

describe("PIN_DEFAULT_MESSAGES", () => {
  it("matches the shared error message catalogue word for word", () => {
    const byKeyword = new Map(
      sharedMessages.messages.map((row) => [row.keyword, row.message]),
    );
    for (const [keyword, message] of Object.entries(PIN_DEFAULT_MESSAGES)) {
      expect(byKeyword.get(keyword), keyword).toBe(message);
    }
  });
});
