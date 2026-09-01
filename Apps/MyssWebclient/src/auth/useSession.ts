// THE stable seam (plan §3.4). This interface is PERMANENT: every component
// depends only on it, so no component knows whether Option 1 (in-browser PKCE)
// or Option 2 (server session) is live. Only the *body* below changes for
// Option 2 (re-backed by the /auth/me query alone + window.location
// navigations).

import { useAuth } from "react-oidc-context";
import type { AuthContextProps } from "react-oidc-context";

import type { MePayload } from "@/api/me";
import { normalizeUser, type CurrentUser } from "./currentUser";
import { IDP_ALIAS, type IdpName } from "./oidcConfig";
import { siteMinderLogout } from "./siteMinderLogout";
import { useMe } from "./useMe";

export interface Session {
  user?: CurrentUser;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (idp: IdpName) => void;
  logout: () => void;
}

// Pure shaper (unit-testable without React). Composes the two halves of the
// caller's identity: display fields from the id token, roles from the server's
// /auth/me response — the API's effective roles (RoleCalculator, ADR-0007),
// which the browser cannot compute. While the me query is still pending the
// session reports loading, so role-gated rendering never flashes a wrong nav;
// if the query errors, roles stay [] and the UI fails closed.
export function buildSession(
  auth: AuthContextProps,
  logout: () => void,
  me?: MePayload,
  isMeLoading = false,
): Session {
  return {
    user: auth.user
      ? { ...normalizeUser(auth.user.profile), roles: me?.roles ?? [] }
      : undefined,
    isAuthenticated: auth.isAuthenticated,
    isLoading: auth.isLoading || (auth.isAuthenticated && isMeLoading),
    login: (idp) =>
      auth.signinRedirect({
        extraQueryParams: { kc_idp_hint: IDP_ALIAS[idp] },
      }),
    logout,
  };
}

// Option-1 body: back the seam with react-oidc-context plus the me query.
export function useSession(): Session {
  const auth = useAuth();
  // A disabled query (signed out) reports pending forever; buildSession only
  // treats pending as loading while actually authenticated.
  const me = useMe(auth.isAuthenticated, auth.user?.profile.sub);
  return buildSession(
    auth,
    () => void siteMinderLogout(auth),
    me.data,
    me.isPending,
  );
}
