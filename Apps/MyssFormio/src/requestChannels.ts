// How an application was started. A form's channel rules
// (properties.myssChannels) are keyed by these ids. The API's RequestChannels
// and MyssContent's request-channels.ts hold the same list; the shared case
// file Shared/validation/channel-specs.json keeps the three equal.

import { isRecord } from "./specWalk";

export const REQUEST_CHANNELS = [
  { id: "client", label: "Client" },
  { id: "worker-override", label: "Worker override" },
] as const;

export type RequestChannel = (typeof REQUEST_CHANNELS)[number]["id"];

export const REQUEST_CHANNEL_IDS: readonly RequestChannel[] = REQUEST_CHANNELS.map((channel) => channel.id);

export function isRequestChannel(value: unknown): value is RequestChannel {
  return typeof value === "string" && (REQUEST_CHANNEL_IDS as readonly string[]).includes(value);
}

/** Where channel rules take effect, for the designer's help texts. */
export const CHANNEL_RULES_SCOPE =
  "Channel rules apply to applications for assistance only; other forms ignore them.";

/** Where a component's channel rules are authored, under `properties`. */
export const CHANNEL_RULES_PROPERTY = "myssChannels";

/** A component's channel rules as authored; `null` and absent are both no rules. */
export function channelRulesOf(component: Record<string, unknown>): unknown {
  return isRecord(component.properties) ? component.properties[CHANNEL_RULES_PROPERTY] : undefined;
}

/** A channel rule's setting on a component, when it is given as true or false. */
export function channelSetting(
  component: Record<string, unknown>,
  channel: RequestChannel,
  setting: "show" | "required",
): boolean | undefined {
  const rules = channelRulesOf(component);
  const rule = isRecord(rules) ? rules[channel] : undefined;
  const value = isRecord(rule) ? rule[setting] : undefined;
  return typeof value === "boolean" ? value : undefined;
}
