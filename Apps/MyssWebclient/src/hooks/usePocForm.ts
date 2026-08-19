import { useMutation, useQuery } from "@tanstack/react-query";

import {
  getFormSpec,
  getSubmission,
  listSubmissions,
  submitForm,
} from "@/api/forms";

// These endpoints are not in the generated client yet. Once the forms API
// stabilises, regenerate the schema and replace these fetches with the SDK.
//
// Because these are raw fetches, the Bearer interceptor in useApiAuth does not
// apply to them — it only wraps the generated client. FormsController is
// [Authorize], so every call here must carry authHeaders() explicitly or the
// API answers 401. Drop the authHeaders() spreads when these move onto the SDK.

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

export type {
  FormSpecPayload,
  FormSubmissionPayload,
  FormSubmissionSummary,
} from "@/api/forms";

export function useFormSpec(formSpecId: string) {
  return useQuery({
    queryKey: ["form-spec", formSpecId],
    queryFn: () => getFormSpec(formSpecId),
  });
}

export function useSubmissions(formSpecId: string) {
  return useQuery({
    queryKey: ["form-submissions", formSpecId],
    queryFn: () => listSubmissions(formSpecId),
  });
}

export function useSubmission(id: string) {
  return useQuery({
    queryKey: ["form-submission", id],
    queryFn: () => getSubmission(id),
  });
}

export function useSubmitForm(formSpecId: string) {
  return useMutation({
    mutationFn: (input: {
      formSpecVersion: number;
      answers: Record<string, unknown>;
    }): Promise<FormSubmissionPayload> => {
      const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/submissions`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...authHeaders() },
        body: JSON.stringify(input),
      });
      if (!res.ok) {
        throw new SubmissionRejectedError(res.status, await readValidationErrors(res));
      }

      return (await res.json()).payload;
    },
  });
}
