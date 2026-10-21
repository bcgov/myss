import { describe, it, expect } from "vitest";

import { buildOidcConfig, currentOrigin } from "./oidc";

const runtime = {
  authority: "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
  clientId: "sdpr-my-ss-6498",
  origin: "http://localhost:5173",
};

describe("buildOidcConfig", () => {
  const cfg = buildOidcConfig(runtime);

  it("uses authorization code flow with the right scopes", () => {
    expect(cfg.authority).toBe(
      "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
    );
    expect(cfg.client_id).toBe("sdpr-my-ss-6498");
    expect(cfg.response_type).toBe("code");
    expect(cfg.scope).toContain("openid");
  });

  it("derives redirect + post-logout URIs from the origin", () => {
    expect(cfg.redirect_uri).toBe("http://localhost:5173/auth/callback");
    expect(cfg.post_logout_redirect_uri).toBe("http://localhost:5173");
  });

  it("appends a post-logout path when one is given", () => {
    expect(
      buildOidcConfig(runtime, { postLogoutPath: "/signed-out" })
        .post_logout_redirect_uri,
    ).toBe("http://localhost:5173/signed-out");
  });

  it("enables automatic silent renew", () => {
    expect(cfg.automaticSilentRenew).toBe(true);
  });

  it("builds no session store outside a browser", () => {
    expect(cfg.userStore).toBeUndefined();
  });
});

describe("currentOrigin", () => {
  it("is a fixed stand-in where there is no window", () => {
    expect(currentOrigin()).toBe("http://localhost");
  });
});
