import { useEffect } from "react";
import { Outlet, useNavigate } from "react-router";

import { WORKER_ROLE } from "@/auth/RequireWorker";
import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import DashboardNav from "@/widgets/layout/DashboardNav";
import styles from "./DashboardLayout.module.css";

// The signed-in dashboard frame (MYSS-194): the left-hand menu beside the
// current section. Every section route renders into the <Outlet />, so the
// landing checks below cover all of them, not just Home.
export default function DashboardLayout() {
  const { hasProfile, isMeLoading, user } = useSession();
  const navigate = useNavigate();
  // Roles come from /auth/me, so this is only known once that has answered.
  const isWorker =
    !isMeLoading &&
    Boolean(user?.idirUsername) &&
    (user?.roles ?? []).includes(WORKER_ROLE);

  // A Ministry worker has no citizen profile: their landing is the worker
  // operations view, decided before the registration check below can fire.
  // A citizen without a registered profile is sent to registration. The API
  // refuses to create an application for them too; this is the courtesy.
  useEffect(() => {
    if (isWorker) {
      void navigate(paths.workerApplications, { replace: true });
    } else if (!isMeLoading && hasProfile === false) {
      void navigate(paths.register, { replace: true });
    }
  }, [hasProfile, isMeLoading, isWorker, navigate]);

  // Hold the menu back until the user is known to belong here, so a worker
  // or an unregistered citizen never glimpses it on the way past.
  if (isMeLoading || isWorker || hasProfile !== true) {
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
