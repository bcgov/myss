import type { ReactNode } from "react";
import { Link } from "react-router";

import { paths } from "@/routes/paths";
import { useRoles } from "./useRoles";

/**
 * Gates the application review screens, inside RequireIdir (which owns
 * sign-in): the caller's effective roles, as MyssAdminApi computed them for
 * /auth/me, must include WORKER or ADMIN, the same rule as the API's
 * WorkerWithIdir policy. The roles arrive after authentication, so this waits
 * for them and fails closed. A signed-in IDIR user without the role gets an
 * explanation, not a redirect: the landing page is theirs too.
 */
export default function RequireWorker({
    children,
}: Readonly<{ children: ReactNode }>) {
    const { isLoading, canReview } = useRoles();

    if (isLoading) {
        return (
            <p role="status" aria-live="polite">
                Loading…
            </p>
        );
    }

    if (!canReview) {
        return (
            <div role="alert">
                <p>
                    Reviewing applications needs the Ministry worker role on
                    your IDIR account.
                </p>
                <Link to={paths.home}>Back to Administration</Link>
            </div>
        );
    }

    return <>{children}</>;
}
