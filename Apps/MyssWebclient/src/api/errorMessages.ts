import { API_URL } from "@/constants";

// The error message catalogue (/v1/forms/error-messages): the citizen-facing
// wording for every stable keyword, as the Service Designer publishes it in
// the content engine. ANONYMOUS by design, like the public bus pass spec: the
// catalogue is shared content, not per-user data, so this fetch carries NO
// auth header. Do not add authHeaders() here.
//
// A 422's field errors already arrive worded by the API, so the catalogue is
// only for the keywords the browser words itself: the bus pass outcomes the
// API reports by keyword alone.
//
// The generated client has this endpoint (getErrorMessages in
// api/generated/sdk.gen.ts); the raw fetch here follows the sibling forms,
// bus pass and estimator modules, which also unwrap `.payload` by hand.

/** Wording keyed by keyword (DOMAIN.CONTEXT.NAME). */
export type ErrorMessageCatalogue = Readonly<Record<string, string>>;

/**
 * Keeps only the string-to-string entries of a payload, so a malformed row
 * can never reach the page as "[object Object]" or an empty line.
 */
export function parseCatalogue(payload: unknown): ErrorMessageCatalogue {
  if (
    typeof payload !== "object" ||
    payload === null ||
    Array.isArray(payload)
  ) {
    return {};
  }

  const catalogue: Record<string, string> = {};
  for (const [keyword, message] of Object.entries(payload)) {
    if (typeof message === "string" && message.trim() !== "") {
      catalogue[keyword] = message;
    }
  }
  return catalogue;
}

/**
 * The catalogue's wording for a keyword, or `fallback` when the catalogue has
 * no row for it, has not loaded, or could not be loaded. The fallback is the
 * compiled wording the page shipped with, so an outage costs the citizen the
 * authored text and nothing more.
 */
export function errorMessageFor(
  catalogue: ErrorMessageCatalogue | undefined,
  keyword: string | null | undefined,
  fallback: string,
): string {
  if (!keyword || !catalogue) return fallback;
  const message = catalogue[keyword];
  return typeof message === "string" && message.trim() !== ""
    ? message
    : fallback;
}

/** The catalogue as the API currently serves it (anonymous read). */
export async function getErrorMessages(): Promise<ErrorMessageCatalogue> {
  const res = await fetch(`${API_URL}/v1/forms/error-messages`);
  if (!res.ok) {
    throw new Error(`Error message catalogue fetch failed (${res.status})`);
  }
  return parseCatalogue((await res.json()).payload);
}
