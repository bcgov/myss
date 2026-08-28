import type {
  FormType,
  JSON as FormioJson,
} from "@formio/react/lib/components/Form";

import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";

// Calls to the forms API (/v1/forms): fetch form specs, list and read
// submissions, and post new ones. Responses come wrapped in the API's
// payload envelope.
//
// These endpoints are not in the generated client yet, hence the raw fetches
// with authHeaders(). Swap for the SDK after regenerating the schema.

export interface FormSpecPayload {
  formSpecId: string;
  version: number;
  title?: string | null;
  spec: FormType;
}

export interface FormSubmissionPayload {
  id: string;
  formSpecId: string;
  formSpecVersion: number;
  answers: { [key: string]: FormioJson };
  submittedAt: string;
  spec?: FormSpecPayload | null;
}

export interface FormSubmissionSummary {
  id: string;
  formSpecId: string;
  formSpecVersion: number;
  submittedAt: string;
}

/**
 * One field-scoped refusal from the API. Mirrors `ValidationErrorModel` in
 * MyssApi/Models/FormValidationModels.cs.
 *
 * `keyword` is the stable half of the contract (`IDA.SIN.INVALID_CHECKSUM` and
 * friends) and `message` the human half. Key UI decisions off the keyword, not
 * the message — the message is destined to be sourced from the content engine
 * and translated, so its wording will move.
 */
export interface FormValidationError {
  field: string;
  keyword: string;
  message: string;
}

/**
 * A submission the API refused, carrying every reason it gave.
 *
 * The endpoint answers 422 with the FULL error collection precisely so the
 * client can build a WCAG error summary in one pass rather than surfacing
 * faults one at a time. Collapsing that body into a bare status string throws
 * away the whole point, so it is preserved here.
 *
 * `errors` is empty for non-422 failures (401, 500, a proxy error page), where
 * there is no collection to parse — callers should handle that case rather
 * than assuming a non-empty list.
 */
export class SubmissionRejectedError extends Error {
  readonly status: number;
  readonly errors: readonly FormValidationError[];

  constructor(status: number, errors: readonly FormValidationError[]) {
    super(
      errors.length > 0
        ? errors.map((error) => error.message).join(" ")
        : `Submission failed (${status})`,
    );
    this.name = "SubmissionRejectedError";
    this.status = status;
    this.errors = errors;

    // Subclassing a built-in loses the prototype link when TypeScript downlevels
    // the class, which silently breaks `instanceof`. Restoring it explicitly
    // keeps the check in PocForm correct regardless of compile target.
    Object.setPrototypeOf(this, SubmissionRejectedError.prototype);
  }
}

function isValidationError(value: unknown): value is FormValidationError {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.field === "string" &&
    typeof candidate.keyword === "string" &&
    typeof candidate.message === "string"
  );
}

/**
 * Pulls the error collection out of a failed response, tolerating anything
 * that is not the shape we expect. A failure to parse must never mask the
 * original failure with a JSON error, so every fault here degrades to an
 * empty list and lets the status speak for itself.
 */
async function readValidationErrors(res: Response): Promise<FormValidationError[]> {
  let body: unknown;
  try {
    body = await res.json();
  } catch {
    return [];
  }

  const payload = (body as { payload?: unknown } | null)?.payload;
  if (!Array.isArray(payload)) return [];

  return payload.filter(isValidationError);
}

/** The latest published spec for a form. */
export async function getFormSpec(formSpecId: string): Promise<FormSpecPayload> {
  const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/spec`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`Spec fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/** A form's submissions, newest first (metadata only). */
export async function listSubmissions(
  formSpecId: string,
): Promise<FormSubmissionSummary[]> {
  const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/submissions`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`Submissions fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/** One submission with the archived spec version that rendered it. */
export async function getSubmission(
  id: string,
): Promise<FormSubmissionPayload> {
  const res = await fetch(`${API_URL}/v1/forms/submissions/${id}`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`Submission fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/** Stores a submission stamped with the spec version it was rendered with. */
export async function submitForm(
  formSpecId: string,
  input: { formSpecVersion: number; answers: Record<string, unknown> },
): Promise<FormSubmissionPayload> {
  const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/submissions`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok)
    throw new SubmissionRejectedError(res.status, await readValidationErrors(res));
  return (await res.json()).payload;
}
