import { Button } from "@bcgov/design-system-react-components";
import { Link, useNavigate } from "react-router";

import StatusPill from "@/components/StatusPill";
import {
  useAfaApplications,
  useAfaPublished,
  useCreateAfaApplication,
} from "@/hooks/useIntake";
import { afaServiceRequestPath } from "@/routes/paths";
import styles from "./ServiceRequestsPage.module.css";

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleDateString() : "—";
}

/** Citizen-only AFA slice of Service Requests; other request types follow later. */
export default function ServiceRequestsPage() {
  const navigate = useNavigate();
  const published = useAfaPublished();
  const { data: requests, error, isPending } = useAfaApplications();
  const create = useCreateAfaApplication();

  return (
    <div className={styles.page}>
      <h1>Service Requests</h1>
      {published.isPending && <output>Checking available applications…</output>}
      {published.error && (
        <p role="alert">
          Could not check available applications: {published.error.message}
        </p>
      )}
      {published.data === true && (
        <Button
          variant="primary"
          isDisabled={create.isPending}
          onPress={() =>
            create.mutate(undefined, {
              onSuccess: (application) =>
                void navigate(afaServiceRequestPath(application.id)),
            })
          }
        >
          Start Application for Assistance
        </Button>
      )}
      {create.error && (
        <p role="alert">
          Could not start an application: {create.error.message}
        </p>
      )}
      {(published.data === true ||
        (requests?.length ?? 0) > 0 ||
        isPending ||
        error) && (
        <section aria-labelledby="current-requests">
          <h2 id="current-requests">Your current applications</h2>
          {isPending && <output>Loading your service requests…</output>}
          {error && (
            <p role="alert">
              Could not load your service requests: {error.message}
            </p>
          )}
          {published.data === true && requests?.length === 0 && (
            <p>You have no Application for Assistance requests yet.</p>
          )}
          {requests && requests.length > 0 && (
            <ul className={styles.requests}>
              {requests.map((request) => (
                <li key={request.id} className={styles.request}>
                  <div className={styles.status}>
                    <StatusPill status={request.status} />
                  </div>
                  <div className={styles.details}>
                    <h3>Application for Assistance</h3>
                    <p>
                      Request number: <code>{request.referenceNumber}</code>
                    </p>
                    <p>Started: {formatDate(request.createdAt)}</p>
                    {request.submittedAt && (
                      <p>Submitted: {formatDate(request.submittedAt)}</p>
                    )}
                  </div>
                  <Link to={afaServiceRequestPath(request.id)}>
                    {request.status === "DRAFT"
                      ? "Continue application"
                      : "View application"}
                    <span className={styles.visuallyHidden}>
                      {" "}
                      {request.referenceNumber}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  );
}
