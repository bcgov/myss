import type { JSON as FormioJson } from "@formio/react/lib/components/Form";
import {
  readValidationErrors,
  type FormSpecPayload,
  type FormValidationError,
} from "@myss/web-common/api";
import { authHeaders } from "@myss/web-common/auth";

import { API_URL } from "@/constants";

// Calls to the forms API (/v1/forms): fetch form specs and post submissions.
// Responses come wrapped in the API's payload envelope. The staff calls (the
// editor's list, drafts and publish; listing and reading submissions) live in
// MyssAdminWebclient and go to MyssAdminApi.
//
// These endpoints are not in the generated client yet, hence the raw fetches
// with authHeaders(). Swap for the SDK after regenerating the schema.
//
// The spec and validation-error shapes, and the 422 reader, are shared with
// the admin webclient (@myss/web-common/api); re-exported here for this app's
// callers.

export {
  readValidationErrors,
  type FormSpecPayload,
  type FormValidationError,
};

export interface FormSubmissionPayload {
  id: string;
  formSpecId: string;
  formSpecVersion: number;
  answers: { [key: string]: FormioJson };
  submittedAt: string;
  spec?: FormSpecPayload | null;
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

/** The latest published spec for a form. */
export async function getFormSpec(formSpecId: string): Promise<FormSpecPayload> {
  const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/spec`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`Spec fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/** Stores a submission stamped with the spec version it was rendered with. */
/**
 * A submission. `pin` and `pinConfirmation` are registration only (MYSS-258)
 * and travel beside the answers, never inside them: the API stores answers as
 * submitted and keeps only the PIN's salted hash.
 */
export interface FormSubmissionInput {
  formSpecVersion: number;
  answers: Record<string, unknown>;
  pin?: string;
  pinConfirmation?: string;
}

export async function submitForm(
  formSpecId: string,
  input: FormSubmissionInput,
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
