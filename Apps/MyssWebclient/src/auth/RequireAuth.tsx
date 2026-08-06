// PERMANENT (depends only on useSession). Route guard: show a spinner while the
// session resolves, the sign-in chooser when signed out, otherwise the page.

import type { ReactNode } from "react";

import { useSession } from "./useSession";
import SignInChooser from "./SignInChooser";

export default function RequireAuth({ children }: { children: ReactNode }) {
    const { isAuthenticated, isLoading } = useSession();

    if (isLoading) {
        return (
            <p role="status" aria-live="polite">
                Loading…
            </p>
        );
    }

    if (!isAuthenticated) {
        return <SignInChooser />;
    }

    return <>{children}</>;
}
