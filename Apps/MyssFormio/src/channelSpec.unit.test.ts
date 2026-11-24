/// <reference types="node" />
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import type { FormType } from "@formio/react/lib/components/Form";
import { describe, expect, it } from "vitest";

import { resolveSpecForChannel } from "./channelSpec";
import { REQUEST_CHANNEL_IDS } from "./requestChannels";

// The resolve cases are shared with the API's ChannelSpec.Resolve, so the
// editor's preview shows each channel the form the API serves it.
interface ResolveCase {
  name: string;
  spec: FormType;
  resolved: Record<string, unknown>;
}

const resolveCases = (
  JSON.parse(
    readFileSync(fileURLToPath(new URL("../../../Shared/validation/channel-specs.json", import.meta.url)), "utf8"),
  ) as { resolveCases: ResolveCase[] }
).resolveCases;

describe("resolveSpecForChannel", () => {
  it("has cases to run", () => {
    expect(resolveCases.length).toBeGreaterThan(0);
  });

  it.each(resolveCases)("$name", ({ spec, resolved }) => {
    expect(Object.keys(resolved).sort()).toEqual([...REQUEST_CHANNEL_IDS].sort());
    for (const channel of REQUEST_CHANNEL_IDS) {
      expect(resolveSpecForChannel(spec, channel), channel).toStrictEqual(resolved[channel]);
    }
  });

  it("leaves the input unchanged", () => {
    const { spec } = resolveCases.find((c) => c.name === "a panel hidden in a channel takes its fields with it")!;
    const before = structuredClone(spec);

    resolveSpecForChannel(spec, "worker-override");

    expect(spec).toEqual(before);
  });
});
