import { useEffect, type ReactNode } from "react";
import { Button } from "@bcgov/design-system-react-components";
import { Outlet, useNavigate } from "react-router";

import { getDashboardAccess } from "@/auth/dashboardAccess";
import { useSession } from "@/auth/useSession";
import { usePhoneLayout } from "@/hooks/usePhoneLayout";
import { paths } from "@/routes/paths";
import DashboardFooter from "@/widgets/layout/DashboardFooter";
import DashboardNav from "@/widgets/layout/DashboardNav";
import styles from "./DashboardLayout.module.css";

// The signed-in dashboard frame: the left-hand menu beside the current
// section, or on a phone the section alone, its menu opened from the
// header. Every section route renders into the <Outlet />, so the landing
// checks below cover all of them, not just Home.
export default function DashboardLayout() {
  const session = useSession();
  const access = getDashboardAccess(session);
  const isPhoneLayout = usePhoneLayout();
  const navigate = useNavigate();

  // A citizen without a registered profile is sent to registration. The API
  // refuses to create an application for them too; this is the courtesy.
  useEffect(() => {
    if (access === "unregistered") {
      void navigate(paths.register, { replace: true });
    }
  }, [access, navigate]);

  if (access === "staff") {
    return (
      <DashboardContent>
        <div role="alert">
          <p>
            My Self Serve is for citizens. Ministry staff use My Self Serve
            Administration, signed in with IDIR.
          </p>
          <Button variant="secondary" onPress={() => session.logout()}>
            Sign out
          </Button>
        </div>
      </DashboardContent>
    );
  }

  // Hold the menu back until the user is known to belong here, so an
  // unregistered citizen never glimpses it on the way past.
  if (access !== "ready") {
    return (
      <DashboardContent>
        <output>Checking your MySS account…</output>
      </DashboardContent>
    );
  }

  return (
    <div className={styles.layout}>
      {!isPhoneLayout && <DashboardNav />}
      <DashboardContent>
        <Outlet />
      </DashboardContent>
    </div>
  );
}

// The content column: what the frame shows, then the copyright line, which
// sits at the bottom of the window when the content is short.
function DashboardContent({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <div className={styles.content}>
      <div className={styles.section}>{children}</div>
      <DashboardFooter />
    </div>
  );
}
