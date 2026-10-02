import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  ApplicationRequestError,
  createApplication,
  getApplication,
  listApplications,
  saveAnswers,
  submitApplication,
  type AnswersInput,
  type ApplicationPayload,
} from "@/api/intake";

// React-query hooks for the citizen's Income Assistance applications. Raw
// fetches for now (see api/intake.ts), so every call carries authHeaders()
// itself; the Bearer interceptor in useApiAuth only wraps the generated client.

export { ApplicationRequestError, INTAKE_KEYWORDS } from "@/api/intake";
export type {
  ApplicationPayload,
  ApplicationStatus,
  ApplicationSummaryPayload,
} from "@/api/intake";

// Invalidation PREFIX: invalidateQueries({ queryKey: INTAKE_QUERY_KEY })
// matches the list and every application read by id, which is what every
// write wants — a save or submit changes the row the list shows too.
export const INTAKE_QUERY_KEY = ["intake", "applications"] as const;

export function applicationKey(id: string): readonly [string, string, string] {
  return [...INTAKE_QUERY_KEY, id];
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

/** One application with the archived spec it renders under. */
export function useApplication(id: string) {
  return useQuery({
    queryKey: applicationKey(id),
    queryFn: () => getApplication(id),
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

/**
 * Folds a write's response into the cached application straight away — the
 * new row version most of all, so a second save cannot go out with the old
 * one while the refetch is still in flight — keeping the archived spec the
 * read by id carried, which write responses do not repeat.
 */
function mergeIntoCache(
  queryClient: ReturnType<typeof useQueryClient>,
  id: string,
  updated: ApplicationPayload,
) {
  queryClient.setQueryData<ApplicationPayload>(applicationKey(id), (old) =>
    old ? { ...old, ...updated, spec: updated.spec ?? old.spec } : updated,
  );
}

/** Saves the draft's working copy. On a 409 the cached application is
 * refetched so the next attempt carries the version the row is at now. */
export function useSaveAnswers(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: AnswersInput) => saveAnswers(id, input),
    onSuccess: (updated) => {
      mergeIntoCache(queryClient, id, updated);
      void queryClient.invalidateQueries({ queryKey: INTAKE_QUERY_KEY });
    },
    onError: (error) => {
      if (error instanceof ApplicationRequestError && error.isConflict) {
        void queryClient.invalidateQueries({ queryKey: applicationKey(id) });
      }
    },
  });
}

/** Submits the application. The merged response already says SUBMITTED, so
 * the form switches to read-only before the refetch lands. */
export function useSubmitApplication(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: AnswersInput) => submitApplication(id, input),
    onSuccess: (updated) => {
      mergeIntoCache(queryClient, id, updated);
      void queryClient.invalidateQueries({ queryKey: INTAKE_QUERY_KEY });
    },
    onError: (error) => {
      if (error instanceof ApplicationRequestError && error.isConflict) {
        void queryClient.invalidateQueries({ queryKey: applicationKey(id) });
      }
    },
  });
}
