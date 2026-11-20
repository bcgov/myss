import { Link } from "react-router";
import { StatusPill } from "@myss/web-common/components";

import styles from "./ReviewQueue.module.css";

import {
  useReviewQueue,
  type ReviewApplicationSummaryPayload,
} from "@/hooks/useReview";
import { applicationPath } from "@/routes/paths";

// The worker's list, newest submission first: the queue (Submitted,
// Re-submitted, and Returned files waiting on the applicant), the files
// assigned to the signed-in worker, and every other submitted file — with
// another worker, escalated or decided. The
// request number is the way in; the status column says where each file is
// and, while it is under review, who has it.

/** Who has the file: "You", another worker's IDIR, or "Unassigned". */
function assignedTo(
  assignee: string | null | undefined,
  me: string | undefined,
): string {
  if (!assignee) return "Unassigned";
  return me && assignee.toUpperCase() === me.toUpperCase() ? "You" : assignee;
}

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString() : "—";
}

function ApplicationsTable({
  caption,
  applications,
  me,
}: Readonly<{
  caption: string;
  applications: ReviewApplicationSummaryPayload[];
  me: string | undefined;
}>) {
  return (
    <table className={styles.table}>
      <caption className={styles.caption}>{caption}</caption>
      <thead>
        <tr>
          <th scope="col">Request Number</th>
          <th scope="col">Applicant</th>
          <th scope="col">Submitted On</th>
          <th scope="col">Status</th>
          <th scope="col">Assigned To</th>
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
            <td>{application.applicantName ?? "—"}</td>
            <td>{formatDate(application.submittedAt)}</td>
            <td>
              <StatusPill
                status={application.status}
                assignee={application.assignee}
              />
            </td>
            <td>
              {assignedTo(application.assignee, me) === "You" ? (
                <strong>You</strong>
              ) : (
                assignedTo(application.assignee, me)
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default function ReviewQueue({
  myIdir: me,
}: Readonly<{
  /** The signed-in worker's IDIR, to pick out their own files. */
  myIdir?: string;
}>) {
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

  // Unassigned files waiting in the queue, the caller's own files, then
  // everything else (with another worker, escalated or decided).
  const queue = applications.filter((application) => application.inQueue);
  const mine = applications.filter(
    (application) =>
      !application.inQueue && assignedTo(application.assignee, me) === "You",
  );
  const others = applications.filter(
    (application) => !application.inQueue && !mine.includes(application),
  );

  return (
    <div className={styles.host}>
      {queue.length > 0 ? (
        <ApplicationsTable caption="Queue" applications={queue} me={me} />
      ) : (
        <p>The queue is empty.</p>
      )}
      {mine.length > 0 && (
        <ApplicationsTable
          caption="Assigned to you"
          applications={mine}
          me={me}
        />
      )}
      {others.length > 0 && (
        <ApplicationsTable
          caption={
            mine.length > 0
              ? "Other files under review and decided"
              : "Under review and decided"
          }
          applications={others}
          me={me}
        />
      )}
    </div>
  );
}
