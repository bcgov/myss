import type { UserManagerSettings } from "oidc-client-ts";
import { describe, it, expect } from "vitest";

import { OIDC_AUTHORITY, OIDC_CLIENT_ID } from "@/constants";
import { IDIR_IDP_HINT, oidcConfig } from "./oidcConfig";

// buildOidcConfig itself is tested in @myss/web-common; these pin what this
// app passes it.
describe("oidcConfig", () => {
  const settings = oidcConfig as UserManagerSettings;

  it("uses this deployment's realm and client", () => {
    expect(settings.authority).toBe(OIDC_AUTHORITY);
    expect(settings.client_id).toBe(OIDC_CLIENT_ID);
  });

  // A guarded page would start IDIR sign-in again at once (see
  // paths.signedOut).
  it("lands on the unguarded signed-out page after sign-out", () => {
    expect(settings.post_logout_redirect_uri).toBe(
      "http://localhost/signed-out",
    );
  });
});

describe("IDIR_IDP_HINT", () => {
  // Pinned to the IDIR provider enabled on the CSS integration. A change here
  // is a change to the integration, not a typo fix.
  it("is the Azure-backed IDIR alias", () => {
    expect(IDIR_IDP_HINT).toBe("azureidir");
  });
});
