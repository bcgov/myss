import { Button } from "@bcgov/design-system-react-components";
import { useNavigate } from "react-router";

import { useSession } from "@/auth/useSession";
import { useCreateApplication } from "@/hooks/useIntake";
import { applicationPath } from "@/routes/paths";
import ApplicationsList from "@/widgets/intake/ApplicationsList";
import styles from "./DashboardPage.module.css";

// The dashboard's Home section. DashboardLayout has already sent workers and
// unregistered citizens elsewhere, and Sign out lives in the header.
export default function DashboardPage() {
  const { hasProfile, profileFirstName, user } = useSession();
  const navigate = useNavigate();
  const create = useCreateApplication();
  const name = user?.name ?? user?.email ?? "there";

  return (
    <div className={styles.page}>
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
