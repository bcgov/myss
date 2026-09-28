import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  FormLoadError,
  getDraft,
  listForms,
  publishForm,
  saveDraft,
} from "@/api/forms";
import type { SaveDraftInput } from "@/api/forms";

// React-query hooks for the IDIR-only admin form editor. Kept short-lived (no
// long staleTime) because saving a draft or publishing changes what these
// return, and the editor moves the user between the list and one form
// expecting to see the latest.

/** Every form with its versions and published/draft state (admin list). */
export function useAdminForms() {
  return useQuery({
    queryKey: ["admin-forms"],
    queryFn: listForms,
  });
}

/** The spec to open in the editor: the in-progress draft, or the latest
 * published version as the starting point for a new draft. */
export function useDraft(formSpecId: string | undefined) {
  return useQuery({
    queryKey: ["admin-draft", formSpecId],
    queryFn: () => getDraft(formSpecId!),
    enabled: !!formSpecId,
    // A 404 is a stable answer (the form does not exist), not a transient
    // fault; retrying it only delays the not-found and new-form paths.
    retry: (failureCount, error) =>
      !(error instanceof FormLoadError && error.status === 404) &&
      failureCount < 3,
  });
}

/** Save the edited spec as a draft. Refreshes the draft and the forms list so
 * a new draft version shows up in both. */
export function useSaveDraft(formSpecId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: SaveDraftInput) => saveDraft(formSpecId!, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-draft", formSpecId] });
      queryClient.invalidateQueries({ queryKey: ["admin-forms"] });
    },
  });
}

/** Publish the current draft as the next version. Refreshes the draft and the
 * forms list so the newly published version shows up in both. */
export function usePublishForm(formSpecId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => publishForm(formSpecId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-draft", formSpecId] });
      queryClient.invalidateQueries({ queryKey: ["admin-forms"] });
    },
  });
}
