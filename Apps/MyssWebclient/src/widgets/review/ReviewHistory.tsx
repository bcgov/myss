import styles from "./ReviewHistory.module.css";

import type {
  ReviewHistoryCode,
  ReviewHistoryEntryPayload,
} from "@/hooks/useReview";

// What happened to a file, oldest first: each submit, pick-up, decision and
// promotion outcome, who did it and when. Notes are listed separately.

/** Display text for each history code. Interim, like the status labels. */
const HISTORY_LABELS: Record<ReviewHistoryCode, string> = {
  SUBMIT: "Submitted",
  RESUBMIT: "Re-submitted",
  PICK_UP: "Picked up for review",
  RELEASE: "Released to the queue",
  REQUEST_MORE_INFO: "More information requested",
  ESCALATE: "Escalated",
  ACCEPT: "Accepted",
  DENY: "Denied",
  PROMOTED: "Promoted",
  PROMOTION_FAILED: "Promotion failed",
};

function actorLabel(entry: ReviewHistoryEntryPayload): string {
  if (entry.actorKind === "WORKER") return entry.idir ?? "Worker";
  return entry.actorKind === "APPLICANT" ? "Applicant" : "System";
}

export default function ReviewHistory({
  history,
}: Readonly<{
  history: ReviewHistoryEntryPayload[];
}>) {
  return (
    <section aria-labelledby="review-history-heading">
      <h2 id="review-history-heading">History</h2>
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col">When</th>
            <th scope="col">What</th>
            <th scope="col">Who</th>
          </tr>
        </thead>
        <tbody>
          {history.map((entry, index) => (
            <tr key={`${entry.occurredAt}-${index}`}>
              <td>{new Date(entry.occurredAt).toLocaleString()}</td>
              <td>{HISTORY_LABELS[entry.code] ?? entry.code}</td>
              <td>{actorLabel(entry)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
