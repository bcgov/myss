import { describe, expect, it } from "vitest";

import {
  DEFAULT_OIDC_AUTHORITY,
  DEFAULT_OIDC_CLIENT_ID,
  readAppConfig,
  resolveRuntimeConfig,
} from "./config";

describe("resolveRuntimeConfig", () => {
  it("prefers the runtime APP_CONFIG over the Vite env", () => {
    const config = resolveRuntimeConfig({
      appConfig: {
        MYSS_API_URL: "https://runtime-api",
        OIDC_AUTHORITY: "https://runtime/realms/standard",
        OIDC_CLIENT_ID: "runtime-client",
        CANADA_POST_API_KEY: "runtime-key",
      },
      viteEnv: {
        VITE_MYSS_API_URL: "https://vite-api",
        VITE_OIDC_AUTHORITY: "https://vite/realms/standard",
        VITE_OIDC_CLIENT_ID: "vite-client",
        VITE_CANADA_POST_API_KEY: "vite-key",
      },
      defaultApiUrl: "http://localhost:5000",
    });

    expect(config).toEqual({
      apiUrl: "https://runtime-api",
      oidcAuthority: "https://runtime/realms/standard",
      oidcClientId: "runtime-client",
      canadaPostApiKey: "runtime-key",
    });
  });

  it("falls back to the Vite env when APP_CONFIG is empty", () => {
    const config = resolveRuntimeConfig({
      appConfig: {},
      viteEnv: {
        VITE_MYSS_API_URL: "https://vite-api",
        VITE_OIDC_AUTHORITY: "https://vite/realms/standard",
        VITE_OIDC_CLIENT_ID: "vite-client",
      },
      defaultApiUrl: "http://localhost:5000",
    });

    expect(config.apiUrl).toBe("https://vite-api");
    expect(config.oidcAuthority).toBe("https://vite/realms/standard");
    expect(config.oidcClientId).toBe("vite-client");
  });

  it("falls back to the caller's API default and the dev realm", () => {
    const config = resolveRuntimeConfig({
      defaultApiUrl: "http://localhost:5010",
    });

    expect(config.apiUrl).toBe("http://localhost:5010");
    expect(config.oidcAuthority).toBe(DEFAULT_OIDC_AUTHORITY);
    expect(config.oidcClientId).toBe(DEFAULT_OIDC_CLIENT_ID);
    expect(config.canadaPostApiKey).toBe("");
  });

  it("ignores Vite values that are empty or not text", () => {
    const config = resolveRuntimeConfig({
      viteEnv: { VITE_MYSS_API_URL: "", VITE_OIDC_CLIENT_ID: true },
      defaultApiUrl: "http://localhost:5000",
    });

    expect(config.apiUrl).toBe("http://localhost:5000");
    expect(config.oidcClientId).toBe(DEFAULT_OIDC_CLIENT_ID);
  });
});

describe("readAppConfig", () => {
  it("is undefined where there is no window", () => {
    expect(readAppConfig()).toBeUndefined();
  });
});
