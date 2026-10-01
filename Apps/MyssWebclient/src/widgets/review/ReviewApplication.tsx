import { Button } from "@bcgov/design-system-react-components";
import { useCallback, useState } from "react";
import { Link } from "react-router";

import styles from "./ReviewApplication.module.css";

import { ApplicationRequestError } from "@/api/intake";
import StatusPill from "@/components/StatusPill";
import {
  REVIEW_KEYWORDS,
  useReviewAction,
  useReviewApplication,
  type ReviewAction,
  type ReviewActionRoute,
} from "@/hooks/useReview";
import { paths } from "@/routes/paths";
import ReadOnlySpecForm from "@/widgets/ReadOnlySpecForm";

// One submitted application opened by a worker: what the applicant submitted,
// rendered read-only under the archived spec, and the moves the file allows
// right now. The buttons come from the server's availableActions, so a file
// that is already decided shows none.
//
// Design: docs/development_design/income-assistance-application.md, "Worker
// slice (MYSS-226)".

const MESSAGE_CONFLICT =
  "This application was changed by another worker before your action was saved. It has been reloaded; check its status before acting again.";

/** Button text and route for each action the API can offer. */
const ACTIONS: Record<
  ReviewAction,
  { label: string; route: ReviewActionRoute; variant: "primary" | "secondary" }
> = {
  Review: { label: "Under Review", route: "review", variant: "primary" },
  Accept: { label: "Accept", route: "accept", variant: "primary" },
  Deny: { label: "Deny", route: "deny", variant: "secondary" },
};

/** Spoken confirmation of what just happened, keyed by the new status. */
const OUTCOMES: Record<string, string> = {
  UNDER_REVIEW: "The application is now under review.",
  ACCEPTED: "The application has been accepted.",
  DENIED: "The application has been denied.",
};

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function ReviewApplication({
  applicationId,
}: {
  applicationId: string;
}) {
  const {
    data: application,
    error,
    isPending,
  } = useReviewApplication(applicationId);
  const act = useReviewAction(applicationId);
  const { mutate } = act;
  // The last outcome this session produced, announced under the summary.
  const [outcome, setOutcome] = useState<string>();

  const handleAction = useCallback(
    (action: ReviewAction) => {
      if (!application || act.isPending) return;
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
    [act.isPending, application, mutate],
  );

  if (isPending) return <p role="status">Loading application…</p>;
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
          <StatusPill status={application.status} />
        </dd>
      </dl>
      {conflict && (
        <p role="alert" className={styles.notice}>
          {conflict.keyword === REVIEW_KEYWORDS.notAllowed
            ? "That action is no longer available for this application. It has been reloaded; check its status."
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
      {application.availableActions.length > 0 && (
        <div className={styles.actions}>
          {application.availableActions.map((action) => (
            <Button
              key={action}
              variant={ACTIONS[action].variant}
              isDisabled={act.isPending}
              onPress={() => handleAction(action)}
            >
              {ACTIONS[action].label}
            </Button>
          ))}
        </div>
      )}
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
    </div>
  );
}
