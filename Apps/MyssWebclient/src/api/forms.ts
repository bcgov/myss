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
  if (!res.ok) throw new Error(`Submission failed (${res.status})`);
  return (await res.json()).payload;
}
