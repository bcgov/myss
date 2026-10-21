// The sign-in plumbing both webclients share. Each app keeps a thin module
// that binds its own paths, API address and session to these.

export { authHeaders, getAccessToken, setAccessToken } from "./accessToken";
export {
  fetchMe,
  HttpError,
  ME_QUERY_KEY,
  meQueryKey,
  shouldRetryMe,
  useMeQuery,
  type MePayloadBase,
} from "./me";
export { buildOidcConfig, currentOrigin, type OidcRuntime } from "./oidc";
export { resolveReturnTo } from "./resolveReturnTo";
export {
  buildKeycloakLogoutUrl,
  buildSiteMinderLogoutUrl,
  isLogoutInProgress,
  siteMinderLogout,
} from "./siteMinderLogout";
export {
  IDLE_MS,
  IdleTimer,
  useIdleTimeout,
  WARNING_MS,
  type IdleTimerOptions,
} from "./useIdleTimeout";
