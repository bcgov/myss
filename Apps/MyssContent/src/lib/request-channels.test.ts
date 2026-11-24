import { readFileSync } from "node:fs";
import { resolve } from "node:path";

import { describe, expect, it } from "vitest";

import { REQUEST_CHANNELS, isRequestChannel } from "./request-channels";

describe("REQUEST_CHANNELS", () => {
  it("equals the shared list", () => {
    const shared = JSON.parse(
      readFileSync(resolve(__dirname, "../../../../Shared/validation/channel-specs.json"), "utf8"),
    ) as { channels: string[] };

    expect([...REQUEST_CHANNELS]).toEqual(shared.channels);
  });
});

describe("isRequestChannel", () => {
  it.each([
    ["client", true],
    ["worker-override", true],
    ["Client", false],
    ["applicant-override", false],
    ["", false],
    [undefined, false],
  ])("%s is %s", (value, expected) => {
    expect(isRequestChannel(value)).toBe(expected);
  });
});
