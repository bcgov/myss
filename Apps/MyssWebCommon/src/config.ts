// Runtime settings both webclients read the same way. Each deployed image
// writes window.APP_CONFIG to /config.js at container start (entrypoint.sh);
// a local `vite dev` run reads VITE_* from .env instead. The two apps differ
// only in their default API address, so each passes its own.

/** What entrypoint.sh writes to window.APP_CONFIG. */
export interface AppConfig {
  MYSS_API_URL?: string;
  OIDC_AUTHORITY?: string;
  OIDC_CLIENT_ID?: string;
  CANADA_POST_API_KEY?: string;
}

declare global {
  interface Window {
    APP_CONFIG?: AppConfig;
  }
}

/**
 * import.meta.env, from which the VITE_* keys are read: VITE_MYSS_API_URL,
 * VITE_OIDC_AUTHORITY, VITE_OIDC_CLIENT_ID and VITE_CANADA_POST_API_KEY. The
 * app passes it in, so this package never reads import.meta.env itself.
 */
export type ViteEnv = Readonly<Record<string, unknown>>;

/** A set, non-empty string value, or undefined. */
function text(value: unknown): string | undefined {
  return typeof value === "string" && value !== "" ? value : undefined;
}

/** The resolved settings. */
export interface RuntimeConfig {
  apiUrl: string;
  oidcAuthority: string;
  oidcClientId: string;
  /**
   * AddressComplete is called directly from the browser. This key is
   * therefore public by design and must be protected in Canada Post with
   * allowed URLs and a daily usage cap. Empty disables suggestions.
   */
  canadaPostApiKey: string;
}

export const DEFAULT_OIDC_AUTHORITY =
  "https://dev.loginproxy.gov.bc.ca/auth/realms/standard";
export const DEFAULT_OIDC_CLIENT_ID = "sdpr-my-ss-6498";

/**
 * window.APP_CONFIG, or undefined where there is no `window` (the node test
 * environment, any SSR context), so importing a caller there is safe.
 */
export function readAppConfig(): AppConfig | undefined {
  return typeof window !== "undefined" ? window.APP_CONFIG : undefined;
}

/**
 * Resolution order, the same for every value: APP_CONFIG, then the VITE_*
 * value, then a safe default (tests, a bare local run).
 */
export function resolveRuntimeConfig(sources: {
  appConfig?: AppConfig;
  viteEnv?: ViteEnv;
  defaultApiUrl: string;
}): RuntimeConfig {
  const { appConfig, viteEnv, defaultApiUrl } = sources;
  return {
    apiUrl:
      appConfig?.MYSS_API_URL ||
      text(viteEnv?.VITE_MYSS_API_URL) ||
      defaultApiUrl,
    oidcAuthority:
      appConfig?.OIDC_AUTHORITY ||
      text(viteEnv?.VITE_OIDC_AUTHORITY) ||
      DEFAULT_OIDC_AUTHORITY,
    oidcClientId:
      appConfig?.OIDC_CLIENT_ID ||
      text(viteEnv?.VITE_OIDC_CLIENT_ID) ||
      DEFAULT_OIDC_CLIENT_ID,
    canadaPostApiKey:
      appConfig?.CANADA_POST_API_KEY ||
      text(viteEnv?.VITE_CANADA_POST_API_KEY) ||
      "",
  };
}
