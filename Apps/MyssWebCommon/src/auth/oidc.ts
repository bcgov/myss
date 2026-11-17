// OIDC client settings for react-oidc-context: everything a webclient needs to
// run the Authorization Code + PKCE flow itself (Option 1). Option 2 deletes
// this (the API owns the flow there).

import { WebStorageStateStore } from "oidc-client-ts";

/** The deployment's sign-in settings plus the page's origin. */
export interface OidcRuntime {
  authority: string;
  clientId: string;
  origin: string;
}

export function buildOidcConfig(
  rt: OidcRuntime,
  options: {
    /**
     * Where Keycloak sends the browser after sign-out, as a path on this
     * origin. Omitted, the origin itself.
     */
    postLogoutPath?: string;
  } = {},
) {
  return {
    authority: rt.authority,
    client_id: rt.clientId,
    redirect_uri: `${rt.origin}/auth/callback`,
    post_logout_redirect_uri: `${rt.origin}${options.postLogoutPath ?? ""}`,
    response_type: "code", // Authorization Code + PKCE (PKCE is automatic)
    scope: "openid profile email",
    // Mirror the original's storage so a refresh keeps the session.
    // Guarded: outside a browser (node unit tests / SSR) there is no
    // sessionStorage, and WebStorageStateStore would fall back to
    // localStorage — also undefined there — throwing at construction. So
    // we only build the store when a real Storage exists. The browser
    // always has one, so runtime behaviour is unchanged.
    userStore:
      typeof window !== "undefined"
        ? new WebStorageStateStore({ store: window.sessionStorage })
        : undefined,
    automaticSilentRenew: true, // rotating refresh token, not a hidden iframe
    // Strip ?code&state from the URL after the callback completes. The history
    // state is kept: React Router stores its position there, which Back needs.
    onSigninCallback: () =>
      window.history.replaceState(
        window.history.state,
        document.title,
        window.location.pathname,
      ),
  };
}

/** This page's origin; a fixed stand-in where there is no `window`. */
export function currentOrigin(): string {
  return typeof window !== "undefined"
    ? window.location.origin
    : "http://localhost";
}
