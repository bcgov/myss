import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";

import {
  FormLoadError,
  getDraft,
  getFormVersion,
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

/** Every form with its versions, read now and without retries, as a function.
 * On success the result becomes the forms list's cached data; a failure
 * rejects and leaves the cached list and its error state as they were. */
export function useFreshFormList() {
  const queryClient = useQueryClient();
  return useCallback(async () => {
    const forms = await listForms();
    // A background read that started earlier must not replace this one.
    await queryClient.cancelQueries({ queryKey: ["admin-forms"] });
    queryClient.setQueryData(["admin-forms"], forms);
    return forms;
  }, [queryClient]);
}

// A 404 is a stable answer (the form or version does not exist), not a
// transient fault; retrying it only delays the not-found paths.
function retryUnlessNotFound(failureCount: number, error: Error): boolean {
  return (
    !(error instanceof FormLoadError && error.status === 404) &&
    failureCount < 3
  );
}

/** The spec to open in the editor: the in-progress draft, or the latest
 * published version as the starting point for a new draft. */
export function useDraft(formSpecId: string | undefined) {
  return useQuery({
    queryKey: ["admin-draft", formSpecId],
    queryFn: () => getDraft(formSpecId!),
    enabled: !!formSpecId,
    retry: retryUnlessNotFound,
  });
}

/** One version of a form, published or draft. Nothing is kept between mounts
 * (`gcTime: 0`): a draft can change between two openings, so each one reads it
 * again rather than showing an earlier copy. */
export function useFormVersion(
  formSpecId: string | undefined,
  version: number | null | undefined,
) {
  return useQuery({
    queryKey: ["admin-form-version", formSpecId, version],
    queryFn: () => getFormVersion(formSpecId!, version!),
    enabled: !!formSpecId && version != null,
    retry: retryUnlessNotFound,
    gcTime: 0,
  });
}

/** Save the edited spec as a draft. Refreshes the draft and the forms list so
 * a new draft version shows up in both. */
export function useSaveDraft(formSpecId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: SaveDraftInput) => saveDraft(formSpecId!, input),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ["admin-draft", formSpecId],
      });
      void queryClient.invalidateQueries({ queryKey: ["admin-forms"] });
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
      void queryClient.invalidateQueries({
        queryKey: ["admin-draft", formSpecId],
      });
      void queryClient.invalidateQueries({ queryKey: ["admin-forms"] });
    },
  });
}
