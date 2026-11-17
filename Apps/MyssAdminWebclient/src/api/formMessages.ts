import {
  readValidationErrors,
  type FormMessagePlaceholder,
  type FormMessageTexts,
  type FormValidationError,
} from "@myss/web-common/api";
import { authHeaders } from "@myss/web-common/auth";

import { API_URL } from "@/constants";

// The form message editor's calls to MyssAdminApi: list the defined messages
// with their last change, read one message with its history, and save a new
// version (GET /v1/form-messages, GET and POST
// /v1/forms/{formSpecId}/messages/{messageKey}). All need an IDIR sign-in.

/** A defined form message: what editors call it, where applicants see it, and its placeholders. */
export interface FormMessageDefinition {
  formSpecId: string;
  messageKey: string;
  name: string;
  description: string;
  placeholders: FormMessagePlaceholder[];
}

/** One saved version of a form message; `changedAt` is an ISO 8601 timestamp. */
export interface FormMessageEntry extends FormMessageTexts {
  id: string;
  changedBy: string;
  changedAt: string;
}

/** A defined message and its last change; both null when none is saved. */
export interface FormMessageSummary {
  definition: FormMessageDefinition;
  lastChangedAt: string | null;
  lastChangedBy: string | null;
}

/** A message as the editor shows it: the live entry, or null, and the history, newest first. */
export interface FormMessageDetail {
  definition: FormMessageDefinition;
  current: FormMessageEntry | null;
  history: FormMessageEntry[];
}

/** The texts to save, and the ID of the live entry the edit started from, or null. */
export interface SaveFormMessageInput extends FormMessageTexts {
  basedOn: string | null;
}

/** A read the API refused or could not answer, such as 403, 404 or 502. */
export class FormMessageLoadError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "FormMessageLoadError";
    this.status = status;

    // Keeps `instanceof` working when TypeScript downlevels the class.
    Object.setPrototypeOf(this, FormMessageLoadError.prototype);
  }
}

/**
 * A save the API refused. `errors` holds the 422 collection, or the 409's
 * single error; it is empty for other failures (401, 403, 502).
 */
export class FormMessageRejectedError extends Error {
  readonly status: number;
  readonly errors: readonly FormValidationError[];

  constructor(status: number, errors: readonly FormValidationError[]) {
    super(
      errors.length > 0
        ? errors.map((error) => error.message).join(" ")
        : `Form message save failed (${status})`,
    );
    this.name = "FormMessageRejectedError";
    this.status = status;
    this.errors = errors;

    Object.setPrototypeOf(this, FormMessageRejectedError.prototype);
  }
}

function messageUrl(formSpecId: string, messageKey: string): string {
  return `${API_URL}/v1/forms/${encodeURIComponent(formSpecId)}/messages/${encodeURIComponent(messageKey)}`;
}

async function read<T>(url: string, what: string): Promise<T> {
  const res = await fetch(url, { headers: authHeaders() });
  if (!res.ok) {
    throw new FormMessageLoadError(res.status, `${what} fetch failed (${res.status})`);
  }
  return (await res.json()).payload;
}

/** Every defined message with its last change, in the order the definitions list them. */
export function listFormMessages(): Promise<FormMessageSummary[]> {
  return read(`${API_URL}/v1/form-messages`, "Form messages");
}

/** One message's definition, live entry and history, read from the content engine without a cache. */
export function getFormMessage(
  formSpecId: string,
  messageKey: string,
): Promise<FormMessageDetail> {
  return read(messageUrl(formSpecId, messageKey), "Form message");
}

/**
 * Saves and publishes a message's new texts, and returns the message as stored.
 * A refusal throws {@link FormMessageRejectedError}.
 */
export async function saveFormMessage(
  formSpecId: string,
  messageKey: string,
  input: SaveFormMessageInput,
): Promise<FormMessageDetail> {
  const res = await fetch(messageUrl(formSpecId, messageKey), {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok) {
    throw new FormMessageRejectedError(res.status, await readValidationErrors(res));
  }
  return (await res.json()).payload;
}
