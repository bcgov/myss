import { describe, it, expect } from "vitest";

import {
  resolveOidcRuntime,
  buildOidcConfig,
  IDIR_IDP_HINT,
} from "./oidcConfig";

describe("resolveOidcRuntime", () => {
  it("prefers runtime APP_CONFIG over Vite env", () => {
    const rt = resolveOidcRuntime({
      appConfig: {
        OIDC_AUTHORITY: "https://runtime/realms/standard",
        OIDC_CLIENT_ID: "runtime-client",
      },
      viteEnv: {
        VITE_OIDC_AUTHORITY: "https://vite/realms/standard",
        VITE_OIDC_CLIENT_ID: "vite-client",
      },
      origin: "http://localhost:5174",
    });
    expect(rt.authority).toBe("https://runtime/realms/standard");
    expect(rt.clientId).toBe("runtime-client");
  });

  it("falls back to Vite env when APP_CONFIG is empty", () => {
    const rt = resolveOidcRuntime({
      appConfig: {},
      viteEnv: {
        VITE_OIDC_AUTHORITY: "https://vite/realms/standard",
        VITE_OIDC_CLIENT_ID: "vite-client",
      },
      origin: "http://localhost:5174",
    });
    expect(rt.authority).toBe("https://vite/realms/standard");
    expect(rt.clientId).toBe("vite-client");
  });
});

describe("buildOidcConfig", () => {
  const cfg = buildOidcConfig({
    authority: "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
    clientId: "sdpr-my-ss-6498",
    origin: "http://localhost:5174",
  });

  it("uses authorization code flow with the right scopes", () => {
    expect(cfg.authority).toBe(
      "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
    );
    expect(cfg.client_id).toBe("sdpr-my-ss-6498");
    expect(cfg.response_type).toBe("code");
    expect(cfg.scope).toContain("openid");
  });

  it("derives redirect + post-logout URIs from the origin", () => {
    expect(cfg.redirect_uri).toBe("http://localhost:5174/auth/callback");
    expect(cfg.post_logout_redirect_uri).toBe(
      "http://localhost:5174/signed-out",
    );
  });

  it("enables automatic silent renew", () => {
    expect(cfg.automaticSilentRenew).toBe(true);
  });
});

describe("IDIR_IDP_HINT", () => {
  // Pinned to the IDIR provider enabled on the CSS integration. A change here
  // is a change to the integration, not a typo fix.
  it("is the Azure-backed IDIR alias", () => {
    expect(IDIR_IDP_HINT).toBe("azureidir");
  });
});
