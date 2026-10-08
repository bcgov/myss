import { Link } from "react-router";

import styles from "./ApplicationsList.module.css";

import StatusPill from "@/components/StatusPill";
import { useApplications } from "@/hooks/useIntake";
import { applicationPath } from "@/routes/paths";

// The citizen's Income Assistance applications, newest first: every draft
// with a way back into it, every submitted one with a way to view it.

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function ApplicationsList() {
  const { data: applications, error, isPending } = useApplications();

  if (isPending) return <output>Loading your applications…</output>;
  if (error) {
    return (
      <p role="alert">Could not load your applications: {error.message}</p>
    );
  }
  if (applications.length === 0) {
    return <p>You have not started an application yet.</p>;
  }

  return (
    <table className={styles.table}>
      <thead>
        <tr>
          <th scope="col">Request number</th>
          <th scope="col">Status</th>
          <th scope="col">Started</th>
          <th scope="col">Submitted on</th>
          <th scope="col">
            <span className={styles.visuallyHidden}>Action</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {applications.map((application) => (
          <tr key={application.id}>
            <td>
              <code>{application.referenceNumber}</code>
            </td>
            <td>
              <StatusPill status={application.status} />
            </td>
            <td>{formatDate(application.createdAt)}</td>
            <td>{formatDate(application.submittedAt)}</td>
            <td>
              <Link to={applicationPath(application.id)}>
                {application.status === "DRAFT" ? "Continue" : "View"}
                <span className={styles.visuallyHidden}>
                  {" "}
                  application {application.referenceNumber}
                </span>
              </Link>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
