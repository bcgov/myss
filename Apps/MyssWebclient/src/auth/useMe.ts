// The /auth/me session query. Kept out of useSession.ts so the stable seam
// file stays a pure shape (buildSession) plus thin wiring.

import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { fetchMe, type MePayload } from "@/api/me";

export const ME_QUERY_KEY = ["auth", "me"] as const;

// Effective roles change at exactly one moment — promotion — so a session
// rarely needs a refetch. 5 minutes keeps the window small until the
// Identity domain lands and promotion invalidates ME_QUERY_KEY explicitly.
const ME_STALE_TIME_MS = 5 * 60 * 1000;

export function useMe(enabled: boolean): UseQueryResult<MePayload> {
  return useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: fetchMe,
    enabled,
    staleTime: ME_STALE_TIME_MS,
  });
}
