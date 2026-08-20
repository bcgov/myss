// Where to land after the OIDC callback.
//
// Lives in its own module rather than beside AuthCallbackPage for two reasons:
// Fast Refresh only hot-swaps modules whose exports are all components, so a
// page exporting a helper alongside its component forces a full reload
// (react-refresh/only-export-components); and this is the open-redirect guard,
// which deserves direct unit tests rather than being reachable only through a
// rendered page.

import { paths } from "@/routes/paths";

/**
 * Resolves the post-callback destination from the OIDC `state`.
 *
 * Callers may pass `state: { returnTo }` to `signinRedirect` to come back to
 * the page they started from; anything else — including the plain sign-in
 * chooser, which passes no state — goes home.
 *
 * Only same-site absolute paths are honoured. `state` survives a round trip
 * through the identity provider and is therefore attacker-influenceable, so a
 * value that is not a rooted path is discarded rather than trusted:
 *
 *   - must start with "/", which rejects "https://evil.example" and "evil.com"
 *   - must NOT start with "//", which browsers read as protocol-relative and
 *     would navigate off-site despite passing the first check
 */
export function resolveReturnTo(state: unknown): string {
    const returnTo = (state as { returnTo?: unknown } | undefined)?.returnTo;
    if (
        typeof returnTo === "string" &&
        returnTo.startsWith("/") &&
        !returnTo.startsWith("//")
    ) {
        return returnTo;
    }
    return paths.home;
}
