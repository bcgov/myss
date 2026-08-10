// Option-1 needs this route (Option 2 does not — the API owns the callback).
// react-oidc-context processes the ?code&state automatically on mount; this
// page just shows a spinner and navigates on once the session resolves.

import { useEffect } from "react";
import { useNavigate } from "react-router";
import { useAuth } from "react-oidc-context";

import { paths } from "@/routes/paths";

// Where to land after the callback. Callers may pass `state: { returnTo }` to
// signinRedirect to come back to the page they started from; anything else
// (including the plain sign-in chooser, which passes no state) goes home.
// Only same-site absolute paths are honoured, so a tampered state cannot turn
// the callback into an open redirect.
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

export default function AuthCallbackPage() {
    const auth = useAuth();
    const navigate = useNavigate();

    useEffect(() => {
        if (!auth.isLoading && (auth.isAuthenticated || auth.error)) {
            navigate(resolveReturnTo(auth.user?.state), { replace: true });
        }
    }, [
        auth.isLoading,
        auth.isAuthenticated,
        auth.error,
        auth.user,
        navigate,
    ]);

    if (auth.error) {
        return (
            <p role="alert">
                Sign-in could not be completed. Please try again.
            </p>
        );
    }

    return (
        <p role="status" aria-live="polite">
            Signing you in…
        </p>
    );
}
