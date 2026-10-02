// Option-1-ONLY. OIDC client settings for react-oidc-context. Everything the
// SPA needs to run the Authorization Code + PKCE dance itself. This whole file
// is deleted when moving to Option 2 (the API owns the flow there).

import { WebStorageStateStore } from "oidc-client-ts";
import type { AuthProviderProps } from "react-oidc-context";

import { OIDC_AUTHORITY, OIDC_CLIENT_ID } from "@/constants";

// Friendly idp name -> Keycloak kc_idp_hint alias.
// The aliases are the identity providers enabled on the CSS integration, as a
// real dev token showed them on 2026-09-09: `bceidboth` (Basic and Business
// BCeID on one provider; the API's RoleCalculator decides what to do with a
// Business GUID) and `azureidir`, the Azure-backed IDIR. `bcservicescard` is
// assumed; the client does not have it yet. A wrong hint is not fatal: Keycloak
// ignores it and shows its own provider chooser, so sign-in still works, just
// with one extra screen. If the CSS integration changes (e.g. to Basic-only
// BCeID, alias `bceidbasic`), change the alias here and in the API's
// IdentityProviders together.
export const IDP_ALIAS = {
  bceid: "bceidboth",
  bcServicesCard: "bcservicescard",
  idir: "azureidir",
} as const;

export type IdpName = keyof typeof IDP_ALIAS;

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
    post_logout_redirect_uri: rt.origin,
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

export const oidcConfig: AuthProviderProps = buildOidcConfig(
  resolveOidcRuntime({
    appConfig: typeof window !== "undefined" ? window.APP_CONFIG : undefined,
    viteEnv: {
      VITE_OIDC_AUTHORITY: import.meta.env.VITE_OIDC_AUTHORITY,
      VITE_OIDC_CLIENT_ID: import.meta.env.VITE_OIDC_CLIENT_ID,
    },
    origin,
  }),
);
