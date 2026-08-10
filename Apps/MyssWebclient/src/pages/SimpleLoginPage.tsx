// A deliberately minimal login harness at paths.simpleLogin (/simplelogin).
//
// WHY THIS EXISTS: the home page will keep changing as the rebuild proceeds.
// This page is a stable, dependency-light way to exercise the real auth flow
// (Login -> Keycloak -> /auth/callback -> back here) and read the token that
// comes back, without touching HomePage/AccountPanel.
//
// It reuses the existing flow wholesale — AuthProvider, oidcConfig, the
// /auth/callback route and the useSession seam are all unchanged. Nothing is
// reimplemented here.

import { useEffect, useRef, useState } from "react";
import { Button } from "@bcgov/design-system-react-components";
import { useAuth } from "react-oidc-context";

import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import TokenDetails from "./TokenDetails";
import styles from "./SimpleLoginPage.module.css";

// How long to wait for Keycloak before assuming we cannot reach it.
// oidc-client-ts has no timeout on its metadata fetch, so if the discovery
// document never responds `signinRedirect()` simply never settles: no error,
// no redirect, and react-oidc-context leaves isLoading pinned true. That reads
// as "the button does nothing". This turns that silence into a message.
const REDIRECT_TIMEOUT_MS = 10_000;

export default function SimpleLoginPage() {
  // The permanent seam gives us auth state. Login/logout here are
  // intentionally simpler than the seam's (no kc_idp_hint, no SiteMinder
  // round trip), so we drive them off `useAuth` directly.
  const { isAuthenticated, isLoading } = useSession();

  // Option-1-ONLY. Login/logout are driven off `useAuth` directly (the token
  // itself is now inspected in the TokenDetails panel below).
  const auth = useAuth();

  const [stalled, setStalled] = useState(false);
  const timerRef = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(timerRef.current), []);

  // No kc_idp_hint: land on Keycloak's own IdP chooser. `state.returnTo`
  // brings us back to this page instead of home after the callback.
  //
  // On success the browser navigates away long before the timer fires, so
  // the timeout only ever surfaces when the redirect genuinely stalled.
  const handleLogin = () => {
    setStalled(false);
    window.clearTimeout(timerRef.current);
    timerRef.current = window.setTimeout(
      () => setStalled(true),
      REDIRECT_TIMEOUT_MS,
    );
    void auth
      .signinRedirect({ state: { returnTo: paths.simpleLogin } })
      .finally(() => window.clearTimeout(timerRef.current));
  };

  // Local sign-out only: clears the stored user so the token box empties and
  // we stay on this page. The Keycloak/SiteMinder SSO session survives, so
  // the next Login may not re-prompt for credentials. Use the sign-out on the
  // home page (siteMinderLogout) for a true end-to-end sign-out.
  const handleLogout = () => void auth.removeUser();

  return (
    <div className={styles.page}>
      <h1 className={styles.title}>Simple Login</h1>

      <div className={styles.actions}>
        <Button
          variant="primary"
          size="medium"
          onPress={handleLogin}
          // A stalled redirect leaves isLoading stuck true forever,
          // so re-enable the button once we've given up waiting.
          isDisabled={isLoading && !stalled}
        >
          Login
        </Button>
        <Button
          variant="secondary"
          size="medium"
          onPress={handleLogout}
          isDisabled={isLoading || !isAuthenticated}
        >
          Logout
        </Button>
      </div>

      {auth.error && (
        <p className={styles.error} role="alert">
          Sign-in failed: {auth.error.message}
        </p>
      )}

      {stalled && !auth.error && (
        <p className={styles.error} role="alert">
          No response from Keycloak after {REDIRECT_TIMEOUT_MS / 1000} seconds,
          so the redirect never started. The browser could not reach the
          identity provider &mdash; check whether a VPN or firewall is blocking{" "}
          <code>dev.loginproxy.gov.bc.ca</code>.
        </p>
      )}

      {!isAuthenticated && (
        <p className={styles.hint}>Sign in to retrieve and inspect a token.</p>
      )}

      {/* Decoded token inspector — only rendered once authenticated, so
          the unauthenticated page stays the minimal harness it was. */}
      {isAuthenticated && <TokenDetails />}
    </div>
  );
}
