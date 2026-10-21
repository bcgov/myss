// The /auth/me session query. Kept out of useSession.ts so the stable seam
// file stays a pure shape (buildSession) plus thin wiring. The query's key,
// caching and retry policy are @myss/web-common's.

import type { UseQueryResult } from "@tanstack/react-query";
import { useMeQuery } from "@myss/web-common/auth";

import { fetchMe, type MePayload } from "@/api/me";

export { ME_QUERY_KEY } from "@myss/web-common/auth";

export function useMe(
  enabled: boolean,
  subject: string | undefined,
): UseQueryResult<MePayload> {
  return useMeQuery(enabled, subject, fetchMe);
}
