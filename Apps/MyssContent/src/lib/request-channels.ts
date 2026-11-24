/**
 * How an application was started. A form's channel rules
 * (`properties.myssChannels`) are keyed by these ids. The API's
 * `RequestChannels` and MyssFormio's `requestChannels.ts` hold the same list;
 * `Shared/validation/channel-specs.json` keeps the three equal.
 */
export const REQUEST_CHANNELS = ["client", "worker-override"] as const;

export type RequestChannel = (typeof REQUEST_CHANNELS)[number];

export function isRequestChannel(value: unknown): value is RequestChannel {
  return typeof value === "string" && (REQUEST_CHANNELS as readonly string[]).includes(value);
}
