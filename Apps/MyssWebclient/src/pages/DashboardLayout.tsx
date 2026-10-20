import { useEffect } from "react";
import { Button } from "@bcgov/design-system-react-components";
import { Outlet, useNavigate } from "react-router";

import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import DashboardNav from "@/widgets/layout/DashboardNav";
import styles from "./DashboardLayout.module.css";

// The signed-in dashboard frame (MYSS-194): the left-hand menu beside the
// current section. Every section route renders into the <Outlet />, so the
// landing checks below cover all of them, not just Home.
export default function DashboardLayout() {
  const { user, hasProfile, isMeLoading, logout } = useSession();
  const navigate = useNavigate();

  // Ministry staff work on the admin site, not here. The sign-in chooser no
  // longer offers IDIR, but an IDIR session can still arrive (Keycloak's own
  // provider page, or an existing IDIR SSO session on the shared client), and
  // staff have no citizen profile, so without this they would be sent to
  // citizen registration.
  const isStaff = Boolean(user?.idirUsername);

  // A citizen without a registered profile is sent to registration. The API
  // refuses to create an application for them too; this is the courtesy.
  useEffect(() => {
    if (!isMeLoading && !isStaff && hasProfile === false) {
      void navigate(paths.register, { replace: true });
    }
  }, [hasProfile, isMeLoading, isStaff, navigate]);

  if (!isMeLoading && isStaff) {
    return (
      <div className={styles.content}>
        <div role="alert">
          <p>
            My Self Serve is for citizens. Ministry staff use My Self Serve
            Administration, signed in with IDIR.
          </p>
          <Button variant="secondary" onPress={() => logout()}>
            Sign out
          </Button>
        </div>
      </div>
    );
  }

  // Hold the menu back until the user is known to belong here, so an
  // unregistered citizen never glimpses it on the way past.
  if (isMeLoading || hasProfile !== true) {
    return (
      <div className={styles.content}>
        <output>Checking your MySS account…</output>
      </div>
    );
  }

  return (
    <div className={styles.layout}>
      <DashboardNav />
      <div className={styles.content}>
        <Outlet />
      </div>
    </div>
  );
}
