// PERMANENT (depends only on useSession). Route guard: show a spinner while the
// session resolves, the sign-in chooser when signed out, otherwise the page.

import { useEffect, useState, type ReactNode } from "react";

import { useSession } from "./useSession";
import SignInChooser from "./SignInChooser";
import { isLogoutInProgress } from "./siteMinderLogout";

export default function RequireAuth({ children }: { children: ReactNode }) {
    const { isAuthenticated, isLoading } = useSession();
    const [signedOutSettled, setSignedOutSettled] = useState(false);

    useEffect(() => {
        // Reset when the auth library starts resolving again.
        if (isLoading) {
            setSignedOutSettled(false);
            return;
        }

        // No need to delay rendering when we already know we’re authenticated.
        if (isAuthenticated) {
            return;
        }

        const timer = window.setTimeout(() => setSignedOutSettled(true), 300);
        return () => window.clearTimeout(timer);
    }, [isLoading, isAuthenticated]);

    if (isLoading) {
        return (
            <p role="status" aria-live="polite">
                Loading…
            </p>
        );
    }

    if (isLogoutInProgress()) {
        return (
            <p role="status" aria-live="polite">
                Signing you out…
            </p>
        );
    }

    if (isAuthenticated) {
        return <>{children}</>;
    }

    if (!signedOutSettled) {
        return (
            <p role="status" aria-live="polite">
                Checking your session…
            </p>
        );
    }

    return <SignInChooser />;
}
