// The session seam every admin component depends on. Unlike the citizen
// webclient there is one identity provider (IDIR) and no /auth/me query: the
// only thing the app gates on is the IDIR username, which the token carries.

import { useAuth } from "react-oidc-context";
import type { AuthContextProps } from "react-oidc-context";

import { normalizeUser, type CurrentUser } from "./currentUser";
import { IDIR_IDP_HINT } from "./oidcConfig";
import { siteMinderLogout } from "./siteMinderLogout";

export interface Session {
  user?: CurrentUser;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (returnTo?: string) => void;
  logout: () => void;
}

// Pure shaper (unit-testable without React).
export function buildSession(
  auth: AuthContextProps,
  logout: () => void,
): Session {
  return {
    user: auth.user ? normalizeUser(auth.user.profile) : undefined,
    isAuthenticated: auth.isAuthenticated,
    isLoading: auth.isLoading,
    login: (returnTo) =>
      auth.signinRedirect({
        extraQueryParams: { kc_idp_hint: IDIR_IDP_HINT },
        ...(returnTo ? { state: { returnTo } } : {}),
      }),
    logout,
  };
}

export function useSession(): Session {
  const auth = useAuth();
  return buildSession(auth, () => void siteMinderLogout(auth));
}
