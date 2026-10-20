// The /auth/me session query. Kept out of useSession.ts so the stable seam
// file stays a pure shape (buildSession) plus thin wiring.

import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { fetchMe, HttpError, type MePayload } from "@/api/me";

// Invalidation PREFIX: invalidateQueries({ queryKey: ME_QUERY_KEY }) matches
// every subject's entry. The full key (meQueryKey) is scoped by subject so a
// cached payload can never be served to a different authenticated user —
// today every login/logout is a full-page navigation that discards the
// in-memory cache anyway, but the key makes that correctness structural
// rather than dependent on how the auth flows happen to navigate.
export const ME_QUERY_KEY = ["auth", "me"] as const;

// Effective roles change at exactly one moment — promotion — so a session
// rarely needs a refetch. 5 minutes keeps the window small until the
// Identity domain lands and promotion invalidates ME_QUERY_KEY explicitly.
const ME_STALE_TIME_MS = 5 * 60 * 1000;

export function meQueryKey(
  subject: string | undefined,
): readonly [string, string, string] {
  return [...ME_QUERY_KEY, subject ?? "anonymous"];
}

// A 4xx (expired token, misconfigured auth) is not transient: retrying it
// only prolongs the session's loading state and generates traffic, so fail
// closed immediately — refetchOnWindowFocus recovers later. Network errors
// and 5xx get two retries.
export function shouldRetryMe(failureCount: number, error: Error): boolean {
  if (error instanceof HttpError && error.status >= 400 && error.status < 500) {
    return false;
  }
  return failureCount < 2;
}

export function useMe(
  enabled: boolean,
  subject: string | undefined,
): UseQueryResult<MePayload> {
  return useQuery({
    queryKey: meQueryKey(subject),
    queryFn: fetchMe,
    enabled,
    staleTime: ME_STALE_TIME_MS,
    retry: shouldRetryMe,
  });
}
