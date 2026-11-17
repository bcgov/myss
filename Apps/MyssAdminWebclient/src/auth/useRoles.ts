// The caller's effective roles, as MyssAdminApi computed them for
// GET /v1/auth/me. Only the review screens need roles (form management needs
// IDIR alone), so the session seam stays token-only and this is asked for
// where it matters. Fails closed: no answer yet, or a failed one, is no roles.

import { useSession } from "./useSession";
import { useMe } from "./useMe";

/** The roles that may review applications, as the API's WorkerWithIdir policy has them. */
export const REVIEW_ROLES = ["WORKER", "ADMIN"] as const;

export interface Roles {
    roles: string[];
    /** True while a signed-in caller's roles are still being fetched. */
    isLoading: boolean;
    /** True when the caller holds a role that may review applications. */
    canReview: boolean;
}

export function useRoles(): Roles {
    const { isAuthenticated, user } = useSession();
    const me = useMe(isAuthenticated, user?.sub);
    const roles = me.data?.roles ?? [];
    return {
        roles,
        isLoading: isAuthenticated && me.isPending,
        canReview: roles.some((role) =>
            (REVIEW_ROLES as readonly string[]).includes(role),
        ),
    };
}
