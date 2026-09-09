import { useEffect, useRef, type ReactNode } from "react";
import { Navigate, useLocation } from "react-router";

import { paths } from "@/routes/paths";
import { useSession } from "./useSession";

export default function RequireIdir({
    children,
}: Readonly<{ children: ReactNode }>) {
    const { user, isAuthenticated, isLoading, login } = useSession();
    const location = useLocation();
    const loginStarted = useRef(false);
    const returnTo = `${location.pathname}${location.search}${location.hash}`;

    useEffect(() => {
        if (!isLoading && !isAuthenticated && !loginStarted.current) {
            loginStarted.current = true;
            login("idir", returnTo);
        }
    }, [isAuthenticated, isLoading, login, returnTo]);

    if (isLoading) {
        return (
            <p role="status" aria-live="polite">
                Loading…
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
        return <Navigate to={paths.home} replace />;
    }

    return <>{children}</>;
}
