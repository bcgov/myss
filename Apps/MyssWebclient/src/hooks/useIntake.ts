import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  ApplicationRequestError,
  createAfaApplication,
  createApplication,
  getAfaApplication,
  getApplication,
  isAfaPublished,
  listAfaApplications,
  listApplications,
  saveAnswers,
  submitApplication,
  type AnswersInput,
  type ApplicationPayload,
} from "@/api/intake";

// React-query hooks for the citizen's Income Assistance applications. Raw
// fetches for now (see api/intake.ts), so every call carries authHeaders()
// itself; the Bearer interceptor in useApiAuth only wraps the generated client.

export {
  AFA_FORM_SPEC_ID,
  ApplicationRequestError,
  INTAKE_KEYWORDS,
  isEditableStatus,
} from "@/api/intake";
export type {
  ApplicationPayload,
  ApplicationStatus,
  ApplicationSummaryPayload,
} from "@/api/intake";

// Invalidation PREFIX: invalidateQueries({ queryKey: INTAKE_QUERY_KEY })
// matches the list and every application read by id, which is what every
// write wants — a save or submit changes the row the list shows too.
export const INTAKE_QUERY_KEY = ["intake", "applications"] as const;
const AFA_LIST_KEY = [...INTAKE_QUERY_KEY, "afa"] as const;
const AFA_PUBLISHED_KEY = ["intake", "afa-published"] as const;

export function applicationKey(id: string, afa = false) {
  return afa
    ? ([...INTAKE_QUERY_KEY, id, "afa"] as const)
    : ([...INTAKE_QUERY_KEY, id] as const);
}

// A 4xx (404 for a missing or someone else's application, 401) is a stable
// answer, not a transient fault; retrying only delays the message.
function shouldRetry(failureCount: number, error: Error): boolean {
  if (
    error instanceof ApplicationRequestError &&
    error.status >= 400 &&
    error.status < 500
  ) {
    return false;
  }
  return failureCount < 2;
}

/** The caller's applications, newest first. */
export function useApplications() {
  return useQuery({
    queryKey: INTAKE_QUERY_KEY,
    queryFn: listApplications,
    retry: shouldRetry,
  });
}

/** The AFA form's published status; 404 means unpublished, other faults are errors. */
export function useAfaPublished() {
  return useQuery({
    queryKey: AFA_PUBLISHED_KEY,
    queryFn: isAfaPublished,
    retry: shouldRetry,
  });
}

/** The caller's AFA service requests only. */
export function useAfaApplications() {
  return useQuery({
    queryKey: AFA_LIST_KEY,
    queryFn: listAfaApplications,
    retry: shouldRetry,
  });
}

/** One application with the archived spec it renders under. */
export function useApplication(id: string, afa = false) {
  return useQuery({
    queryKey: applicationKey(id, afa),
    queryFn: () => (afa ? getAfaApplication(id) : getApplication(id)),
    retry: shouldRetry,
  });
}

/** Creates a new draft. Refreshes the list so it shows up there. */
export function useCreateApplication() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createApplication,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: INTAKE_QUERY_KEY });
    },
  });
}

/** Starts AFA and refreshes the Service Requests list. */
export function useCreateAfaApplication() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createAfaApplication,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: AFA_LIST_KEY });
    },
  });
}

/**
 * Folds a write's response into the cached application straight away — the
 * new row version most of all, so a second save cannot go out with the old
 * one while the refetch is still in flight — keeping the archived spec the
 * read by id carried, which write responses do not repeat.
 */
function mergeIntoCache(
  queryClient: ReturnType<typeof useQueryClient>,
  id: string,
  afa: boolean,
  updated: ApplicationPayload,
) {
  queryClient.setQueryData<ApplicationPayload>(
    applicationKey(id, afa),
    (old) =>
      old ? { ...old, ...updated, spec: updated.spec ?? old.spec } : updated,
  );
}

/** Saves the draft's working copy. On a 409 the cached application is
 * refetched so the next attempt carries the version the row is at now. */
export function useSaveAnswers(id: string, afa = false) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: AnswersInput) => saveAnswers(id, input),
    onSuccess: (updated) => {
      mergeIntoCache(queryClient, id, afa, updated);
      void queryClient.invalidateQueries({ queryKey: INTAKE_QUERY_KEY });
    },
    onError: (error) => {
      if (error instanceof ApplicationRequestError && error.isConflict) {
        void queryClient.invalidateQueries({
          queryKey: applicationKey(id, afa),
        });
      }
    },
  });
}

/** Submits the application. The merged response already says SUBMITTED, so
 * the form switches to read-only before the refetch lands. */
export function useSubmitApplication(id: string, afa = false) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: AnswersInput) => submitApplication(id, input),
    onSuccess: (updated) => {
      mergeIntoCache(queryClient, id, afa, updated);
      void queryClient.invalidateQueries({ queryKey: INTAKE_QUERY_KEY });
    },
    onError: (error) => {
      if (error instanceof ApplicationRequestError && error.isConflict) {
        void queryClient.invalidateQueries({
          queryKey: applicationKey(id, afa),
        });
      }
    },
  });
}
