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

export { SubmissionRejectedError } from "@/api/forms";
export type {
  FormValidationError,
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
    }) => submitForm(formSpecId, input),
  });
}
