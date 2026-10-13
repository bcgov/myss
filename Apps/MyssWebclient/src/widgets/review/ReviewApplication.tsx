import { Button } from "@bcgov/design-system-react-components";
import { useCallback, useState } from "react";
import { Link } from "react-router";

import styles from "./ReviewApplication.module.css";

import { ApplicationRequestError } from "@/api/intake";
import StatusPill from "@/components/StatusPill";
import {
  REVIEW_KEYWORDS,
  useAddReviewNote,
  useReviewAction,
  useReviewApplication,
  type ReviewAction,
  type ReviewActionRoute,
} from "@/hooks/useReview";
import { paths } from "@/routes/paths";
import ReadOnlySpecForm from "@/widgets/ReadOnlySpecForm";
import ReviewHistory from "@/widgets/review/ReviewHistory";
import ReviewNotes from "@/widgets/review/ReviewNotes";

// One submitted application opened by a worker: its status and assignee, the
// internal notes, the moves the file allows the caller right now, what the
// applicant submitted (read-only under the archived spec), and its history.
// The buttons come from the server's availableActions, so a file that is
// decided, or that another worker has Under Review, shows none.
//
// Design: docs/development_design/income-assistance-application.md, "Worker
// slice (MYSS-226)".

const MESSAGE_UNSAVED_NOTE =
  "You have a note that has not been added. Add it or clear it before choosing an action.";
const MESSAGE_CONFLICT =
  "This application was changed by another worker before your action was saved. It has been reloaded; check its status before acting again.";

/** Button text and route for each action the API can offer. */
const ACTIONS: Record<
  ReviewAction,
  {
    label: string;
    route: ReviewActionRoute;
    variant: "primary" | "secondary" | "tertiary";
  }
> = {
  Review: { label: "Pick up for review", route: "review", variant: "primary" },
  RequestMoreInfo: {
    label: "Request more information",
    route: "request-more-info",
    variant: "secondary",
  },
  Escalate: { label: "Escalate", route: "escalate", variant: "secondary" },
  Deny: { label: "Deny", route: "deny", variant: "secondary" },
  Accept: { label: "Accept", route: "accept", variant: "primary" },
  Release: { label: "Release to queue", route: "release", variant: "tertiary" },
};

/** Spoken confirmation of what just happened, keyed by the new status. */
const OUTCOMES: Record<string, string> = {
  SUBMITTED: "The application has been released to the queue.",
  RESUBMITTED: "The application has been released to the queue.",
  UNDER_REVIEW: "The application is now under review and assigned to you.",
  RETURNED:
    "The application has been returned to the applicant for more information.",
  ESCALATED: "The application has been escalated for review.",
  ACCEPTED: "The application has been accepted.",
  DENIED: "The application has been denied.",
  PROMOTION_FAILED:
    "The application has been accepted, but handing it on to the case management system failed.",
};

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function ReviewApplication({
  applicationId,
}: Readonly<{
  applicationId: string;
}>) {
  const {
    data: application,
    error,
    isPending,
  } = useReviewApplication(applicationId);
  const act = useReviewAction(applicationId);
  const { mutate } = act;
  const addNote = useAddReviewNote(applicationId);
  const { mutate: mutateNote } = addNote;
  // The last outcome this session produced, announced under the summary.
  const [outcome, setOutcome] = useState<string>();
  // The note being written. A decision is held back while it has text, so
  // a note is never dropped by choosing an action before adding it.
  const [noteText, setNoteText] = useState("");
  const [unsavedNote, setUnsavedNote] = useState(false);
  // One write at a time: a note and an action sent together would carry the
  // same stream version, and one of them would be refused.
  const busy = act.isPending || addNote.isPending;

  const handleNoteChange = useCallback((text: string) => {
    setNoteText(text);
    setUnsavedNote(false);
  }, []);

  const handleAddNote = useCallback(() => {
    const text = noteText.trim();
    if (!application || busy || !text) return;
    setOutcome(undefined);
    mutateNote(
      { text, streamVersion: application.streamVersion },
      { onSuccess: () => setNoteText("") },
    );
  }, [application, busy, mutateNote, noteText]);

  const handleAction = useCallback(
    (action: ReviewAction) => {
      if (!application || busy) return;
      if (noteText.trim()) {
        setUnsavedNote(true);
        return;
      }
      setOutcome(undefined);
      mutate(
        {
          action: ACTIONS[action].route,
          streamVersion: application.streamVersion,
        },
        {
          onSuccess: (updated) => setOutcome(OUTCOMES[updated.status]),
        },
      );
    },
    [application, busy, mutate, noteText],
  );

  if (isPending) return <output>Loading application…</output>;
  if (error) {
    return (
      <p role="alert">
        {error instanceof ApplicationRequestError && error.status === 404
          ? "This application could not be found."
          : `Could not load the application: ${error.message}`}
      </p>
    );
  }

  const conflict =
    act.error instanceof ApplicationRequestError && act.error.isConflict
      ? act.error
      : null;
  const otherError = act.error && !conflict ? act.error : null;
  const heldByAnother =
    application.status === "UNDER_REVIEW" &&
    !!application.assignee &&
    application.availableActions.length === 0;

  return (
    <div className={styles.host}>
      <nav aria-label="Breadcrumb">
        <Link to={paths.workerApplications}>← Back to list</Link>
      </nav>
      <dl className={styles.summary}>
        <dt>Request number</dt>
        <dd>
          <code>{application.referenceNumber}</code>
        </dd>
        <dt>Submitted on</dt>
        <dd>{formatDate(application.submittedAt)}</dd>
        <dt>Status</dt>
        <dd>
          <StatusPill
            status={application.status}
            assignee={application.assignee}
          />
        </dd>
      </dl>
      {heldByAnother && (
        <p className={styles.outcome}>
          This application is under review by {application.assignee}. Only they
          can add notes or act on it until they decide it or release it.
        </p>
      )}
      {conflict && (
        <p role="alert" className={styles.notice}>
          {conflict.keyword === REVIEW_KEYWORDS.notAllowed
            ? "That action is no longer available for this application. It has been reloaded; check its status."
            : conflict.keyword === REVIEW_KEYWORDS.assignedToAnother
              ? "Another worker picked up this application first. It has been reloaded; only they can act on it now."
              : MESSAGE_CONFLICT}
        </p>
      )}
      {otherError && (
        <p role="alert" className={styles.notice}>
          The action could not be saved: {otherError.message}
        </p>
      )}
      {outcome && (
        <p role="status" aria-live="polite" className={styles.outcome}>
          {outcome}
        </p>
      )}
      <ReviewNotes
        application={application}
        text={noteText}
        onTextChange={handleNoteChange}
        onAdd={handleAddNote}
        isBusy={busy}
        error={addNote.error}
      />
      {unsavedNote && (
        <p role="alert" className={styles.notice}>
          {MESSAGE_UNSAVED_NOTE}
        </p>
      )}
      {application.availableActions.length > 0 && (
        <div className={styles.actions}>
          {application.availableActions.map((action) => (
            <Button
              key={action}
              variant={ACTIONS[action].variant}
              isDisabled={busy}
              onPress={() => handleAction(action)}
            >
              {ACTIONS[action].label}
            </Button>
          ))}
        </div>
      )}
      <h2>Submitted answers</h2>
      {application.spec ? (
        <ReadOnlySpecForm
          spec={application.spec.spec}
          answers={application.answers}
        />
      ) : (
        <p role="alert">
          The form version v{application.formSpecVersion} this application uses
          is no longer available from the content engine.
        </p>
      )}
      <ReviewHistory history={application.history} />
    </div>
  );
}
