import { Link } from "react-router";
import { StatusPill } from "@myss/web-common/components";

import styles from "./ReviewQueue.module.css";

import {
  useReviewQueue,
  type ReviewApplicationSummaryPayload,
} from "@/hooks/useReview";
import { applicationPath } from "@/routes/paths";

// The worker's list, newest submission first, in two tables: the queue
// (Submitted, Re-submitted, and Returned files waiting on the applicant) and
// every other submitted file — under review, escalated or decided. The
// request number is the way in; the status column says where each file is
// and, while it is under review, who has it.

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString() : "—";
}

function ApplicationsTable({
  caption,
  applications,
}: Readonly<{
  caption: string;
  applications: ReviewApplicationSummaryPayload[];
}>) {
  return (
    <table className={styles.table}>
      <caption className={styles.caption}>{caption}</caption>
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
              <Link to={applicationPath(application.id)}>
                {application.referenceNumber}
              </Link>
            </td>
            <td>{formatDate(application.submittedAt)}</td>
            <td>
              <StatusPill
                status={application.status}
                assignee={application.assignee}
              />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default function ReviewQueue() {
  const { data: applications, error, isPending } = useReviewQueue();

  if (isPending) return <output>Loading submitted applications…</output>;
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

  const queue = applications.filter((application) => application.inQueue);
  const others = applications.filter((application) => !application.inQueue);

  return (
    <div className={styles.host}>
      {queue.length > 0 ? (
        <ApplicationsTable caption="Queue" applications={queue} />
      ) : (
        <p>The queue is empty.</p>
      )}
      {others.length > 0 && (
        <ApplicationsTable
          caption="Under review and decided"
          applications={others}
        />
      )}
    </div>
  );
}
