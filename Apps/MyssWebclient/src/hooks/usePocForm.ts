import { useMutation, useQuery } from "@tanstack/react-query";
import type {
  FormType,
  JSON as FormioJson,
} from "@formio/react/lib/components/Form";

import { API_URL } from "@/constants";

// These endpoints are not in the generated client yet. Once the forms API
// stabilises, regenerate the schema and replace these fetches with the SDK.

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

export function useFormSpec(formSpecId: string) {
  return useQuery({
    queryKey: ["form-spec", formSpecId],
    queryFn: async (): Promise<FormSpecPayload> => {
      const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/spec`);
      if (!res.ok) throw new Error(`Spec fetch failed (${res.status})`);
      return (await res.json()).payload;
    },
  });
}

export interface FormSubmissionSummary {
  id: string;
  formSpecId: string;
  formSpecVersion: number;
  submittedAt: string;
}

export function useSubmissions(formSpecId: string) {
  return useQuery({
    queryKey: ["form-submissions", formSpecId],
    queryFn: async (): Promise<FormSubmissionSummary[]> => {
      const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/submissions`);
      if (!res.ok) throw new Error(`Submissions fetch failed (${res.status})`);
      return (await res.json()).payload;
    },
  });
}

export function useSubmission(id: string) {
  return useQuery({
    queryKey: ["form-submission", id],
    queryFn: async (): Promise<FormSubmissionPayload> => {
      const res = await fetch(`${API_URL}/v1/forms/submissions/${id}`);
      if (!res.ok) throw new Error(`Submission fetch failed (${res.status})`);
      return (await res.json()).payload;
    },
  });
}

export function useSubmitForm(formSpecId: string) {
  return useMutation({
    mutationFn: async (input: {
      formSpecVersion: number;
      answers: Record<string, unknown>;
    }): Promise<FormSubmissionPayload> => {
      const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/submissions`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(input),
      });
      if (!res.ok) throw new Error(`Submission failed (${res.status})`);
      return (await res.json()).payload;
    },
  });
}
