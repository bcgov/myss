import { Button } from "@bcgov/design-system-react-components";
import { useEffect } from "react";
import { useNavigate } from "react-router";

import { WORKER_ROLE } from "@/auth/RequireWorker";
import { useSession } from "@/auth/useSession";
import { useCreateApplication } from "@/hooks/useIntake";
import { applicationPath, paths } from "@/routes/paths";
import ApplicationsList from "@/widgets/intake/ApplicationsList";
import styles from "./DashboardPage.module.css";

export default function DashboardPage() {
  const { hasProfile, isMeLoading, profileFirstName, user, logout } =
    useSession();
  const navigate = useNavigate();
  const create = useCreateApplication();
  const name = user?.name ?? user?.email ?? "there";
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

  if (isMeLoading || isWorker || hasProfile === undefined) {
    return <output>Checking your MySS account…</output>;
  }

  return (
    <div className={styles.page}>
      <div className={styles.actions}>
        <Button variant="primary" onPress={() => logout()}>
          Log out
        </Button>
      </div>
      <h1>Hello {name}</h1>
      {hasProfile && profileFirstName && (
        <p>Your MySS account profile is registered to {profileFirstName}.</p>
      )}

      <section aria-labelledby="my-applications">
        <div className={styles.sectionHeader}>
          <h2 id="my-applications">My applications</h2>
          <Button
            variant="primary"
            isDisabled={create.isPending}
            onPress={() =>
              create.mutate(undefined, {
                onSuccess: (application) =>
                  navigate(applicationPath(application.id)),
              })
            }
          >
            Start new application
          </Button>
        </div>
        {create.error && (
          <p role="alert">
            Could not start an application: {create.error.message}
          </p>
        )}
        <ApplicationsList />
      </section>
    </div>
  );
}
