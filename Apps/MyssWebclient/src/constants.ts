declare global {
    interface Window {
        APP_CONFIG?: {
            MYSS_API_URL?: string;
            OIDC_AUTHORITY?: string;
            OIDC_CLIENT_ID?: string;
        };
    }
}

// Guard `window` so this module is safe to import under the node test
// environment (and any SSR context), where `window` is undefined.
const appConfig =
    typeof window !== "undefined" ? window.APP_CONFIG : undefined;

// Resolution order (same pattern for every runtime value):
//   - window.APP_CONFIG.* written to /config.js at container startup by entrypoint.sh
//   - import.meta.env.VITE_* (local-dev fallback read from .env by Vite at build time)
//   - a safe default (tests / bare local run).
export const API_URL: string =
    appConfig?.MYSS_API_URL ||
    import.meta.env.VITE_MYSS_API_URL ||
    "http://localhost:5000";

// OIDC (Option 1: SPA Auth Code + PKCE). Injected at container start for
// deployed environments; VITE_* for local vite dev. Consumed by src/auth/oidcConfig.ts.
export const OIDC_AUTHORITY: string =
    appConfig?.OIDC_AUTHORITY ||
    import.meta.env.VITE_OIDC_AUTHORITY ||
    "https://dev.loginproxy.gov.bc.ca/auth/realms/standard";

export const OIDC_CLIENT_ID: string =
    appConfig?.OIDC_CLIENT_ID ||
    import.meta.env.VITE_OIDC_CLIENT_ID ||
    "sdpr-my-ss-6498";
