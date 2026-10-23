import type { JSON as FormioJson } from "@formio/react/lib/components/Form";
import {
  readValidationErrors,
  SubmissionRejectedError,
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
// The spec and validation-error shapes, the 422 reader and the refused-
// submission error are shared with
// the admin webclient (@myss/web-common/api); re-exported here for this app's
// callers.

export {
  readValidationErrors,
  SubmissionRejectedError,
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
