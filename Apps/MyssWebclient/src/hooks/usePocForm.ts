import { useMutation, useQuery } from "@tanstack/react-query";

import {
  getFormSpec,
  getSubmission,
  listSubmissions,
  submitForm,
} from "@/api/forms";

// React-query hooks over the forms API: spec fetch, submission list and
// detail, and the submit mutation.

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
    }) => submitForm(formSpecId, input),
  });
}
