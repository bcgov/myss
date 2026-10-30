import { Link } from "react-router";
import { StatusPill } from "@myss/web-common/components";

import styles from "./ApplicationsList.module.css";

import { useApplications } from "@/hooks/useIntake";
import { applicationPath } from "@/routes/paths";

// The citizen's Income Assistance applications, newest first: every draft
// with a way back into it, every returned one with a way to update it, every
// other one with a way to view it.

const LINK_TEXT: Record<string, string> = {
  DRAFT: "Continue",
  RETURNED: "Update",
};

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

  // On a phone the rows are restyled as blocks of labelled lines, which can
  // drop a table's semantics in some browsers; the explicit roles keep them.
  return (
    <table className={styles.table} role="table">
      <thead role="rowgroup">
        <tr role="row">
          <th scope="col" role="columnheader">
            Request number
          </th>
          <th scope="col" role="columnheader">
            Status
          </th>
          <th scope="col" role="columnheader">
            Started
          </th>
          <th scope="col" role="columnheader">
            Submitted on
          </th>
          <th scope="col" role="columnheader">
            <span className={styles.visuallyHidden}>Action</span>
          </th>
        </tr>
      </thead>
      <tbody role="rowgroup">
        {applications.map((application) => (
          <tr key={application.id} role="row">
            <td role="cell" data-label="Request number">
              <code>{application.referenceNumber}</code>
            </td>
            <td role="cell" data-label="Status">
              <StatusPill status={application.status} />
            </td>
            <td role="cell" data-label="Started">
              {formatDate(application.createdAt)}
            </td>
            <td role="cell" data-label="Submitted on">
              {formatDate(application.submittedAt)}
            </td>
            <td role="cell">
              <Link to={applicationPath(application.id)}>
                {LINK_TEXT[application.status] ?? "View"}
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
