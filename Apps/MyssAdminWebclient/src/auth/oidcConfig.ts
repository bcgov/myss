// OIDC client settings for react-oidc-context, built by @myss/web-common's
// buildOidcConfig from this deployment's settings, as the citizen webclient's
// are. The differences: the single IDIR hint below, and sign-out landing on
// the signed-out page.

import type { AuthProviderProps } from "react-oidc-context";
import { buildOidcConfig, currentOrigin } from "@myss/web-common/auth";

import { OIDC_AUTHORITY, OIDC_CLIENT_ID } from "@/constants";
import { paths } from "@/routes/paths";

// The kc_idp_hint for IDIR. The admin app signs in through IDIR only, so the
// hint always sends the user straight to it, past Keycloak's provider chooser.
// `azureidir` is the alias the CSS integration enables (the dev realm's chooser
// lists it as "IDIR - MFA", 2026-10-02).
export const IDIR_IDP_HINT = "azureidir";

// Sign-out lands on the unguarded signed-out page, never a guarded one (see
// paths.signedOut).
export const oidcConfig: AuthProviderProps = buildOidcConfig(
  {
    authority: OIDC_AUTHORITY,
    clientId: OIDC_CLIENT_ID,
    origin: currentOrigin(),
  },
  { postLogoutPath: paths.signedOut },
);
