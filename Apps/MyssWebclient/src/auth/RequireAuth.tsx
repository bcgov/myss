// PERMANENT (depends only on useSession). Route guard: show a spinner while the
// session resolves, the sign-in chooser when signed out, otherwise the page.

import { useEffect, useState, type ReactNode } from "react";

import { useSession } from "./useSession";
import SignInChooser from "./SignInChooser";
import { isLogoutInProgress } from "./siteMinderLogout";

export default function RequireAuth({ children }: { children: ReactNode }) {
    const { isAuthenticated, isLoading } = useSession();
    const [signedOutSettled, setSignedOutSettled] = useState(false);

    // Reset the grace period when the auth library starts resolving again.
    // Done during render (React's "adjust state when an input changes" pattern)
    // rather than synchronously inside an effect, which would trigger a
    // cascading render.
    const [wasLoading, setWasLoading] = useState(isLoading);
    if (isLoading !== wasLoading) {
        setWasLoading(isLoading);
        if (isLoading) setSignedOutSettled(false);
    }

    // Reset the grace period during render when the auth library starts
    // resolving again, rather than in the effect below: setState in an effect
    // body is a cascading render. React re-runs this component immediately with
    // the new state and never commits the stale UI.
    const [wasLoading, setWasLoading] = useState(isLoading);
    if (wasLoading !== isLoading) {
        setWasLoading(isLoading);
        if (isLoading) {
            setSignedOutSettled(false);
        }
    }

    // The effect now owns only the timer — a genuine external system, and the
    // setState happens in its callback rather than synchronously in the body.
    useEffect(() => {
        // No need to delay rendering while resolving, or once we already know
        // we’re authenticated.
        if (isLoading || isAuthenticated) {
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
