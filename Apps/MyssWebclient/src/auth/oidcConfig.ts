// Option-1-ONLY. OIDC client settings for react-oidc-context, built by
// @myss/web-common's buildOidcConfig from this deployment's settings. This
// whole file is deleted when moving to Option 2 (the API owns the flow there).

import type { AuthProviderProps } from "react-oidc-context";
import { buildOidcConfig, currentOrigin } from "@myss/web-common/auth";

import { OIDC_AUTHORITY, OIDC_CLIENT_ID } from "@/constants";

// Friendly idp name -> Keycloak kc_idp_hint alias.
// The aliases are the identity providers enabled on the CSS integration, as
// the dev realm's own chooser lists them (2026-10-02): `bceidboth` (Basic and
// Business BCeID on one provider; the API's RoleCalculator decides what to do
// with a Business GUID) and BC Services Card. IDIR (`azureidir`) is enabled on
// the integration too, but staff sign in on the admin site, so this app never
// hints it. That last one has no fixed alias: CSS creates one BC Services Card
// broker per integration and names it after the integration's client id, so
// the hint is the client id itself (SSO docs, "Do skip the Keycloak default
// login page"). It is therefore built from the resolved runtime below rather
// than written here. A wrong hint is not fatal: Keycloak ignores it and shows
// its own provider chooser, so sign-in still works, just with one extra
// screen. If the CSS integration changes (e.g. to Basic-only BCeID, alias
// `bceidbasic`), change the alias here and in the API's IdentityProviders
// together.
export function buildIdpAliases(clientId: string) {
  return {
    bceid: "bceidboth",
    bcServicesCard: clientId,
  } as const;
}

export type IdpName = keyof ReturnType<typeof buildIdpAliases>;

// Sign-out returns to the landing page (the origin itself).
export const oidcConfig: AuthProviderProps = buildOidcConfig({
  authority: OIDC_AUTHORITY,
  clientId: OIDC_CLIENT_ID,
  origin: currentOrigin(),
});

// The hint aliases for this deployment's client (see buildIdpAliases).
export const IDP_ALIAS = buildIdpAliases(OIDC_CLIENT_ID);
