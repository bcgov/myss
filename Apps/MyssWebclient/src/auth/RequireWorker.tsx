import { useEffect, useRef, type ReactNode } from "react";
import { Navigate, useLocation } from "react-router";

import { paths } from "@/routes/paths";
import { useSession } from "./useSession";

/** The role the API grants Ministry staff (MyssRoles.Worker). */
export const WORKER_ROLE = "WORKER";

/**
 * Gates the worker operations view: an IDIR sign-in whose effective roles,
 * as the API computed them for /auth/me, include WORKER. The roles arrive
 * after authentication, so this waits for them and fails closed: a signed-in
 * user with no roles yet sees the loading status, not the worker view and not
 * a bounce to home.
 */
export default function RequireWorker({
  children,
}: Readonly<{ children: ReactNode }>) {
  const { user, isAuthenticated, isLoading, isMeLoading, login } = useSession();
  const location = useLocation();
  const loginStarted = useRef(false);
  const returnTo = `${location.pathname}${location.search}${location.hash}`;

  useEffect(() => {
    if (!isLoading && !isAuthenticated && !loginStarted.current) {
      loginStarted.current = true;
      login("idir", returnTo);
    }
  }, [isAuthenticated, isLoading, login, returnTo]);

  if (isLoading || (isAuthenticated && isMeLoading)) {
    return (
      <p role="status" aria-live="polite">
        Loading…
      </p>
    );
  }

  if (!isAuthenticated) {
    return (
      <p role="status" aria-live="polite">
        Redirecting to IDIR sign in…
      </p>
    );
  }

  if (!user?.idirUsername || !user.roles.includes(WORKER_ROLE)) {
    return <Navigate to={paths.home} replace />;
  }

  return <>{children}</>;
}
