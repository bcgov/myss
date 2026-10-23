import { readAppConfig, resolveRuntimeConfig } from "@myss/web-common/config";

// The runtime settings, resolved the same way in both webclients (see
// @myss/web-common/config): window.APP_CONFIG written to /config.js at
// container start by entrypoint.sh, then the VITE_* values Vite reads from
// .env for a local run, then a safe default (tests, a bare local run).
const config = resolveRuntimeConfig({
    appConfig: readAppConfig(),
    viteEnv: import.meta.env,
    defaultApiUrl: "http://localhost:5000",
});

export const API_URL: string = config.apiUrl;

// OIDC (Option 1: SPA Auth Code + PKCE). Consumed by src/auth/oidcConfig.ts.
export const OIDC_AUTHORITY: string = config.oidcAuthority;
export const OIDC_CLIENT_ID: string = config.oidcClientId;

// AddressComplete is called directly from the browser. This key is therefore
// public by design and must be protected in Canada Post with allowed URLs and
// a daily usage cap.
export const CANADA_POST_API_KEY: string = config.canadaPostApiKey;
