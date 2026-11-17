import type { FormMessageTexts } from "@myss/web-common/api";

import { API_URL } from "@/constants";

// A form message's live texts (/v1/forms/{formSpecId}/messages/{messageKey}).
// ANONYMOUS by design, like the error message catalogue: a form's public page
// shows them before anyone signs in, so this fetch carries NO auth header.

function isText(value: unknown): value is string {
  return typeof value === "string" && value.trim() !== "";
}

/** The three texts of a payload, or null when any is missing or blank. */
export function parseFormMessage(payload: unknown): FormMessageTexts | null {
  if (typeof payload !== "object" || payload === null) return null;
  const { heading, title, body } = payload as Record<string, unknown>;
  return isText(heading) && isText(title) && isText(body)
    ? { heading, title, body }
    : null;
}

/** A form message's live texts, or null on a 404; throws on any other failure. */
export async function getFormMessage(
  formSpecId: string,
  messageKey: string,
): Promise<FormMessageTexts | null> {
  const res = await fetch(
    `${API_URL}/v1/forms/${encodeURIComponent(formSpecId)}/messages/${encodeURIComponent(messageKey)}`,
  );
  if (res.status === 404) return null;
  if (!res.ok) {
    throw new Error(`Form message fetch failed (${res.status})`);
  }
  const message = parseFormMessage((await res.json()).payload);
  if (!message) {
    throw new Error("Form message payload is malformed");
  }
  return message;
}
