import type { UserManagerSettings } from "oidc-client-ts";
import { describe, it, expect } from "vitest";

import { OIDC_AUTHORITY, OIDC_CLIENT_ID } from "@/constants";
import { buildIdpAliases, IDP_ALIAS, oidcConfig } from "./oidcConfig";

// buildOidcConfig itself is tested in @myss/web-common; these pin what this
// app passes it.
describe("oidcConfig", () => {
  const settings = oidcConfig as UserManagerSettings;

  it("uses this deployment's realm and client", () => {
    expect(settings.authority).toBe(OIDC_AUTHORITY);
    expect(settings.client_id).toBe(OIDC_CLIENT_ID);
  });

  it("returns to the landing page (the origin) after sign-out", () => {
    expect(settings.post_logout_redirect_uri).toBe("http://localhost");
  });
});

describe("IDP_ALIAS", () => {
  // Pinned to the providers enabled on the CSS integration (the dev realm's
  // chooser on 2026-10-02 listed bceidboth, azureidir and BC Services Card).
  // A change here is a change to the integration, not a typo fix. IDIR is
  // the admin site's; this app has no hint for it.
  it("maps friendly idp names to the hint aliases the CSS integration has", () => {
    const aliases = buildIdpAliases("sdpr-my-ss-6498");
    expect(aliases.bceid).toBe("bceidboth");
    expect(aliases).not.toHaveProperty("idir");
  });

  // CSS names each integration's BC Services Card broker after the client
  // id, so the hint must follow the client id, not a fixed string.
  it("hints BC Services Card with the client id", () => {
    expect(buildIdpAliases("sdpr-my-ss-6498").bcServicesCard).toBe(
      "sdpr-my-ss-6498",
    );
    expect(buildIdpAliases("other-client").bcServicesCard).toBe("other-client");
  });

  it("builds the module-level aliases from the resolved client id", () => {
    expect(IDP_ALIAS.bcServicesCard).toBe(OIDC_CLIENT_ID);
  });
});
