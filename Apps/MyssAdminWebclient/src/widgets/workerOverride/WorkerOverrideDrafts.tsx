import { Link } from "react-router";

import styles from "@/widgets/review/ReviewQueue.module.css";

import { useWorkerOverrideDrafts } from "@/hooks/useWorkerOverride";
import { workerOverridePath } from "@/routes/paths";

// Every worker's unsubmitted worker override applications (MYSS-322), most
// recently saved first. The request number opens the draft to carry on with;
// any worker may finish any draft. Once submitted, an application leaves this
// list for the review queue (as does one later returned for more information).
// The table looks like the review queue's, so it borrows its styles.

function formatDate(value: string): string {
  return new Date(value).toLocaleString();
}

export default function WorkerOverrideDrafts() {
  const { data: drafts, error, isPending } = useWorkerOverrideDrafts();

  if (isPending) return <output>Loading unsubmitted applications…</output>;
  if (error) {
    return (
      <p role="alert">
        Could not load the unsubmitted applications: {error.message}
      </p>
    );
  }
  if (drafts.length === 0) {
    return <p>There are no unsubmitted applications.</p>;
  }

  return (
    <table className={styles.table}>
      <caption className={styles.caption}>Drafts</caption>
      <thead>
        <tr>
          <th scope="col">Request Number</th>
          <th scope="col">Client</th>
          <th scope="col">Started By</th>
          <th scope="col">Started On</th>
          <th scope="col">Last Saved</th>
        </tr>
      </thead>
      <tbody>
        {drafts.map((draft) => (
          <tr key={draft.id}>
            <td>
              <Link to={workerOverridePath(draft.id)}>
                {draft.referenceNumber}
              </Link>
            </td>
            <td>{draft.clientName ?? "Not entered yet"}</td>
            <td>{draft.startedBy ?? "—"}</td>
            <td>{formatDate(draft.createdAt)}</td>
            <td>{formatDate(draft.updatedAt)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
