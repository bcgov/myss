/// <reference types="node" />
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

import { REQUEST_CHANNEL_IDS, channelSetting, isRequestChannel } from "./requestChannels";

describe("REQUEST_CHANNEL_IDS", () => {
  it("equals the shared list", () => {
    const shared = JSON.parse(
      readFileSync(fileURLToPath(new URL("../../../Shared/validation/channel-specs.json", import.meta.url)), "utf8"),
    ) as { channels: string[] };

    expect([...REQUEST_CHANNEL_IDS]).toEqual(shared.channels);
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

describe("channelSetting", () => {
  const component = {
    key: "sin",
    properties: { myssChannels: { "worker-override": { required: false, show: "no" } } },
  };

  it("reads a setting given as true or false", () => {
    expect(channelSetting(component, "worker-override", "required")).toBe(false);
  });

  it("ignores a setting that is not true or false", () => {
    expect(channelSetting(component, "worker-override", "show")).toBeUndefined();
  });

  it("finds nothing for a channel without a rule, or a component without rules", () => {
    expect(channelSetting(component, "client", "required")).toBeUndefined();
    expect(channelSetting({ key: "name" }, "client", "show")).toBeUndefined();
  });
});
