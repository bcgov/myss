// OIDC client settings for react-oidc-context: everything the SPA needs to run
// the Authorization Code + PKCE dance itself. Copied from the citizen
// webclient; the only difference is the single IDIR hint below.

import { WebStorageStateStore } from "oidc-client-ts";
import type { AuthProviderProps } from "react-oidc-context";

import { OIDC_AUTHORITY, OIDC_CLIENT_ID } from "@/constants";

// The kc_idp_hint for IDIR. The admin app signs in through IDIR only, so the
// hint always sends the user straight to it, past Keycloak's provider chooser.
// `azureidir` is the alias the CSS integration enables (the dev realm's chooser
// lists it as "IDIR - MFA", 2026-10-02); the citizen webclient uses the same one.
export const IDIR_IDP_HINT = "azureidir";

export interface OidcRuntime {
  authority: string;
  clientId: string;
  origin: string;
}

// Resolution order: runtime APP_CONFIG (nginx-injected) -> Vite env -> the
// resolved constants (which already carry a dev default). Pure + injectable so
// it is unit-testable without a browser.
export function resolveOidcRuntime(sources: {
  appConfig?: { OIDC_AUTHORITY?: string; OIDC_CLIENT_ID?: string };
  viteEnv?: { VITE_OIDC_AUTHORITY?: string; VITE_OIDC_CLIENT_ID?: string };
  origin: string;
}): OidcRuntime {
  const { appConfig, viteEnv, origin } = sources;
  return {
    authority:
      appConfig?.OIDC_AUTHORITY ||
      viteEnv?.VITE_OIDC_AUTHORITY ||
      OIDC_AUTHORITY,
    clientId:
      appConfig?.OIDC_CLIENT_ID ||
      viteEnv?.VITE_OIDC_CLIENT_ID ||
      OIDC_CLIENT_ID,
    origin,
  };
}

export function buildOidcConfig(rt: OidcRuntime) {
  return {
    authority: rt.authority,
    client_id: rt.clientId,
    redirect_uri: `${rt.origin}/auth/callback`,
    // The unguarded signed-out page, never a guarded one (see paths.signedOut).
    post_logout_redirect_uri: `${rt.origin}/signed-out`,
    response_type: "code", // Authorization Code + PKCE (PKCE is automatic)
    scope: "openid profile email",
    // Mirror the original's storage so a refresh keeps the session.
    // Guarded: outside a browser (node unit tests / SSR) there is no
    // sessionStorage, and WebStorageStateStore would fall back to
    // localStorage — also undefined there — throwing at construction. So we
    // only build the store when a real Storage exists. The browser always
    // has one, so runtime behaviour is unchanged.
    userStore:
      typeof window !== "undefined"
        ? new WebStorageStateStore({ store: window.sessionStorage })
        : undefined,
    automaticSilentRenew: true, // rotating refresh token, not a hidden iframe
    // Strip ?code&state from the URL after the callback completes.
    onSigninCallback: () =>
      window.history.replaceState({}, document.title, window.location.pathname),
  };
}

// Module-level config, guarded so importing it in a node test never touches a
// missing `window`. The real values come from constants.ts at runtime.
const origin =
  typeof window !== "undefined" ? window.location.origin : "http://localhost";

const runtime = resolveOidcRuntime({
  appConfig: typeof window !== "undefined" ? window.APP_CONFIG : undefined,
  viteEnv: {
    VITE_OIDC_AUTHORITY: import.meta.env.VITE_OIDC_AUTHORITY,
    VITE_OIDC_CLIENT_ID: import.meta.env.VITE_OIDC_CLIENT_ID,
  },
  origin,
});

export const oidcConfig: AuthProviderProps = buildOidcConfig(runtime);
