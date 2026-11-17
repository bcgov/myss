import { useQuery } from "@tanstack/react-query";
import { ApplicationRequestError } from "@myss/web-common/api";

import { searchClients, type ClientSearchCriteria } from "@/api/clientSearch";

// The Support View client search (MYSS-275) as a query: one per set of
// criteria the worker has searched by, so going back to an earlier search
// shows it again at once.

export {
  CLIENT_SEARCH_KEYWORDS,
  type ClientSearchCriteria,
  type ClientSearchResultPayload,
  type ClientSignInType,
  type ClientSummaryPayload,
} from "@/api/clientSearch";

export const CLIENT_SEARCH_QUERY_KEY = ["support", "clients"] as const;

// A 4xx (400 for refused criteria, 403 for a non-worker) is a stable answer;
// retrying only delays the message.
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

/** Searches by the criteria; idle while they are null (nothing searched yet). */
export function useClientSearch(criteria: ClientSearchCriteria | null) {
  return useQuery({
    queryKey: [...CLIENT_SEARCH_QUERY_KEY, criteria],
    queryFn: () => searchClients(criteria!),
    enabled: criteria !== null,
    retry: shouldRetry,
    // Personal information: not kept once the worker has moved on.
    gcTime: 60_000,
  });
}
