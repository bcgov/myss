// Option-1 needs this route (Option 2 does not — the API owns the callback).
// react-oidc-context processes the ?code&state automatically on mount; this
// page just shows a spinner and navigates on once the session resolves: to the
// page the user started from, or to the app's `fallbackPath`. A refused
// sign-in goes there too: a silent one (`prompt=none`) is refused whenever the
// identity provider has no session, and the page's own guard then offers the
// chooser. The library keeps the request's state on the error for this.

import { useEffect } from "react";
import { useNavigate } from "react-router";
import { useAuth } from "react-oidc-context";

import { resolveReturnTo } from "../auth/resolveReturnTo";

export default function AuthCallbackPage({
  fallbackPath,
}: Readonly<{ fallbackPath: string }>) {
  const auth = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!auth.isLoading && (auth.isAuthenticated || auth.error)) {
      const state =
        auth.user?.state ??
        (auth.error as { state?: unknown } | undefined)?.state;
      void navigate(resolveReturnTo(state, fallbackPath), {
        replace: true,
      });
    }
  }, [
    auth.isLoading,
    auth.isAuthenticated,
    auth.error,
    auth.user,
    navigate,
    fallbackPath,
  ]);

  if (auth.error) {
    return (
      <p role="alert">Sign-in could not be completed. Please try again.</p>
    );
  }

  return (
    <p role="status" aria-live="polite">
      Signing you in…
    </p>
  );
}
