import { readFileSync } from "node:fs";
import { resolve } from "node:path";

import { describe, expect, it } from "vitest";

import {
  ERROR_MESSAGE_KEYWORD_PATTERN,
  seededErrorMessages,
  type ErrorMessageSeed,
} from "./error-message-seed-data";

/**
 * The seed is one of two mirrors of Shared/validation/error-messages.json; the
 * other is MyssApi's compiled fallback. The shared file cannot be imported by
 * the Strapi build (it sits outside the app's TypeScript root and outside the
 * container's build context), so the seed carries a copy and this test is what
 * keeps the copy honest. Read from disk here rather than imported, for the same
 * reason.
 */
const SHARED_FILE = resolve(
  __dirname,
  "../../../../Shared/validation/error-messages.json",
);

interface SharedCatalogue {
  readonly messages: readonly ErrorMessageSeed[];
}

function readSharedCatalogue(): SharedCatalogue {
  return JSON.parse(readFileSync(SHARED_FILE, "utf8")) as SharedCatalogue;
}

describe("error message seed", () => {
  it("is identical to the shared catalogue, row for row", () => {
    const shared = readSharedCatalogue();

    // toEqual rather than a set comparison: order is part of the contract, so
    // the two mirrors read the same to a reviewer diffing them side by side.
    expect(seededErrorMessages).toEqual(shared.messages);
  });

  it("seeds at least one row", () => {
    expect(seededErrorMessages.length).toBeGreaterThan(0);
  });

  it("uses DOMAIN.CONTEXT.NAME keywords, each exactly once", () => {
    const keywords = seededErrorMessages.map((row) => row.keyword);

    for (const keyword of keywords) {
      expect(keyword).toMatch(ERROR_MESSAGE_KEYWORD_PATTERN);
    }
    expect(new Set(keywords).size).toBe(keywords.length);
  });

  it("never seeds a blank message", () => {
    for (const row of seededErrorMessages) {
      expect(row.message.trim()).not.toBe("");
    }
  });

  it("leaves FORM.FIELD.REQUIRED to per-field wording on the form", () => {
    // The shared file's header explains why: the bus pass rules and the form
    // specs carry field-specific required wording, and a catalogue row would
    // flatten all of it to one sentence.
    expect(
      seededErrorMessages.some((row) => row.keyword === "FORM.FIELD.REQUIRED"),
    ).toBe(false);
  });
});
