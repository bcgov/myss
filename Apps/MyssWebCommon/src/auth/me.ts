// GET /v1/auth/me — the server-computed caller identity. Roles here are the
// API's EFFECTIVE roles (RoleCalculator, ADR-0007); the SPA never derives
// roles from token claims, because the browser cannot see the server's derive
// switch today, nor MySS account state once the APPLICANT/CLIENT split lands.
// Both APIs answer it; MyssApi adds the citizen-profile fields, so each
// webclient declares its own payload on top of MePayloadBase.
//
// A raw fetch with authHeaders() rather than a generated client: only the
// citizen app generates one (it has getMe), and this is shared with the admin
// app, which does not.

import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { authHeaders } from "./accessToken";

/** The fields both APIs' `CurrentUser` carry. */
export interface MePayloadBase {
  isAuthenticated: boolean;
  subject: string;
  roles: string[];
  bceidGuid?: string | null;
  idirUsername?: string | null;
}

/**
 * Error carrying the HTTP status, so the retry policy (shouldRetryMe) can tell
 * a non-transient 4xx from a transient network/server failure.
 */
export class HttpError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "HttpError";
    this.status = status;
  }
}

/** Reads /v1/auth/me from the API at `apiUrl` and unwraps the payload. */
export async function fetchMe<T extends MePayloadBase>(
  apiUrl: string,
): Promise<T> {
  const res = await fetch(`${apiUrl}/v1/auth/me`, {
    headers: { ...authHeaders() },
  });
  if (!res.ok) {
    throw new HttpError(res.status, `GET /v1/auth/me failed: ${res.status}`);
  }
  const body = (await res.json()) as { payload: T };
  return body.payload;
}

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

/** The /auth/me query, read with the app's `fetchMe`. */
export function useMeQuery<T>(
  enabled: boolean,
  subject: string | undefined,
  queryFn: () => Promise<T>,
): UseQueryResult<T> {
  return useQuery({
    queryKey: meQueryKey(subject),
    queryFn,
    enabled,
    staleTime: ME_STALE_TIME_MS,
    retry: shouldRetryMe,
  });
}
