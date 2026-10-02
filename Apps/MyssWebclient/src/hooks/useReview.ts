import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { ApplicationRequestError } from "@/api/intake";
import {
  getReviewApplication,
  listReviewQueue,
  performReviewAction,
  type ReviewActionRoute,
  type ReviewApplicationPayload,
} from "@/api/review";

// React-query hooks for the worker's review of submitted applications. Raw
// fetches for now (see api/review.ts), so every call carries authHeaders()
// itself; the Bearer interceptor in useApiAuth only wraps the generated client.

export { REVIEW_KEYWORDS } from "@/api/review";
export type {
  ReviewAction,
  ReviewActionRoute,
  ReviewApplicationPayload,
  ReviewApplicationSummaryPayload,
} from "@/api/review";

// Invalidation PREFIX: invalidateQueries({ queryKey: REVIEW_QUERY_KEY })
// matches the queue and every application read by id, which is what every
// action wants — a decision changes the row the queue shows too.
export const REVIEW_QUERY_KEY = ["intake", "review"] as const;

export function reviewApplicationKey(
  id: string,
): readonly [string, string, string] {
  return [...REVIEW_QUERY_KEY, id];
}

// A 4xx (404 for a draft or a missing file, 403 for a non-worker) is a stable
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

/** Every submitted application, newest submission first. */
export function useReviewQueue() {
  return useQuery({
    queryKey: REVIEW_QUERY_KEY,
    queryFn: listReviewQueue,
    retry: shouldRetry,
  });
}

/** One submitted application with its archived spec and available actions. */
export function useReviewApplication(id: string) {
  return useQuery({
    queryKey: reviewApplicationKey(id),
    queryFn: () => getReviewApplication(id),
    retry: shouldRetry,
  });
}

/** The input of one review action. */
export interface ReviewActionInput {
  action: ReviewActionRoute;
  streamVersion: number;
}

/**
 * Moves a file. On success the response is folded into the cached application
 * straight away — status, available actions and the new stream version, so a
 * second click cannot go out with the old one while the refetch is in flight —
 * keeping the archived spec the read by id carried, which action responses do
 * not repeat. On a 409 the cached application is refetched so the worker sees
 * what the file is at now.
 */
export function useReviewAction(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ action, streamVersion }: ReviewActionInput) =>
      performReviewAction(id, action, streamVersion),
    onSuccess: (updated) => {
      queryClient.setQueryData<ReviewApplicationPayload>(
        reviewApplicationKey(id),
        (old) =>
          old
            ? { ...old, ...updated, spec: updated.spec ?? old.spec }
            : updated,
      );
      void queryClient.invalidateQueries({ queryKey: REVIEW_QUERY_KEY });
    },
    onError: (error) => {
      if (error instanceof ApplicationRequestError && error.isConflict) {
        void queryClient.invalidateQueries({
          queryKey: reviewApplicationKey(id),
        });
      }
    },
  });
}
