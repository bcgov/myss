// THE stable seam (plan §3.4). This interface is PERMANENT: every component
// depends only on it, so no component knows whether Option 1 (in-browser PKCE)
// or Option 2 (server session) is live. Only the *body* below changes for
// Option 2 (re-backed by a /auth/me query + window.location navigations).

import { useAuth } from "react-oidc-context";
import type { AuthContextProps } from "react-oidc-context";

import { normalizeUser, type CurrentUser } from "./currentUser";
import { IDP_ALIAS, type IdpName } from "./oidcConfig";
import { siteMinderLogout } from "./siteMinderLogout";

export interface Session {
    user?: CurrentUser;
    isAuthenticated: boolean;
    isLoading: boolean;
    login: (idp: IdpName) => void;
    logout: () => void;
}

// Pure shaper (unit-testable without React). Maps react-oidc-context's auth
// state onto the stable Session shape and wires login/logout.
export function buildSession(
    auth: AuthContextProps,
    logout: () => void,
): Session {
    return {
        user: auth.user ? normalizeUser(auth.user.profile) : undefined,
        isAuthenticated: auth.isAuthenticated,
        isLoading: auth.isLoading,
        login: (idp) =>
            auth.signinRedirect({
                extraQueryParams: { kc_idp_hint: IDP_ALIAS[idp] },
            }),
        logout,
    };
}

// Option-1 body: back the seam with react-oidc-context.
export function useSession(): Session {
    const auth = useAuth();
    return buildSession(auth, () => void siteMinderLogout(auth));
}
