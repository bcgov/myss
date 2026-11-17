import { useEffect, useRef, type ReactNode } from "react";
import { Button } from "@bcgov/design-system-react-components";
import { useLocation } from "react-router";

import { isLogoutInProgress } from "./siteMinderLogout";
import { useSession } from "./useSession";

// Every admin page sits behind this guard. Signed out, it starts IDIR sign-in
// and comes back to the exact page asked for. Signed in without an IDIR
// identity, it explains instead of redirecting: the whole site is admin-only,
// so there is no public page to send the user to. While a logout is under way
// it starts nothing: logout clears the local user before it navigates away, and
// a sign-in started in that gap would replace the logout navigation.
export default function RequireIdir({
    children,
}: Readonly<{ children: ReactNode }>) {
    const { user, isAuthenticated, isLoading, login, logout } = useSession();
    const location = useLocation();
    const loginStarted = useRef(false);
    const returnTo = `${location.pathname}${location.search}${location.hash}`;

    useEffect(() => {
        if (
            !isLoading &&
            !isAuthenticated &&
            !loginStarted.current &&
            !isLogoutInProgress()
        ) {
            loginStarted.current = true;
            login(returnTo);
        }
    }, [isAuthenticated, isLoading, login, returnTo]);

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

    if (!isAuthenticated) {
        return (
            <p role="status" aria-live="polite">
                Redirecting to IDIR sign in…
            </p>
        );
    }

    if (!user?.idirUsername) {
        return (
            <div role="alert">
                <p>
                    My Self Serve Administration is for ministry staff signed
                    in with IDIR.
                </p>
                <Button variant="secondary" onPress={() => logout()}>
                    Sign out
                </Button>
            </div>
        );
    }

    return <>{children}</>;
}
