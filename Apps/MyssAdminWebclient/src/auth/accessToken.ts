// The current API access token, mirrored out of React by useApiAuth.
//
// This lives in its own module rather than inside useApiAuth so that call sites
// using raw `fetch` can attach the token without importing react-oidc-context
// (and therefore without needing an <AuthProvider> above them in tests). Only
// useApiAuth writes to it; everyone else reads.
//
// Option 2 (server session + cookies) deletes this file along with useApiAuth.

let currentToken: string | undefined;

/** Records the freshest access token. Called by useApiAuth only. */
export function setAccessToken(token: string | undefined): void {
    currentToken = token;
}

/** The freshest access token, or undefined when signed out. */
export function getAccessToken(): string | undefined {
    return currentToken;
}

/**
 * Authorization header for call sites that use raw `fetch` instead of the
 * generated client (see src/hooks/usePocForm.ts).
 *
 * Returns an empty object when signed out rather than throwing, so the request
 * still goes out and the API's 401 stays the single source of truth about
 * whether the session is good.
 */
export function authHeaders(): Record<string, string> {
    return currentToken ? { Authorization: `Bearer ${currentToken}` } : {};
}
