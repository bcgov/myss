import { Link } from "react-router";

import styles from "./ReviewQueue.module.css";

import StatusPill from "@/components/StatusPill";
import { useReviewQueue } from "@/hooks/useReview";
import { workerApplicationPath } from "@/routes/paths";

// The worker's list of submitted applications, newest submission first. The
// request number is the way in; the status column says where each file is.

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function ReviewQueue() {
  const { data: applications, error, isPending } = useReviewQueue();

  if (isPending) return <p role="status">Loading submitted applications…</p>;
  if (error) {
    return (
      <p role="alert">
        Could not load the submitted applications: {error.message}
      </p>
    );
  }
  if (applications.length === 0) {
    return <p>No applications have been submitted yet.</p>;
  }

  return (
    <table className={styles.table}>
      <thead>
        <tr>
          <th scope="col">Request Number</th>
          <th scope="col">Submitted On</th>
          <th scope="col">Status</th>
        </tr>
      </thead>
      <tbody>
        {applications.map((application) => (
          <tr key={application.id}>
            <td>
              <Link to={workerApplicationPath(application.id)}>
                {application.referenceNumber}
              </Link>
            </td>
            <td>{formatDate(application.submittedAt)}</td>
            <td>
              <StatusPill status={application.status} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
