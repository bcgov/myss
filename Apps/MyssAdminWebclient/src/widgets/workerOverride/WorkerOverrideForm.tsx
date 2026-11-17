import type { FormType } from "@formio/react/lib/components/Form";
import { useCallback, useEffect, useRef, useState } from "react";
import { ApplicationRequestError } from "@myss/web-common/api";
import {
  DraftApplicationForm,
  StatusPill,
  type DraftFormMessages,
} from "@myss/web-common/components";
import { ReadOnlySpecForm } from "@myss/formio";

import "@formio/js/dist/formio.form.min.css";
import styles from "@myss/web-common/components/ApplicationForm.module.css";

import {
  isEditableStatus,
  useSaveWorkerOverride,
  useSubmitWorkerOverride,
  useWorkerOverride,
  WORKER_OVERRIDE_KEYWORDS,
  type WorkerOverrideApplicationPayload,
} from "@/hooks/useWorkerOverride";

// One worker override Application for Assistance (MYSS-321): the client's six
// details, filled in by a worker for a client who cannot submit their own.
// While it is a draft, or once returned for more information, it renders in
// the shared DraftApplicationForm (as the citizen's ApplicationForm does).
// Once submitted it renders read-only, with the request number and status.
// No account id and no review queue link: the worker who fills it in is
// usually not the one who reviews it; reviewers find it under Applications to
// review. Any worker may open any draft.

const DRAFT_MESSAGES: DraftFormMessages = {
  conflict:
    "Another worker, tab or window saved a newer version of this application, so the answers here were not saved. The latest version has been loaded; check the answers and save again to keep these.",
  notEditable:
    "This application has been submitted and cannot be changed while it is being reviewed.",
  returned:
    "A worker asked for more information on this application. Update the answers, then submit it again.",
  saveFailed: "The answers could not be saved:",
  savedHint: "Any worker can open this draft from its link and finish it later.",
  saveLabel: "Save draft",
};

/**
 * A submitted application: just after the submit, the acknowledgement takes
 * focus so a screen-reader user hears the outcome. The read-only form follows.
 */
function SubmittedView({
  application,
  spec,
  announce,
}: Readonly<{
  application: WorkerOverrideApplicationPayload;
  spec: FormType;
  announce: boolean;
}>) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    if (announce) headingRef.current?.focus();
  }, [announce]);

  const submittedAt = application.submittedAt
    ? new Date(application.submittedAt).toLocaleString()
    : undefined;

  return (
    <>
      <section className={styles.outcome} aria-live="polite">
        <h2 ref={headingRef} tabIndex={-1} className={styles.heading}>
          {application.status === "RESUBMITTED"
            ? "Application resubmitted"
            : "Application submitted"}
        </h2>
        <p>
          The worker override account has been created and the application is
          in the review queue
          {submittedAt ? `. It was first submitted on ${submittedAt}` : ""}.
        </p>
        <dl className={styles.details}>
          <dt>Request number</dt>
          <dd>
            <code>{application.referenceNumber}</code>
          </dd>
          <dt>Status</dt>
          <dd>
            <StatusPill status={application.status} />
          </dd>
        </dl>
      </section>
      <ReadOnlySpecForm spec={spec} answers={application.answers} />
    </>
  );
}

/** A draft or a returned application, saved and submitted as worker override. */
function DraftForm({
  application,
  spec,
  onSubmitted,
}: Readonly<{
  application: WorkerOverrideApplicationPayload;
  spec: FormType;
  onSubmitted: () => void;
}>) {
  const save = useSaveWorkerOverride(application.id);
  const submit = useSubmitWorkerOverride(application.id);

  return (
    <DraftApplicationForm
      application={application}
      spec={spec}
      save={save}
      submit={submit}
      onSubmitted={onSubmitted}
      notEditableKeyword={WORKER_OVERRIDE_KEYWORDS.notEditable}
      messages={DRAFT_MESSAGES}
    />
  );
}

/**
 * Renders one worker override application by id: editable while it is a draft
 * or returned for more information, read-only once submitted. The spec comes
 * with the application (the version it was started on).
 */
export default function WorkerOverrideForm({
  applicationId,
}: Readonly<{
  applicationId: string;
}>) {
  const {
    data: application,
    error,
    isPending,
  } = useWorkerOverride(applicationId);
  // The acknowledgement takes focus only when the submit happened here; a
  // later visit just renders it.
  const [submittedHere, setSubmittedHere] = useState(false);
  const handleSubmitted = useCallback(() => setSubmittedHere(true), []);

  if (isPending) return <p>Loading application…</p>;
  if (error) {
    return (
      <p role="alert">
        {error instanceof ApplicationRequestError && error.status === 404
          ? "This worker override application could not be found."
          : `Could not load the application: ${error.message}`}
      </p>
    );
  }

  if (!application.spec) {
    return (
      <p role="alert">
        The form version v{application.formSpecVersion} this application uses
        is no longer available from the content engine.
      </p>
    );
  }

  if (!isEditableStatus(application.status)) {
    return (
      <SubmittedView
        application={application}
        spec={application.spec.spec}
        announce={submittedHere}
      />
    );
  }

  return (
    <DraftForm
      // A return remounts the form, so it starts from the answers as loaded.
      key={`${application.id}:${application.status}`}
      application={application}
      spec={application.spec.spec}
      onSubmitted={handleSubmitted}
    />
  );
}
