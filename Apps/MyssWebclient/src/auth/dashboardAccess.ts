import type { Session } from "./useSession";

/**
 * Where a signed-in user stands with the dashboard: still being looked up,
 * Ministry staff, a citizen without a registered profile, or a citizen who
 * belongs here.
 */
export type DashboardAccess = "checking" | "staff" | "unregistered" | "ready";

// Ministry staff work on the admin site, not here. The sign-in chooser no
// longer offers IDIR, but an IDIR session can still arrive (Keycloak's own
// provider page, or an existing IDIR SSO session on the shared client), and
// staff have no citizen profile, so staff is decided before registration.
export function getDashboardAccess(
  session: Pick<Session, "user" | "isMeLoading" | "hasProfile">,
): DashboardAccess {
  if (session.isMeLoading) return "checking";
  if (session.user?.idirUsername) return "staff";
  if (session.hasProfile === false) return "unregistered";
  if (session.hasProfile === true) return "ready";
  return "checking";
}
