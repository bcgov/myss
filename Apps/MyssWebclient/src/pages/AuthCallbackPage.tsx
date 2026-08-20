// Option-1 needs this route (Option 2 does not — the API owns the callback).
// react-oidc-context processes the ?code&state automatically on mount; this
// page just shows a spinner and navigates on once the session resolves.

import { useEffect } from "react";
import { useNavigate } from "react-router";
import { useAuth } from "react-oidc-context";

import { resolveReturnTo } from "@/auth/resolveReturnTo";

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
