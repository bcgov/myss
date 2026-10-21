import { readAppConfig, resolveRuntimeConfig } from "@myss/web-common/config";

// Resolved like the citizen webclient's settings (see
// @myss/web-common/config): APP_CONFIG from /config.js, then VITE_*, then a
// default. The API is MyssAdminApi, not the citizen MyssApi; its key keeps
// the citizen app's name (MYSS_API_URL) so both images are configured the
// same way.
const config = resolveRuntimeConfig({
    appConfig: readAppConfig(),
    viteEnv: import.meta.env,
    defaultApiUrl: "http://localhost:5010",
});

export const API_URL: string = config.apiUrl;

// OIDC (SPA Auth Code + PKCE). Consumed by src/auth/oidcConfig.ts.
export const OIDC_AUTHORITY: string = config.oidcAuthority;
export const OIDC_CLIENT_ID: string = config.oidcClientId;

// The form editor's preview renders the address autocomplete, which calls
// AddressComplete from the browser. Public by design; protect it in Canada
// Post with allowed URLs and a daily usage cap. Empty disables suggestions.
export const CANADA_POST_API_KEY: string = config.canadaPostApiKey;
