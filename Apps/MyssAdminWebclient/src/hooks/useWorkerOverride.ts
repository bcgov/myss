import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ApplicationRequestError,
  type ApplicationStatus,
} from "@myss/web-common/api";

import {
  getWorkerOverride,
  listWorkerOverrideDrafts,
  saveWorkerOverrideAnswers,
  startWorkerOverride,
  submitWorkerOverride,
  type WorkerOverrideAnswersInput,
  type WorkerOverrideApplicationPayload,
} from "@/api/workerOverride";
import { REVIEW_QUERY_KEY } from "@/hooks/useReview";

// React-query hooks for worker override (MYSS-321). Raw fetches, as
// useReview: every call carries authHeaders() itself.

export { WORKER_OVERRIDE_KEYWORDS } from "@/api/workerOverride";
export type {
  WorkerOverrideAnswersInput,
  WorkerOverrideApplicationPayload,
  WorkerOverrideDraftSummaryPayload,
} from "@/api/workerOverride";

export const WORKER_OVERRIDE_QUERY_KEY = ["intake", "worker-override"] as const;

/** The draft list; beside the per-application keys, never one of them. */
export const WORKER_OVERRIDE_DRAFTS_KEY = [
  ...WORKER_OVERRIDE_QUERY_KEY,
  "drafts",
] as const;

export function workerOverrideKey(
  id: string,
): readonly [string, string, string] {
  return [...WORKER_OVERRIDE_QUERY_KEY, id];
}

/** A draft, or one returned for more information: a worker may change it. */
export function isEditableStatus(status: ApplicationStatus): boolean {
  return status === "DRAFT" || status === "RETURNED";
}

// A 4xx (404 for a missing application, 403 for a non-worker) is a stable
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

/** Every worker's unsubmitted drafts (MYSS-322), most recently saved first. */
export function useWorkerOverrideDrafts() {
  return useQuery({
    queryKey: WORKER_OVERRIDE_DRAFTS_KEY,
    queryFn: listWorkerOverrideDrafts,
    retry: shouldRetry,
  });
}

/** Starts a draft. The caller navigates to it. */
export function useStartWorkerOverride() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: startWorkerOverride,
    onSuccess: (started) => {
      queryClient.setQueryData(workerOverrideKey(started.id), started);
      void queryClient.invalidateQueries({
        queryKey: WORKER_OVERRIDE_DRAFTS_KEY,
      });
    },
  });
}

/** One worker override application with the spec that renders it. */
export function useWorkerOverride(id: string) {
  return useQuery({
    queryKey: workerOverrideKey(id),
    queryFn: () => getWorkerOverride(id),
    retry: shouldRetry,
  });
}

/**
 * Folds a save or submit response into the cached application straight away
 * (status and the new row version, so a second click cannot go out with the
 * old one), keeping the spec the read carried, which these responses do not
 * repeat, and refreshes the draft list (a save moves the draft up it; a submit
 * takes it off). On a 409 the application is refetched so the worker sees
 * where it is now.
 */
function useWorkerOverrideWrite(
  id: string,
  write: (
    input: WorkerOverrideAnswersInput,
  ) => Promise<WorkerOverrideApplicationPayload>,
  afterSuccess?: () => void,
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: write,
    onSuccess: (updated) => {
      queryClient.setQueryData<WorkerOverrideApplicationPayload>(
        workerOverrideKey(id),
        (old) =>
          old
            ? { ...old, ...updated, spec: updated.spec ?? old.spec }
            : updated,
      );
      void queryClient.invalidateQueries({
        queryKey: WORKER_OVERRIDE_DRAFTS_KEY,
      });
      afterSuccess?.();
    },
    onError: (error) => {
      if (error instanceof ApplicationRequestError && error.isConflict) {
        void queryClient.invalidateQueries({ queryKey: workerOverrideKey(id) });
      }
    },
  });
}

/** Saves a draft's answers, complete or not. */
export function useSaveWorkerOverride(id: string) {
  return useWorkerOverrideWrite(id, (input) =>
    saveWorkerOverrideAnswers(id, input),
  );
}

/** Submits: the review queue gains the application. */
export function useSubmitWorkerOverride(id: string) {
  const queryClient = useQueryClient();
  return useWorkerOverrideWrite(
    id,
    (input) => submitWorkerOverride(id, input),
    () => void queryClient.invalidateQueries({ queryKey: REVIEW_QUERY_KEY }),
  );
}
